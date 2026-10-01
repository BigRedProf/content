using BigRedProf.Data.Core;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BigRedProf.Content.Core.Providers
{
	/// <summary>
	/// A durable storage provider that stores each blob as a file under a root directory.
	/// </summary>
	/// <remarks>
	/// <para>
	/// On-disk format version 1. Each blob is a file at
	/// <c>&lt;root&gt;/blobs/&lt;hex2&gt;/&lt;id&gt;</c>. <c>id</c> is the canonical
	/// string from <see cref="Multihash.ToMultibaseString()"/>. <c>hex2</c> is the
	/// first two lowercase hex characters of SHA-256 over the UTF-8 bytes of that
	/// whole id, which is 256 folders and an even spread for any algorithm. The
	/// shard comes only from that public string, so a change in how a multihash
	/// header is encoded cannot move an existing blob. The folder name is
	/// lowercase, so the path is safe on volumes that ignore case. This layout is
	/// durable user data: changing it would break existing stores, and blobs
	/// already written would look missing.
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

		#region internal functions
		/// <summary>
		/// Returns the on-disk format version 1 folder for <paramref name="id"/>.
		/// </summary>
		/// <param name="id">The canonical <see cref="Multihash.ToMultibaseString()"/>.</param>
		/// <returns>
		/// The first two lowercase hex characters of SHA-256 over the UTF-8 bytes of
		/// <paramref name="id"/>.
		/// </returns>
		internal static string GetShard(string id)
		{
			if(id == null)
				throw new ArgumentNullException(nameof(id));

			if(
				id.IndexOf('/') >= 0
				|| id.IndexOf('\\') >= 0
				|| !string.Equals(id, id.ToLowerInvariant(), StringComparison.Ordinal)
			)
			{
				throw new InvalidOperationException("Multihash multibase form cannot be used as a blob path.");
			}

			// Hash the public id only. Rebuilding the multihash wire header here would
			// tie this durable path to encoding details that are not part of Data's
			// public contract.
			byte[] hash;
			using(SHA256 sha256 = SHA256.Create())
			{
				hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(id));
			}

			return hash[0].ToString("x2");
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
		#endregion

		#region private methods
		private string GetBlobPath(Multihash multihash)
		{
			string id = multihash.ToMultibaseString();
			string shard = GetShard(id);
			return Path.Combine(_rootDirectory, BlobDirectoryName, shard, id);
		}
		#endregion

		#region constants
		private const int BufferSize = 4096;
		private const string BlobDirectoryName = "blobs";
		private const string TempFilePrefix = "tmp-";
		#endregion
	}
}
