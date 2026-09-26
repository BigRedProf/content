using BigRedProf.Content.Core;
using BigRedProf.Content.Core.Providers;
using BigRedProf.Content.Test.TestDoubles;
using BigRedProf.Data.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xunit;

namespace BigRedProf.Content.Test
{
	public class DiskContentStoreStorageProviderTests
	{
		#region PutBlobAsync tests
		[Fact]
		public async Task PutBlobShouldRoundTrip()
		{
			string root = CreateRoot(nameof(PutBlobShouldRoundTrip));
			try
			{
				DiskContentStoreStorageProvider provider = new DiskContentStoreStorageProvider(root);
				byte[] blob = new byte[] { 0x42, 0x49, 0x47 };
				Multihash multihash = Multihash.FromBytes(blob, MultihashAlgorithm.Sha256);

				await provider.PutBlobAsync(multihash, blob);
				byte[]? fetchedBlob = await provider.TryGetBlobAsync(multihash);

				Assert.NotNull(fetchedBlob);
				Assert.Equal(blob, fetchedBlob);
			}
			finally
			{
				DeleteRoot(root);
			}
		}

		[Fact]
		public async Task PutBlobShouldBeIdempotent()
		{
			string root = CreateRoot(nameof(PutBlobShouldBeIdempotent));
			try
			{
				DiskContentStoreStorageProvider provider = new DiskContentStoreStorageProvider(root);
				byte[] blob = new byte[] { 0x42, 0x49, 0x47 };
				Multihash multihash = Multihash.FromBytes(blob, MultihashAlgorithm.Sha256);

				await provider.PutBlobAsync(multihash, blob);
				await provider.PutBlobAsync(multihash, blob);

				string[] files = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
				Assert.Single(files);
				Assert.Equal(blob, await provider.TryGetBlobAsync(multihash));
			}
			finally
			{
				DeleteRoot(root);
			}
		}

		[Fact]
		public async Task PutBlobShouldDefensivelyCopyTheBlob()
		{
			string root = CreateRoot(nameof(PutBlobShouldDefensivelyCopyTheBlob));
			try
			{
				DiskContentStoreStorageProvider provider = new DiskContentStoreStorageProvider(root);
				byte[] blob = new byte[] { 0x42, 0x49, 0x47 };
				Multihash multihash = Multihash.FromBytes(blob, MultihashAlgorithm.Sha256);

				await provider.PutBlobAsync(multihash, blob);
				blob[0] = 0xFF;

				byte[]? fetchedBlob = await provider.TryGetBlobAsync(multihash);
				Assert.NotNull(fetchedBlob);
				Assert.Equal(new byte[] { 0x42, 0x49, 0x47 }, fetchedBlob);
			}
			finally
			{
				DeleteRoot(root);
			}
		}

		[Fact]
		public async Task PutBlobShouldPersistAcrossProviderInstances()
		{
			string root = CreateRoot(nameof(PutBlobShouldPersistAcrossProviderInstances));
			try
			{
				byte[] blob = new byte[] { 0x42, 0x49, 0x47 };
				Multihash multihash = Multihash.FromBytes(blob, MultihashAlgorithm.Sha256);

				DiskContentStoreStorageProvider writer = new DiskContentStoreStorageProvider(root);
				await writer.PutBlobAsync(multihash, blob);

				DiskContentStoreStorageProvider reader = new DiskContentStoreStorageProvider(root);
				byte[]? fetchedBlob = await reader.TryGetBlobAsync(multihash);

				Assert.NotNull(fetchedBlob);
				Assert.Equal(blob, fetchedBlob);
			}
			finally
			{
				DeleteRoot(root);
			}
		}

		[Fact]
		public async Task PutBlobShouldShardPastTheFixedMultibasePrefix()
		{
			string root = CreateRoot(nameof(PutBlobShouldShardPastTheFixedMultibasePrefix));
			try
			{
				DiskContentStoreStorageProvider provider = new DiskContentStoreStorageProvider(root);
				byte[] firstBlob = new byte[] { 0x42, 0x49, 0x47 };
				byte[] secondBlob = new byte[] { 0x01 };
				Multihash firstMultihash = Multihash.FromBytes(firstBlob, MultihashAlgorithm.Sha256);
				Multihash secondMultihash = Multihash.FromBytes(secondBlob, MultihashAlgorithm.Sha256);

				await provider.PutBlobAsync(firstMultihash, firstBlob);
				await provider.PutBlobAsync(secondMultihash, secondBlob);

				string firstShard = AssertBlobPath(root, firstMultihash);
				string secondShard = AssertBlobPath(root, secondMultihash);
				Assert.NotEqual(firstShard, secondShard);
			}
			finally
			{
				DeleteRoot(root);
			}
		}

