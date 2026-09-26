using BigRedProf.Data.Core;
using System;
using System.IO;
using System.Threading.Tasks;

namespace BigRedProf.Content.Core.Providers
{
	/// <summary>
	/// A durable storage provider that stores each blob as a file under a root directory.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Blobs are addressed at <c>blobs/&lt;shard&gt;/&lt;id&gt;</c> under the root.
	/// <c>id</c> is <see cref="Multihash.ToMultibaseString()"/>, multibase base32
	/// lowercase. The first characters of that id are the multibase prefix and the
	/// multihash header, which do not depend on the digest, so <c>shard</c> starts
	/// after them. For sha2-256 the header is 16 bits and the shard is still the
	/// two characters at offset 4 (every such id begins <c>bciq</c>, and the first
	/// two characters would otherwise share one directory). Other algorithms keep
	/// the same rule with their own header size. The characters are lowercase, so
	/// the path is safe on volumes that ignore case.
	/// </para>
	/// <para>
	/// A put writes a temporary file in the shard directory, flushes it, and renames
	/// it onto the final path. The final path appears only after the bytes are
	/// complete, so a crash cannot leave a half-written blob there. A second put of
	/// the same multihash, including one racing the first, leaves that blob in place.
	/// This provider does not hash the bytes. <see cref="ContentStore"/> does that
	/// on read.
	/// </para>
	/// </remarks>
	public class DiskContentStoreStorageProvider : IContentStoreStorageProvider
	{
		#region fields
		private readonly string _rootDirectory;
		#endregion

		#region constructors
		/// <summary>
		/// Creates a provider that stores blobs under <paramref name="rootDirectory"/>.
		/// </summary>
		/// <param name="rootDirectory">
		/// The store root. Created on the first put if it does not already exist.
		/// Must not name an existing file.
		/// </param>
		public DiskContentStoreStorageProvider(string rootDirectory)
		{
			if(rootDirectory == null)
				throw new ArgumentNullException(nameof(rootDirectory));

			if(string.IsNullOrWhiteSpace(rootDirectory))
				throw new ArgumentException("Value cannot be null or whitespace.", nameof(rootDirectory));

			// Resolve once, so a later change of working directory cannot retarget the store.
			_rootDirectory = Path.GetFullPath(rootDirectory);

			if(File.Exists(_rootDirectory))
				throw new ArgumentException("Root directory names an existing file.", nameof(rootDirectory));
		}
		#endregion

		#region IContentStoreStorageProvider methods
		/// <inheritdoc/>
		public async Task PutBlobAsync(Multihash multihash, byte[] blob)
		{
			if(multihash == null)
				throw new ArgumentNullException(nameof(multihash));

			if(blob == null)
				throw new ArgumentNullException(nameof(blob));

			// Copy before the first await. Callers may reuse or mutate their buffer as
			// soon as this method yields, and the bytes that land on disk must be the
			// ones passed in.
			byte[] blobCopy = new byte[blob.Length];
			Buffer.BlockCopy(blob, 0, blobCopy, 0, blob.Length);

			string finalPath = GetBlobPath(multihash);

			// Same multihash, same bytes. The first complete file wins, which is also
			// what keeps a racing put from replacing a blob that is already durable.
			if(File.Exists(finalPath))
				return;

			string? directory = Path.GetDirectoryName(finalPath);
			if(string.IsNullOrEmpty(directory))
				throw new InvalidOperationException("Blob path has no directory.");

			Directory.CreateDirectory(directory);

			if(File.Exists(finalPath))
				return;

			string tempPath = Path.Combine(directory, TempFilePrefix + Guid.NewGuid().ToString("N"));
			try
			{
				await WriteTempFileAsync(tempPath, blobCopy).ConfigureAwait(false);
				CommitTempFile(tempPath, finalPath);
			}
			finally
			{
				// The rename consumes the temp on success. On failure, or when another
				// put won the race, the temp must not be left behind as a second blob.
				DeleteTempFile(tempPath);
			}
		}

		/// <inheritdoc/>
		public async Task<byte[]?> TryGetBlobAsync(Multihash multihash)
		{
			if(multihash == null)
				throw new ArgumentNullException(nameof(multihash));

			string finalPath = GetBlobPath(multihash);
			byte[]? result = null;

			try
			{
				// Only the final path is ever opened. A temp file from a crashed put
				// is a different name, so it cannot be returned as the blob.
				using(FileStream stream = new FileStream(
					finalPath,
					FileMode.Open,
					FileAccess.Read,
					FileShare.Read,
					BufferSize,
					FileOptions.Asynchronous
				))
				{
					result = await ReadEntireBlobAsync(stream).ConfigureAwait(false);
				}
			}
			catch(FileNotFoundException)
			{
				result = null;
			}
			catch(DirectoryNotFoundException)
			{
				result = null;
			}

			return result;
		}
		#endregion

