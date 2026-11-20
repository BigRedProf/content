using BigRedProf.Content.Core;
using BigRedProf.Content.Core.Data;
using BigRedProf.Content.Core.PackRats;
using BigRedProf.Content.Core.Providers;
using BigRedProf.Content.Test.TestDoubles;
using BigRedProf.Data.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace BigRedProf.Content.Test
{
	/// <summary>
	/// Reads src/Golden/golden.txt, captured from the content store before BitStream.
	/// The bytes in that file are the oracle for blobs, ids, shards, and catalog encodings.
	/// </summary>
	public class GoldenBytesTests
	{
		#region tests
		[Fact]
		public async Task StoredFormatsShouldMatchTheGoldenBytes()
		{
			string path = Path.Combine(AppContext.BaseDirectory, "Golden", "golden.txt");
			string text = File.ReadAllText(path);
			List<Sample> samples = Parse(text);

			Assert.Contains(samples, sample => sample.Name == "content-byte-aligned");
			Assert.Contains(samples, sample => sample.Name == "content-mid-byte");
			Assert.Contains(samples, sample => sample.Name == "content-zero-bit");
			Assert.Contains(samples, sample => sample.Name == "content-back-to-back");
			Assert.Equal(4, samples.Count);

			PiedPiper piedPiper = CreatePiedPiper();
			foreach(Sample sample in samples)
				await Replay(piedPiper, sample);
		}
		#endregion

		#region replay
		private static async Task Replay(PiedPiper piedPiper, Sample sample)
		{
			Assert.Equal("stored-content", sample.Kind);

			Code content = sample.Bits == "-" ? new Code(0) : new Code(sample.Bits);
			Assert.Equal(sample.BitLength, content.Length);

			if(sample.Name == "content-mid-byte")
				Assert.NotEqual(0, content.Length % 8);

			if(sample.Name == "content-zero-bit")
				Assert.Equal(0, content.Length);

			if(sample.Operations.Count > 0)
			{
				Assert.Equal(sample.Bits, ConcatenateOperations(sample.Operations));
				Assert.Equal("content-back-to-back", sample.Name);
			}

			byte[] blob = piedPiper.SaveCodeToByteArray(content);
			Assert.Equal(ParseHex(sample.BlobHex), blob);
			Assert.Equal(content, piedPiper.LoadCodeFromByteArray(ParseHex(sample.BlobHex)));

			Multihash multihash = Multihash.FromCode(content, ContentStore.DefaultAlgorithm);
			Assert.Equal(sample.Id, multihash.ToMultibaseString());
			Assert.Equal(multihash, Multihash.Parse(sample.Id));
			Assert.Equal(sample.Shard, DiskContentStoreStorageProvider.GetShard(sample.Id));
			Assert.Equal("blobs/" + sample.Shard + "/" + sample.Id, sample.Path);

			if((content.Length % 8) == 0)
			{
				Assert.Equal(
					Multihash.FromBytes(content.ToByteArray(), ContentStore.DefaultAlgorithm),
					multihash
				);
			}
			else
			{
				Assert.NotEqual(
					Multihash.FromBytes(content.ToByteArray(), ContentStore.DefaultAlgorithm),
					multihash
				);
			}

			ContentStored model = new ContentStored()
			{
				Multihash = multihash
			};
			Code stored = piedPiper.PackModel<ContentStored>(model, ContentSchemaId.ContentStored);
			Assert.Equal(sample.StoredBitLength, stored.Length);
			Assert.Equal(ParseHex(sample.StoredHex), stored.ToByteArray());

			Code storedFromGolden = new Code(ParseHex(sample.StoredHex), sample.StoredBitLength);
			ContentStored unpackedStored = piedPiper.UnpackModel<ContentStored>(
				storedFromGolden,
				ContentSchemaId.ContentStored
			);
			Assert.Equal(multihash, unpackedStored.Multihash);

			Datum datum = piedPiper.PackDatum(model, ContentSchemaId.ContentStored);
			Code catalog = piedPiper.PackModel<Datum>(datum, CoreSchema.Datum);
			Assert.Equal(sample.CatalogBitLength, catalog.Length);
			Assert.Equal(ParseHex(sample.CatalogHex), catalog.ToByteArray());

			Code catalogFromGolden = new Code(ParseHex(sample.CatalogHex), sample.CatalogBitLength);
			Datum unpackedDatum = piedPiper.UnpackModel<Datum>(catalogFromGolden, CoreSchema.Datum);
			Assert.Equal(new Guid(ContentSchemaId.ContentStored), unpackedDatum.SchemaId);
			ContentStored fromCatalog = unpackedDatum.Unpack<ContentStored>(piedPiper);
			Assert.Equal(multihash, fromCatalog.Multihash);

			string root = Path.Combine(Path.GetTempPath(), "BigRedProf.Content.Test", "GoldenBytes", sample.Name);
			if(Directory.Exists(root))
				Directory.Delete(root, true);

			try
			{
				ListScribe scribe = new ListScribe();
				ContentStore store = new ContentStore(new DiskContentStoreStorageProvider(root), scribe);
				Multihash storedId = await store.PutContentAsync(content);

				Assert.Equal(multihash, storedId);
				Assert.Equal(ParseHex(sample.BlobHex), File.ReadAllBytes(BlobPath(root, sample)));
				Assert.Equal(content, await store.TryGetContentAsync(storedId));

				Assert.Single(scribe.Things);
				Assert.Equal(catalogFromGolden, scribe.Things[0]);
			}
			finally
			{
				if(Directory.Exists(root))
					Directory.Delete(root, true);
			}
		}

		private static PiedPiper CreatePiedPiper()
		{
			PiedPiper piedPiper = new PiedPiper();
			piedPiper.RegisterCorePackRats();
			piedPiper.RegisterPackRat<ContentStored>(
				new ContentStoredPackRat(piedPiper),
				ContentSchemaId.ContentStored
			);
			return piedPiper;
		}

		private static string ConcatenateOperations(IReadOnlyList<string> operations)
		{
			StringBuilder builder = new StringBuilder();
			foreach(string operation in operations)
			{
				Assert.StartsWith("WRITE ", operation);
				string written = operation.Substring("WRITE ".Length);
				if(written != "-")
					builder.Append(written);
			}

			string bits = builder.ToString();
			if(bits.Length == 0)
				return "-";

			return bits;
		}

		private static string BlobPath(string root, Sample sample)
		{
			return Path.Combine(root, "blobs", sample.Shard, sample.Id);
		}
		#endregion

		#region parsing
		private static List<Sample> Parse(string text)
		{
			List<Sample> samples = new List<Sample>();
			Sample? current = null;

			string[] lines = text.Split('\n');
			for(int i = 0; i < lines.Length; ++i)
			{
				string line = lines[i].TrimEnd('\r');
				if(line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
					continue;

				if(line.StartsWith("SAMPLE ", StringComparison.Ordinal))
				{
					current = new Sample(line.Substring("SAMPLE ".Length));
					continue;
				}

				if(current == null)
					throw new InvalidOperationException("Line outside a sample: " + line);

				if(line == "END")
				{
					samples.Add(current);
					current = null;
					continue;
				}

				if(line.StartsWith("OP ", StringComparison.Ordinal))
				{
					current.Operations.Add(line.Substring("OP ".Length));
					continue;
				}

				int space = line.IndexOf(' ');
				if(space < 0)
					throw new InvalidOperationException("Unknown golden line '" + line + "'.");

				string key = line.Substring(0, space);
				string rest = line.Substring(space + 1);
				if(key == "KIND")
					current.Kind = rest;
				else if(key == "BITS")
					current.Bits = rest;
				else if(key == "BIT_LENGTH")
					current.BitLength = int.Parse(rest, CultureInfo.InvariantCulture);
				else if(key == "BLOB_HEX")
					current.BlobHex = rest;
				else if(key == "ID")
					current.Id = rest;
				else if(key == "SHARD")
					current.Shard = rest;
				else if(key == "PATH")
					current.Path = rest;
				else if(key == "STORED_BIT_LENGTH")
					current.StoredBitLength = int.Parse(rest, CultureInfo.InvariantCulture);
				else if(key == "STORED_HEX")
					current.StoredHex = rest;
				else if(key == "CATALOG_BIT_LENGTH")
					current.CatalogBitLength = int.Parse(rest, CultureInfo.InvariantCulture);
				else if(key == "CATALOG_HEX")
					current.CatalogHex = rest;
				else
					throw new InvalidOperationException("Unknown golden line '" + line + "'.");
			}

			if(current != null)
				throw new InvalidOperationException("Sample " + current.Name + " is missing END.");

			return samples;
		}

		private static byte[] ParseHex(string hex)
		{
			if(hex == "-")
				return Array.Empty<byte>();

			if((hex.Length % 2) != 0)
				throw new InvalidOperationException("Hex length is odd.");

			byte[] bytes = new byte[hex.Length / 2];
			for(int i = 0; i < bytes.Length; ++i)
				bytes[i] = byte.Parse(hex.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

			return bytes;
		}
		#endregion

		#region nested types
		private sealed class Sample
		{
			public Sample(string name)
			{
				Name = name;
				Kind = "";
				Bits = "";
				BlobHex = "";
				Id = "";
				Shard = "";
				Path = "";
				StoredHex = "";
				CatalogHex = "";
				Operations = new List<string>();
			}

			public string Name
			{
				get;
			}

			public string Kind
			{
				get;
				set;
			}

			public string Bits
			{
				get;
				set;
			}

			public int BitLength
			{
				get;
				set;
			}

			public string BlobHex
			{
				get;
				set;
			}

			public string Id
			{
				get;
				set;
			}

			public string Shard
			{
				get;
				set;
			}

			public string Path
			{
				get;
				set;
			}

			public int StoredBitLength
			{
				get;
				set;
			}

			public string StoredHex
			{
				get;
				set;
			}

			public int CatalogBitLength
			{
				get;
				set;
			}

			public string CatalogHex
			{
				get;
				set;
			}

			public List<string> Operations
			{
				get;
			}
		}
		#endregion
	}
}