		[Fact]
		public async Task PutBlobShouldStayIdempotentWhenRaced()
		{
			string root = CreateRoot(nameof(PutBlobShouldStayIdempotentWhenRaced));
			try
			{
				DiskContentStoreStorageProvider provider = new DiskContentStoreStorageProvider(root);
				byte[] blob = new byte[] { 0x42, 0x49, 0x47, 0x52, 0x45, 0x44 };
				Multihash multihash = Multihash.FromBytes(blob, MultihashAlgorithm.Sha256);

				Task[] puts = new Task[8];
				for(int i = 0; i < puts.Length; i++)
					puts[i] = provider.PutBlobAsync(multihash, blob);

				await Task.WhenAll(puts);

				string[] files = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
				Assert.Single(files);
				Assert.Equal(blob, File.ReadAllBytes(files[0]));
				Assert.Equal(blob, await provider.TryGetBlobAsync(multihash));
			}
			finally
			{
				DeleteRoot(root);
			}
		}

		[Fact]
		public async Task PutBlobShouldLeaveAnExistingBlobUntouched()
		{
			string root = CreateRoot(nameof(PutBlobShouldLeaveAnExistingBlobUntouched));
			try
			{
				byte[] blob = new byte[] { 0x42, 0x49, 0x47 };
				Multihash multihash = Multihash.FromBytes(blob, MultihashAlgorithm.Sha256);
				string finalPath = BlobPath(root, multihash);
				string? directory = Path.GetDirectoryName(finalPath);
				Assert.NotNull(directory);
				Directory.CreateDirectory(directory!);

				// A complete file already occupies the final path. A later put must not
				// replace it: replacing is how a torn write would become visible, and
				// hash-addressed storage keeps the first blob.
				byte[] existing = new byte[] { 0x11, 0x22 };
				File.WriteAllBytes(finalPath, existing);

				DiskContentStoreStorageProvider provider = new DiskContentStoreStorageProvider(root);
				await provider.PutBlobAsync(multihash, blob);

				Assert.Equal(existing, File.ReadAllBytes(finalPath));
			}
			finally
			{
				DeleteRoot(root);
			}
		}

		[Fact]
		public async Task PutBlobShouldThrowWhenArgumentsAreNull()
		{
			string root = CreateRoot(nameof(PutBlobShouldThrowWhenArgumentsAreNull));
			try
			{
				DiskContentStoreStorageProvider provider = new DiskContentStoreStorageProvider(root);
				byte[] blob = new byte[] { 0x01 };
				Multihash multihash = Multihash.FromBytes(blob, MultihashAlgorithm.Sha256);

				await Assert.ThrowsAsync<ArgumentNullException>(
					async () =>
					{
						await provider.PutBlobAsync(null!, blob);
					}
				);

				await Assert.ThrowsAsync<ArgumentNullException>(
					async () =>
					{
						await provider.PutBlobAsync(multihash, null!);
					}
				);
			}
			finally
			{
				DeleteRoot(root);
			}
		}
		#endregion

		#region TryGetBlobAsync tests
		[Fact]
		public async Task TryGetBlobShouldReturnNullWhenBlobNotFound()
		{
			string root = CreateRoot(nameof(TryGetBlobShouldReturnNullWhenBlobNotFound));
			try
			{
				DiskContentStoreStorageProvider provider = new DiskContentStoreStorageProvider(root);
				Multihash multihash = Multihash.FromBytes(new byte[] { 0x01 }, MultihashAlgorithm.Sha256);

				byte[]? fetchedBlob = await provider.TryGetBlobAsync(multihash);

				Assert.Null(fetchedBlob);
			}
			finally
			{
				DeleteRoot(root);
			}
		}