		#region private functions
		private static async Task WriteTempFileAsync(string tempPath, byte[] blob)
		{
			using(FileStream stream = new FileStream(
				tempPath,
				FileMode.CreateNew,
				FileAccess.Write,
				FileShare.None,
				BufferSize,
				FileOptions.Asynchronous | FileOptions.WriteThrough
			))
			{
				await stream.WriteAsync(blob, 0, blob.Length).ConfigureAwait(false);

				// Flush(bool) is synchronous on purpose: there is no async overload
				// that flushes to stable storage, and the rename below must not publish
				// a final path whose bytes are still only in a write-back cache.
				stream.Flush(true);
			}
		}

		private static void CommitTempFile(string tempPath, string finalPath)
		{
			try
			{
				// Rename within the shard directory, so it stays on one volume and is
				// atomic. Where rename refuses to replace, an existing final path means
				// the other put already committed a complete blob.
				File.Move(tempPath, finalPath);
			}
			catch(IOException) when(File.Exists(finalPath))
			{
				// The other put committed first. The final path is only created by
				// renaming a finished temp file, so the blob already there is complete.
			}
		}

		private static void DeleteTempFile(string tempPath)
		{
			try
			{
				if(File.Exists(tempPath))
					File.Delete(tempPath);
			}
			catch(IOException)
			{
				// Best effort. A leftover temp is not the final path, so a later get
				// will not return it. Losing the original put exception here would be worse.
			}
			catch(UnauthorizedAccessException)
			{
				// Same as above: a leftover temp is not returned as a blob.
			}
		}

		private static async Task<byte[]> ReadEntireBlobAsync(FileStream stream)
		{
			if(stream.Length > int.MaxValue)
				throw new IOException("Blob is larger than the content store can read.");

			int length = (int)stream.Length;
			byte[] buffer = new byte[length];
			int offset = 0;
			while(offset < buffer.Length)
			{
				int read = await stream.ReadAsync(buffer, offset, buffer.Length - offset).ConfigureAwait(false);
				if(read == 0)
					break;

				offset += read;
			}

			// A short read would be a partial blob. The final file is immutable once
			// published, so this means it changed under us; do not return what we have.
			if(offset != buffer.Length)
				throw new IOException("Blob changed while it was being read.");

			return buffer;
		}

		private static string GetShard(Multihash multihash, string id)
		{
			if(
				id.IndexOf('/') >= 0
				|| id.IndexOf('\\') >= 0
				|| !string.Equals(id, id.ToLowerInvariant(), StringComparison.Ordinal)
			)
			{
				throw new InvalidOperationException("Multihash multibase form cannot be used as a blob path.");
			}

			// 'b' is the multibase prefix for the default base32 encoding. The shard
			// is the first two characters that are not fixed by that prefix plus the
			// multihash header (algorithm code and digest length). sha2-256's header
			// is 16 bits, so this offset is 4: the layout already written for it.
			int offset = GetBase32ShardOffset(multihash);
			if(id.Length > 0 && id[0] == 'b' && id.Length >= offset + ShardLength)
				return id.Substring(offset, ShardLength);

			// Some other encoding, or an id too short to skip its header. A stable
			// hash still yields a two-character lowercase folder. string.GetHashCode
			// is not stable across processes, so it cannot address a blob.
			return ShardFromId(id);
		}

		private static int GetBase32ShardOffset(Multihash multihash)
		{
			uint code = (uint)multihash.Algorithm;
			uint digestLength = (uint)multihash.DigestLength;
			int headerBytes = VarIntSize(code) + VarIntSize(digestLength);
			int headerBits = headerBytes * 8;

			// One character for the multibase prefix, then one base32 character per
			// 5 header bits. Leftover header bits spill into the shard, which is
			// what makes sha2-256's first shard character vary.
			return 1 + (headerBits / 5);
		}

		private static int VarIntSize(uint value)
		{
			int size = 1;
			while(value >= 0x80)
			{
				value >>= 7;
				size++;
			}

			return size;
		}

		private static string ShardFromId(string id)
		{
			// FNV-1a, 32-bit. The low byte is two lowercase hex digits.
			uint hash = 2166136261;
			for(int i = 0; i < id.Length; i++)
			{
				hash ^= id[i];
				hash *= 16777619;
			}

			return (hash & 0xFF).ToString("x2");
		}
		#endregion

		#region private methods
		private string GetBlobPath(Multihash multihash)
		{
			string id = multihash.ToMultibaseString();
			string shard = GetShard(multihash, id);
			return Path.Combine(_rootDirectory, BlobDirectoryName, shard, id);
		}
		#endregion

		#region constants
		private const int BufferSize = 4096;
		private const int ShardLength = 2;
		private const string BlobDirectoryName = "blobs";
		private const string TempFilePrefix = "tmp-";
		#endregion
	}
}
