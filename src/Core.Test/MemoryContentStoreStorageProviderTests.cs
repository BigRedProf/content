using BigRedProf.Content.Core.Providers;
using BigRedProf.Data.Core;
using System;
using System.Threading.Tasks;
using Xunit;

namespace BigRedProf.Content.Test
{
	public class MemoryContentStoreStorageProviderTests
	{
		#region PutBlobAsync tests
		[Fact]
		public async Task PutBlobShouldRoundTrip()
		{
			MemoryContentStoreStorageProvider provider = new MemoryContentStoreStorageProvider();
			byte[] blob = new byte[] { 0x42, 0x49, 0x47 };
			Multihash multihash = Multihash.FromBytes(blob, MultihashAlgorithm.Sha256);

			await provider.PutBlobAsync(multihash, blob);
			byte[]? fetchedBlob = await provider.TryGetBlobAsync(multihash);

			Assert.NotNull(fetchedBlob);
			Assert.Equal(blob, fetchedBlob);
		}

		[Fact]
		public async Task PutBlobShouldBeIdempotent()
		{
			MemoryContentStoreStorageProvider provider = new MemoryContentStoreStorageProvider();
			byte[] blob = new byte[] { 0x42, 0x49, 0x47 };
			Multihash multihash = Multihash.FromBytes(blob, MultihashAlgorithm.Sha256);

			await provider.PutBlobAsync(multihash, blob);
			await provider.PutBlobAsync(multihash, blob);

			Assert.Equal(1, provider.BlobCount);
		}

		[Fact]
		public async Task PutBlobShouldDefensivelyCopyTheBlob()
		{
			MemoryContentStoreStorageProvider provider = new MemoryContentStoreStorageProvider();
			byte[] blob = new byte[] { 0x42, 0x49, 0x47 };
			Multihash multihash = Multihash.FromBytes(blob, MultihashAlgorithm.Sha256);

			await provider.PutBlobAsync(multihash, blob);
			blob[0] = 0xFF;

			byte[]? fetchedBlob = await provider.TryGetBlobAsync(multihash);
			Assert.NotNull(fetchedBlob);
			Assert.Equal(new byte[] { 0x42, 0x49, 0x47 }, fetchedBlob);
		}
		#endregion

		#region TryGetBlobAsync tests
		[Fact]
		public async Task TryGetBlobShouldReturnNullWhenBlobNotFound()
		{
			MemoryContentStoreStorageProvider provider = new MemoryContentStoreStorageProvider();
			Multihash multihash = Multihash.FromBytes(new byte[] { 0x01 }, MultihashAlgorithm.Sha256);

			byte[]? fetchedBlob = await provider.TryGetBlobAsync(multihash);

			Assert.Null(fetchedBlob);
		}

		[Fact]
		public async Task TryGetBlobShouldDefensivelyCopyTheBlob()
		{
			MemoryContentStoreStorageProvider provider = new MemoryContentStoreStorageProvider();
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
		#endregion
	}
}