		[Fact]
		public async Task TryGetBlobShouldReturnNullWhenTheStoreDirectoryDoesNotExist()
		{
			string root = Path.Combine(
				CreateRoot(nameof(TryGetBlobShouldReturnNullWhenTheStoreDirectoryDoesNotExist)),
				"missing"
			);
			try
			{
				DiskContentStoreStorageProvider provider = new DiskContentStoreStorageProvider(root);
				Multihash multihash = Multihash.FromBytes(new byte[] { 0x01 }, MultihashAlgorithm.Sha256);

				byte[]? fetchedBlob = await provider.TryGetBlobAsync(multihash);

				Assert.Null(fetchedBlob);
				Assert.False(Directory.Exists(root));
			}
			finally
			{
				DeleteRoot(Path.GetDirectoryName(root)!);
			}
		}

		[Fact]
		public async Task TryGetBlobShouldDefensivelyCopyTheBlob()
		{
			string root = CreateRoot(nameof(TryGetBlobShouldDefensivelyCopyTheBlob));
			try
			{
				DiskContentStoreStorageProvider provider = new DiskContentStoreStorageProvider(root);
				byte[] blob = new byte[] { 0x42, 0x49, 0x47 };
				Multihash multihash = Multihash.FromBytes(blob, MultihashAlgorithm.Sha256);

				await provider.PutBlobAsync(multihash, blob);

				byte[]? firstFetchedBlob = await provider.TryGetBlobAsync(multihash);
				Assert.NotNull(firstFetchedBlob);
				firstFetchedBlob![0] = 0xFF;

				byte[]? secondFetchedBlob = await provider.TryGetBlobAsync(multihash);
				Assert.NotNull(secondFetchedBlob);
				Assert.Equal(new byte[] { 0x42, 0x49, 0x47 }, secondFetchedBlob);
			}
			finally
			{
				DeleteRoot(root);
			}
		}

		[Fact]
		public async Task TryGetBlobShouldIgnoreAnIncompleteTemporaryFile()
		{
			string root = CreateRoot(nameof(TryGetBlobShouldIgnoreAnIncompleteTemporaryFile));
			try
			{
				byte[] blob = new byte[] { 0x42, 0x49, 0x47 };
				Multihash multihash = Multihash.FromBytes(blob, MultihashAlgorithm.Sha256);
				string finalPath = BlobPath(root, multihash);
				string? directory = Path.GetDirectoryName(finalPath);
				Assert.NotNull(directory);
				Directory.CreateDirectory(directory!);

				// A crashed put leaves its temp next to where the blob will be. That
				// file is not the blob, and a later successful put must not return it.
				string tempPath = Path.Combine(directory!, "tmp-crashed");
				File.WriteAllBytes(tempPath, new byte[] { 0x01, 0x02, 0x03 });

				DiskContentStoreStorageProvider provider = new DiskContentStoreStorageProvider(root);
				Assert.Null(await provider.TryGetBlobAsync(multihash));

				await provider.PutBlobAsync(multihash, blob);

				byte[]? fetchedBlob = await provider.TryGetBlobAsync(multihash);
				Assert.Equal(blob, fetchedBlob);
				Assert.Equal(blob, File.ReadAllBytes(finalPath));
			}
			finally
			{
				DeleteRoot(root);
			}
		}

		[Fact]
		public async Task TryGetBlobShouldThrowWhenMultihashIsNull()
		{
			string root = CreateRoot(nameof(TryGetBlobShouldThrowWhenMultihashIsNull));
			try
			{
				DiskContentStoreStorageProvider provider = new DiskContentStoreStorageProvider(root);

				await Assert.ThrowsAsync<ArgumentNullException>(
					async () =>
					{
						await provider.TryGetBlobAsync(null!);
					}
				);
			}
			finally
			{
				DeleteRoot(root);
			}
		}
		#endregion

		#region constructor tests
		[Fact]
		public void ConstructorShouldThrowWhenRootDirectoryIsNullOrWhitespace()
		{
			Assert.Throws<ArgumentNullException>(
				() =>
				{
					new DiskContentStoreStorageProvider(null!);
				}
			);

			Assert.Throws<ArgumentException>(
				() =>
				{
					new DiskContentStoreStorageProvider(" ");
				}
			);
		}

