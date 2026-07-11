using BigRedProf.Content.Core;
using BigRedProf.Data.Core;
using System;
using System.Threading.Tasks;

namespace BigRedProf.Content.Test.TestDoubles
{
	/// <summary>
	/// A test storage provider that always fails, simulating a storage outage.
	/// </summary>
	public class ThrowingStorageProvider : IContentStoreStorageProvider
	{
		#region IContentStoreStorageProvider methods
		public Task PutBlobAsync(Multihash multihash, byte[] blob)
		{
			throw new InvalidOperationException("This storage provider always fails.");
		}

		public Task<byte[]?> TryGetBlobAsync(Multihash multihash)
		{
			throw new InvalidOperationException("This storage provider always fails.");
		}
		#endregion
	}
}
