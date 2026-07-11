using BigRedProf.Data.Core;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace BigRedProf.Content.Core.Providers
{
	/// <summary>
	/// An in-memory storage provider, useful for testing and ephemeral scenarios.
	/// </summary>
	public class MemoryContentStoreStorageProvider : IContentStoreStorageProvider
	{
		#region fields
		private readonly ConcurrentDictionary<Multihash, byte[]> _blobs;
		#endregion

		#region constructors
		public MemoryContentStoreStorageProvider()
		{
			_blobs = new ConcurrentDictionary<Multihash, byte[]>();
		}
		#endregion

		#region properties
		/// <summary>
		/// The number of blobs currently stored.
		/// </summary>
		public int BlobCount
		{
			get
			{
				return _blobs.Count;
			}
		}
		#endregion

		#region IContentStoreStorageProvider methods
		/// <inheritdoc/>
		public Task PutBlobAsync(Multihash multihash, byte[] blob)
		{
			if(multihash == null)
				throw new ArgumentNullException(nameof(multihash));

			if(blob == null)
				throw new ArgumentNullException(nameof(blob));

			// Defensive copy so callers can't mutate stored blobs. TryAdd (rather than
			// overwrite) makes duplicate puts no-ops, which is safe because blobs are
			// hash-addressed: same multihash, same bytes.
			byte[] blobCopy = new byte[blob.Length];
			Buffer.BlockCopy(blob, 0, blobCopy, 0, blob.Length);
			_blobs.TryAdd(multihash, blobCopy);

			return Task.CompletedTask;
		}

		/// <inheritdoc/>
		public Task<byte[]?> TryGetBlobAsync(Multihash multihash)
		{
			if(multihash == null)
				throw new ArgumentNullException(nameof(multihash));

			byte[]? result = null;
			if(_blobs.TryGetValue(multihash, out byte[] blob))
			{
				result = new byte[blob.Length];
				Buffer.BlockCopy(blob, 0, result, 0, blob.Length);
			}

			return Task.FromResult<byte[]?>(result);
		}
		#endregion
	}
}