		[Fact]
		public void ConstructorShouldThrowWhenRootDirectoryIsAFile()
		{
			string root = CreateRoot(nameof(ConstructorShouldThrowWhenRootDirectoryIsAFile));
			try
			{
				string filePath = Path.Combine(root, "not-a-directory");
				File.WriteAllText(filePath, "x");

				Assert.Throws<ArgumentException>(
					() =>
					{
						new DiskContentStoreStorageProvider(filePath);
					}
				);
			}
			finally
			{
				DeleteRoot(root);
			}
		}
		#endregion

		#region ContentStore tests
		[Fact]
		public async Task ContentShouldPersistAcrossProviderInstances()
		{
			string root = CreateRoot(nameof(ContentShouldPersistAcrossProviderInstances));
			try
			{
				Code content = new Code(new byte[] { 0x42, 0x49, 0x47, 0x52, 0x45, 0x44 });
				Multihash multihash;

				DiskContentStoreStorageProvider writer = new DiskContentStoreStorageProvider(root);
				ContentStore writingStore = new ContentStore(writer, new ListScribe());
				multihash = await writingStore.PutContentAsync(content);

				// A new process gets a new provider and a new catalog scribe. The catalog
				// is not this provider's job; the blob on disk is enough to read the content.
				DiskContentStoreStorageProvider reader = new DiskContentStoreStorageProvider(root);
				ContentStore readingStore = new ContentStore(reader, new ListScribe());
				Code? fetchedContent = await readingStore.TryGetContentAsync(multihash);

				Assert.NotNull(fetchedContent);
				Assert.Equal(content, fetchedContent);
			}
			finally
			{
				DeleteRoot(root);
			}
		}

		[Fact]
		public async Task CorruptBlobShouldSurfaceAsContentIntegrityException()
		{
			string root = CreateRoot(nameof(CorruptBlobShouldSurfaceAsContentIntegrityException));
			try
			{
				Code content = new Code(new byte[] { 0x42, 0x49, 0x47, 0x52, 0x45, 0x44 });
				DiskContentStoreStorageProvider writer = new DiskContentStoreStorageProvider(root);
				ContentStore writingStore = new ContentStore(writer, new ListScribe());
				Multihash multihash = await writingStore.PutContentAsync(content);

				string path = BlobPath(root, multihash);
				byte[] stored = File.ReadAllBytes(path);
				stored[stored.Length - 1] ^= 0x01;
				File.WriteAllBytes(path, stored);

				DiskContentStoreStorageProvider reader = new DiskContentStoreStorageProvider(root);
				ContentStore readingStore = new ContentStore(reader, new ListScribe());

				// The provider stays dumb and returns the bytes on disk. ContentStore
				// re-hashes and is what turns the corruption into an integrity failure.
				Assert.Equal(stored, await reader.TryGetBlobAsync(multihash));

				ContentIntegrityException exception = await Assert.ThrowsAsync<ContentIntegrityException>(
					async () =>
					{
						await readingStore.TryGetContentAsync(multihash);
					}
				);

				Assert.Equal(multihash, exception.ExpectedMultihash);
				Assert.NotEqual(multihash, exception.ActualMultihash);
			}
			finally
			{
				DeleteRoot(root);
			}
		}

		[Fact]
		public async Task MissingBlobShouldReturnNullFromContentStore()
		{
			string root = CreateRoot(nameof(MissingBlobShouldReturnNullFromContentStore));
			try
			{
				DiskContentStoreStorageProvider provider = new DiskContentStoreStorageProvider(root);
				ContentStore store = new ContentStore(provider, new ListScribe());
				Multihash multihash = Multihash.FromBytes(new byte[] { 0x01, 0x02, 0x03 }, MultihashAlgorithm.Sha256);

				Assert.Null(await provider.TryGetBlobAsync(multihash));

				Code? fetchedContent = await store.TryGetContentAsync(multihash);

				Assert.Null(fetchedContent);
			}
			finally
			{
				DeleteRoot(root);
			}
		}
		#endregion

