using BigRedProf.Content.Core;
using BigRedProf.Content.Core.Providers;
using BigRedProf.Data.Core;
using System;
using System.Threading.Tasks;

namespace BigRedProf.Content.Test.TestDoubles
{
	/// <summary>
	/// A test storage provider that silently corrupts every blob it returns, simulating
	/// storage corruption.
	/// </summary>
	public class CorruptingStorageProvider : IContentStoreStorageProvider
	{
		#region fields
		private readonly MemoryContentStoreStorageProvider _innerProvider;
		#endregion

		#region constructors
		public CorruptingStorageProvider()
		{
			_innerProvider = new MemoryContentStoreStorageProvider();
		}
		#endregion

		#region IContentStoreStorageProvider methods
		public Task PutBlobAsync(Multihash multihash, byte[] blob)
		{
			return _innerProvider.PutBlobAsync(multihash, blob);
		}

		public async Task<byte[]?> TryGetBlobAsync(Multihash multihash)
		{
			byte[]? blob = await _innerProvider.TryGetBlobAsync(multihash);
			if(blob == null)
				return null;

			// Flip a bit in the last byte. The last byte of a saved code is content (not
			// length prefix), so this corrupts the content itself.
			blob[blob.Length - 1] ^= 0x01;
			return blob;
		}
		#endregion
	}
}
