using BigRedProf.Content.Core;
using BigRedProf.Content.Core.Models;
using BigRedProf.Content.Core.Providers;
using BigRedProf.Content.Test.TestDoubles;
using BigRedProf.Data.Core;
using System;
using System.Threading.Tasks;
using Xunit;

namespace BigRedProf.Content.Test
{
	public class ContentStoreTests
	{
		#region PutContentAsync tests
		[Fact]
		public async Task PutContentShouldReturnTheMultihashOfTheContent()
		{
			ContentStoreTestHarness harness = new ContentStoreTestHarness();
			Code content = new Code(new byte[] { 0x42, 0x49, 0x47, 0x52, 0x45, 0x44 });

			Multihash multihash = await harness.ContentStore.PutContentAsync(content);

			Assert.Equal(Multihash.FromCode(content, MultihashAlgorithm.Sha256), multihash);

			// For byte-aligned content, the multihash is also the hash of the raw bytes.
			Assert.Equal(Multihash.FromBytes(content.ToByteArray(), MultihashAlgorithm.Sha256), multihash);
		}

		[Fact]
		public async Task PutContentShouldRoundTripByteAlignedContent()
		{
			ContentStoreTestHarness harness = new ContentStoreTestHarness();
			Code content = new Code(new byte[] { 0x42, 0x49, 0x47, 0x52, 0x45, 0x44 });

			Multihash multihash = await harness.ContentStore.PutContentAsync(content);
			Code? fetchedContent = await harness.ContentStore.TryGetContentAsync(multihash);

			Assert.NotNull(fetchedContent);
			Assert.Equal(content, fetchedContent);
		}

		[Fact]
		public async Task PutContentShouldRoundTripNonByteAlignedContent()
		{
			ContentStoreTestHarness harness = new ContentStoreTestHarness();
			Code content = new Code("10110");

			Multihash multihash = await harness.ContentStore.PutContentAsync(content);
			Code? fetchedContent = await harness.ContentStore.TryGetContentAsync(multihash);

			Assert.NotNull(fetchedContent);
			Assert.Equal(content, fetchedContent);
		}

		[Fact]
		public async Task PutContentShouldAppendContentStoredEventToCatalog()
		{
			ContentStoreTestHarness harness = new ContentStoreTestHarness();
			Code content = new Code("11010010");

			Multihash multihash = await harness.ContentStore.PutContentAsync(content);

			Assert.Single(harness.CatalogScribe.Things);

			ModelWithSchema modelWithSchema = harness.PiedPiper.DecodeModel<ModelWithSchema>(
				harness.CatalogScribe.Things[0],
				CoreSchema.ModelWithSchema
			);
			Assert.Equal(new AttributeFriendlyGuid(ContentSchemaId.ContentStored), modelWithSchema.SchemaId);

			ContentStored contentStored = (ContentStored) modelWithSchema.Model;
			Assert.Equal(multihash, contentStored.Multihash);
		}

		[Fact]
		public async Task PutContentShouldBeIdempotentButAllowDuplicateCatalogEvents()
		{
			ContentStoreTestHarness harness = new ContentStoreTestHarness();
			Code content = new Code("11010010");

			Multihash firstMultihash = await harness.ContentStore.PutContentAsync(content);
			Multihash secondMultihash = await harness.ContentStore.PutContentAsync(content);

			Assert.Equal(firstMultihash, secondMultihash);

			// The storage layer deduplicates by multihash.
			Assert.Equal(1, harness.StorageProvider.BlobCount);

			// Duplicate catalog events are allowed by design; projections deduplicate at
			// replay time.
			Assert.Equal(2, harness.CatalogScribe.Things.Count);
		}

		[Fact]
		public async Task PutContentShouldNotCatalogWhenStorageFails()
		{
			IPiedPiper piedPiper = new PiedPiper();
			ListScribe catalogScribe = new ListScribe();
			ContentStore contentStore = new ContentStore(piedPiper, new ThrowingStorageProvider(), catalogScribe);
			Code content = new Code("11010010");

			await Assert.ThrowsAsync<InvalidOperationException>(
				async () =>
				{
					await contentStore.PutContentAsync(content);
				}
			);

			Assert.Empty(catalogScribe.Things);
		}