		#region path tests
		[Fact]
		public async Task PutBlobShouldStoreTheBlobInATwoCharacterSubfolder()
		{
			string root = CreateRoot(nameof(PutBlobShouldStoreTheBlobInATwoCharacterSubfolder));
			try
			{
				DiskContentStoreStorageProvider provider = new DiskContentStoreStorageProvider(root);
				byte[] blob = new byte[] { 0x42, 0x49, 0x47 };
				Multihash multihash = Multihash.FromBytes(blob, MultihashAlgorithm.Sha256);

				await provider.PutBlobAsync(multihash, blob);

				string blobsDirectory = Path.Combine(Path.GetFullPath(root), "blobs");
				string[] shardDirectories = Directory.GetDirectories(blobsDirectory);
				Assert.Single(shardDirectories);

				string shardName = Path.GetFileName(shardDirectories[0]);
				Assert.Equal(2, shardName.Length);

				string id = multihash.ToMultibaseString();
				Assert.Equal(id.Substring(4, 2), shardName);
				Assert.NotEqual(id.Substring(0, 2), shardName);

				string[] files = Directory.GetFiles(shardDirectories[0]);
				Assert.Single(files);
				Assert.Equal(id, Path.GetFileName(files[0]));
			}
			finally
			{
				DeleteRoot(root);
			}
		}

		[Fact]
		public async Task PutBlobShouldRoundTripWhenRootContainsSpaces()
		{
			string root = CreateRoot(nameof(PutBlobShouldRoundTripWhenRootContainsSpaces) + " with spaces");
			try
			{
				Assert.Contains(" ", root);

				DiskContentStoreStorageProvider provider = new DiskContentStoreStorageProvider(root);
				byte[] blob = new byte[] { 0x42, 0x49, 0x47 };
				Multihash multihash = Multihash.FromBytes(blob, MultihashAlgorithm.Sha256);

				await provider.PutBlobAsync(multihash, blob);
				byte[]? fetchedBlob = await provider.TryGetBlobAsync(multihash);

				Assert.Equal(blob, fetchedBlob);

				string path = BlobPath(root, multihash);
				Assert.Contains(" with spaces", path);
				Assert.True(File.Exists(path));
			}
			finally
			{
				DeleteRoot(root);
			}
		}

		[Fact]
		public async Task PutBlobShouldRoundTripWhenRootUsesBackslashes()
		{
			string parent = CreateRoot(nameof(PutBlobShouldRoundTripWhenRootUsesBackslashes));
			// A caller on Windows can hand the provider a root that is already
			// separated with backslashes, including a segment that contains a space.
			string root = parent.TrimEnd('\\', '/') + "\\disk store";
			try
			{
				Assert.Contains("\\", root);

				DiskContentStoreStorageProvider provider = new DiskContentStoreStorageProvider(root);
				byte[] blob = new byte[] { 0x42, 0x49, 0x47 };
				Multihash multihash = Multihash.FromBytes(blob, MultihashAlgorithm.Sha256);

				await provider.PutBlobAsync(multihash, blob);

				string resolvedRoot = Path.GetFullPath(root);
				string shard = AssertBlobPath(resolvedRoot, multihash);
				Assert.Equal(2, shard.Length);

				// On Windows the backslash is a separator, so the store is a child
				// directory. Elsewhere it is just a character in the directory name,
				// and the round trip above is what has to keep working.
				if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
					Assert.True(Directory.Exists(Path.Combine(parent, "disk store")));

				DiskContentStoreStorageProvider reader = new DiskContentStoreStorageProvider(root);
				Assert.Equal(blob, await reader.TryGetBlobAsync(multihash));
			}
			finally
			{
				DeleteRoot(Path.GetFullPath(root));
				DeleteRoot(parent);
			}
		}

		[Fact]
		public async Task PutBlobShouldRoundTripANonSha256Multihash()
		{
			string root = CreateRoot(nameof(PutBlobShouldRoundTripANonSha256Multihash));
			try
			{
				// sha2-512 is multicodec 0x13. Its base32 form does not start with the
				// sha2-256 header "bciq", and the header is still 16 bits, so the shard
				// stays at offset 4.
				byte[] digest = new byte[64];
				digest[0] = 0x42;
				Multihash multihash = CreateMultihash(0x13, digest);
				string id = multihash.ToMultibaseString();
				Assert.False(id.StartsWith("bciq"));

				byte[] blob = new byte[] { 0x11, 0x22, 0x33 };
				DiskContentStoreStorageProvider provider = new DiskContentStoreStorageProvider(root);
				await provider.PutBlobAsync(multihash, blob);

				string path = Path.Combine(Path.GetFullPath(root), "blobs", id.Substring(4, 2), id);
				Assert.True(File.Exists(path));
				Assert.Equal(blob, await provider.TryGetBlobAsync(multihash));
			}
			finally
			{
				DeleteRoot(root);
			}
		}

