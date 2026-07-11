using BigRedProf.Data.Core;
using System.Threading.Tasks;

namespace BigRedProf.Content.Core
{
	/// <summary>
	/// Dumb hash-addressed blob storage. Providers own only the mechanics of storing and
	/// fetching bytes by multihash; <see cref="ContentStore"/> owns all the important
	/// invariants (hashing, encoding, verification, cataloging).
	/// </summary>
	public interface IContentStoreStorageProvider
	{
		#region methods
		/// <summary>
		/// Stores a blob. Must be idempotent: putting the same multihash again, with the
		/// same bytes, is a safe no-op.
		/// </summary>
		/// <param name="multihash">The multihash the blob is addressed by.</param>
		/// <param name="blob">The blob.</param>
		public Task PutBlobAsync(Multihash multihash, byte[] blob);

		/// <summary>
		/// Fetches a blob by its multihash.
		/// </summary>
		/// <param name="multihash">The multihash the blob is addressed by.</param>
		/// <returns>The blob, or null if this provider doesn't have it.</returns>
		public Task<byte[]?> TryGetBlobAsync(Multihash multihash);
		#endregion
	}
}