		[Fact]
		public async Task PutContentShouldLeaveOnlyAnOrphanBlobWhenCatalogingFails()
		{
			IPiedPiper piedPiper = new PiedPiper();
			MemoryContentStoreStorageProvider storageProvider = new MemoryContentStoreStorageProvider();
			ContentStore contentStore = new ContentStore(piedPiper, storageProvider, new ThrowingScribe());
			Code content = new Code("11010010");

			await Assert.ThrowsAsync<InvalidOperationException>(
				async () =>
				{
					await contentStore.PutContentAsync(content);
				}
			);

			// The blob was stored before cataloging failed. That's the correct failure mode:
			// an orphan blob is collectible garbage, whereas a cataloged-but-missing blob
			// would break restore. And since the caller never received the multihash, no
			// external reference can point at this content.
			Assert.Equal(1, storageProvider.BlobCount);
		}

		[Fact]
		public async Task PutContentShouldThrowWhenContentIsNull()
		{
			ContentStoreTestHarness harness = new ContentStoreTestHarness();

			await Assert.ThrowsAsync<ArgumentNullException>(
				async () =>
				{
					await harness.ContentStore.PutContentAsync(null!);
				}
			);
		}
		#endregion

		#region TryGetContentAsync tests
		[Fact]
		public async Task TryGetContentShouldReturnNullWhenContentNotFound()
		{
			ContentStoreTestHarness harness = new ContentStoreTestHarness();
			Multihash multihash = Multihash.FromBytes(new byte[] { 0x01, 0x02, 0x03 }, MultihashAlgorithm.Sha256);

			Code? fetchedContent = await harness.ContentStore.TryGetContentAsync(multihash);

			Assert.Null(fetchedContent);
		}

		[Fact]
		public async Task TryGetContentShouldThrowWhenContentIsCorrupted()
		{
			IPiedPiper piedPiper = new PiedPiper();
			ContentStore contentStore = new ContentStore(piedPiper, new CorruptingStorageProvider(), new ListScribe());
			Code content = new Code(new byte[] { 0x42, 0x49, 0x47, 0x52, 0x45, 0x44 });

			Multihash multihash = await contentStore.PutContentAsync(content);

			ContentIntegrityException exception = await Assert.ThrowsAsync<ContentIntegrityException>(
				async () =>
				{
					await contentStore.TryGetContentAsync(multihash);
				}
			);

			Assert.Equal(multihash, exception.ExpectedMultihash);
			Assert.NotEqual(multihash, exception.ActualMultihash);
		}

		[Fact]
		public async Task TryGetContentShouldThrowWhenMultihashIsNull()
		{
			ContentStoreTestHarness harness = new ContentStoreTestHarness();

			await Assert.ThrowsAsync<ArgumentNullException>(
				async () =>
				{
					await harness.ContentStore.TryGetContentAsync(null!);
				}
			);
		}
		#endregion

		#region constructor tests
		[Fact]
		public void ConstructorShouldThrowWhenArgumentsAreNull()
		{
			IPiedPiper piedPiper = new PiedPiper();
			MemoryContentStoreStorageProvider storageProvider = new MemoryContentStoreStorageProvider();
			ListScribe catalogScribe = new ListScribe();

			Assert.Throws<ArgumentNullException>(
				() =>
				{
					new ContentStore(null!, storageProvider, catalogScribe);
				}
			);

			Assert.Throws<ArgumentNullException>(
				() =>
				{
					new ContentStore(piedPiper, null!, catalogScribe);
				}
			);

			Assert.Throws<ArgumentNullException>(
				() =>
				{
					new ContentStore(piedPiper, storageProvider, null!);
				}
			);
		}

		[Fact]
		public void ConstructorShouldTolerateAPiedPiperWithPackRatsAlreadyRegistered()
		{
			IPiedPiper piedPiper = new PiedPiper();
			piedPiper.RegisterCorePackRats();
			MemoryContentStoreStorageProvider storageProvider = new MemoryContentStoreStorageProvider();

			new ContentStore(piedPiper, storageProvider, new ListScribe());
			new ContentStore(piedPiper, storageProvider, new ListScribe());
		}
		#endregion
	}
}