		[Fact]
		public async Task PutBlobShouldShardAWiderHeaderByItsOwnSize()
		{
			string root = CreateRoot(nameof(PutBlobShouldShardAWiderHeaderByItsOwnSize));
			try
			{
				// Code 0xC8 takes two varint bytes, plus one byte of digest length:
				// 24 header bits, so the shard starts at offset 5 rather than 4.
				byte[] digest = new byte[32];
				digest[0] = 0x7E;
				Multihash multihash = CreateMultihash(0xC8, digest);
				string id = multihash.ToMultibaseString();
				Assert.NotEqual(id.Substring(4, 2), id.Substring(5, 2));

				byte[] blob = new byte[] { 0x44, 0x55 };
				DiskContentStoreStorageProvider provider = new DiskContentStoreStorageProvider(root);
				await provider.PutBlobAsync(multihash, blob);

				string path = Path.Combine(Path.GetFullPath(root), "blobs", id.Substring(5, 2), id);
				Assert.True(File.Exists(path));
				Assert.False(File.Exists(Path.Combine(Path.GetFullPath(root), "blobs", id.Substring(4, 2), id)));
				Assert.Equal(blob, await provider.TryGetBlobAsync(multihash));
			}
			finally
			{
				DeleteRoot(root);
			}
		}
		#endregion

		#region private methods
		private static string CreateRoot(string name)
		{
			string root = Path.Combine(Path.GetTempPath(), "BigRedProf.Content.Test", "DiskContentStoreStorageProvider", name);
			if(Directory.Exists(root))
				Directory.Delete(root, true);

			Directory.CreateDirectory(root);
			return root;
		}

		private static void DeleteRoot(string root)
		{
			if(Directory.Exists(root))
				Directory.Delete(root, true);
		}

		private static string BlobPath(string root, Multihash multihash)
		{
			// Skip the fixed "bciq" prefix. See DiskContentStoreStorageProvider.
			string id = multihash.ToMultibaseString();
			string shard = id.Substring(4, 2);
			return Path.Combine(Path.GetFullPath(root), "blobs", shard, id);
		}

		private static Multihash CreateMultihash(byte code, byte[] digest)
		{
			// BigRedProf.Data currently encodes only sha2-256. Teaching its encoder
			// this code lets a test round-trip a real multibase id whose header is
			// not "bciq". The map is append-only and does not change sha2-256.
			FieldInfo? field = typeof(Multihash).GetField(
				"_algoToCode",
				BindingFlags.NonPublic | BindingFlags.Static
			);
			if(field == null)
				throw new InvalidOperationException("Multihash no longer keeps its algorithm map in _algoToCode.");

			Dictionary<MultihashAlgorithm, uint>? algorithms =
				field.GetValue(null) as Dictionary<MultihashAlgorithm, uint>;
			if(algorithms == null)
				throw new InvalidOperationException("Multihash algorithm map is null.");

			algorithms[(MultihashAlgorithm)code] = code;

			ConstructorInfo? constructor = typeof(Multihash).GetConstructor(
				BindingFlags.Instance | BindingFlags.NonPublic,
				null,
				new Type[] { typeof(byte[]), typeof(MultihashAlgorithm) },
				null
			);
			if(constructor == null)
				throw new InvalidOperationException("Multihash no longer has the expected constructor.");

			object created = constructor.Invoke(new object[] { digest, (MultihashAlgorithm)code });
			return (Multihash)created;
		}

		private static string AssertBlobPath(string root, Multihash multihash)
		{
			string id = multihash.ToMultibaseString();
			Assert.StartsWith("bciq", id);
			Assert.Equal(id, id.ToLowerInvariant());

			string path = BlobPath(root, multihash);
			Assert.True(File.Exists(path));
			Assert.Equal(id, Path.GetFileName(path));

			string shard = id.Substring(4, 2);
			Assert.Equal(shard, Path.GetFileName(Path.GetDirectoryName(path)));
			Assert.NotEqual(id.Substring(0, 2), shard);
			return shard;
		}
		#endregion
	}
}
