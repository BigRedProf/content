using BigRedProf.Content.Core.Models;
using BigRedProf.Content.Core.PackRats;
using BigRedProf.Data.Core;
using BigRedProf.Stories;
using System;
using System.Threading.Tasks;

namespace BigRedProf.Content.Core
{
	/// <summary>
	/// The standard content store, composed around a storage provider and a catalog scribe.
	/// Owns the invariants every content store must uphold: content identity via
	/// <see cref="Multihash.FromCode"/>, encoding to and from bytes, verification on read,
	/// idempotent puts, and cataloging every successful put in a story.
	/// </summary>
	public class ContentStore : IContentStore
	{
		#region static fields
		private static readonly IPiedPiper _piedPiper;
		#endregion

		#region fields
		private readonly IContentStoreStorageProvider _storageProvider;
		private readonly IScribe _catalogScribe;
		private readonly MultihashAlgorithm _algorithm;
		#endregion

		#region class constructors
		static ContentStore()
		{
			// The API boundary is Code, not model, so no caller models ever cross into a
			// content store. That lets us manage our own pied piper internally rather than
			// requiring callers to prepare one.
			_piedPiper = new PiedPiper();
			_piedPiper.RegisterCorePackRats();
			_piedPiper.RegisterPackRat<ContentStored>(
				new ContentStoredPackRat(_piedPiper),
				ContentSchemaId.ContentStored
			);
		}
		#endregion

		#region constructors
		/// <summary>
		/// Creates a <see cref="ContentStore"/> using the default multihash algorithm.
		/// </summary>
		/// <param name="storageProvider">The storage provider that stores the actual blobs.</param>
		/// <param name="catalogScribe">
		/// The scribe, already bound to this store's catalog story, used to record
		/// <see cref="ContentStored"/> events.
		/// </param>
		public ContentStore(IContentStoreStorageProvider storageProvider, IScribe catalogScribe)
			: this(storageProvider, catalogScribe, DefaultAlgorithm)
		{
		}

		/// <summary>
		/// Creates a <see cref="ContentStore"/> using a specific multihash algorithm.
		/// </summary>
		/// <param name="storageProvider">The storage provider that stores the actual blobs.</param>
		/// <param name="catalogScribe">
		/// The scribe, already bound to this store's catalog story, used to record
		/// <see cref="ContentStored"/> events.
		/// </param>
		/// <param name="algorithm">The multihash algorithm to use for new content.</param>
		public ContentStore(
			IContentStoreStorageProvider storageProvider,
			IScribe catalogScribe,
			MultihashAlgorithm algorithm
		)
		{
			if(storageProvider == null)
				throw new ArgumentNullException(nameof(storageProvider));

			if(catalogScribe == null)
				throw new ArgumentNullException(nameof(catalogScribe));

			_storageProvider = storageProvider;
			_catalogScribe = catalogScribe;
			_algorithm = algorithm;
		}
		#endregion

		#region IContentStore methods
		/// <inheritdoc/>
		public async Task<Multihash> PutContentAsync(Code content)
		{
			if(content == null)
				throw new ArgumentNullException(nameof(content));

			Multihash multihash = Multihash.FromCode(content, _algorithm);
			byte[] blob = _piedPiper.SaveCodeToByteArray(content);

			// INVARIANT: Store the blob first, then append the catalog event, and only return
			// the multihash after both succeed. A failure between the two leaves an orphan
			// blob (collectible garbage), never a cataloged-but-missing one. And because the
			// caller only learns the multihash after the catalog append succeeds, no external
			// reference can ever point at content that export/restore wouldn't recover.
			// Reordering these two awaits, or returning the multihash early, silently breaks
			// restore.
			await _storageProvider.PutBlobAsync(multihash, blob).ConfigureAwait(false);

			ModelWithSchema modelWithSchema = new ModelWithSchema()
			{
				SchemaId = ContentSchemaId.ContentStored,
				Model = new ContentStored()
				{
					Multihash = multihash
				}
			};
			Code catalogThing = _piedPiper.EncodeModel<ModelWithSchema>(modelWithSchema, CoreSchema.ModelWithSchema);
			await _catalogScribe.RecordSomethingAsync(catalogThing).ConfigureAwait(false);

			return multihash;
		}
		#endregion

		#region IContentSource methods
		/// <inheritdoc/>
		/// <exception cref="ContentIntegrityException">
		/// Thrown if the fetched content fails hash verification.
		/// </exception>
		public async Task<Code?> TryGetContentAsync(Multihash multihash)
		{
			if(multihash == null)
				throw new ArgumentNullException(nameof(multihash));

			byte[]? blob = await _storageProvider.TryGetBlobAsync(multihash).ConfigureAwait(false);
			if(blob == null)
				return null;

			Code content = _piedPiper.LoadCodeFromByteArray(blob);

			// Verify on read, using the algorithm of the requested multihash so stores can
			// serve content written under older algorithms after a future algorithm upgrade.
			Multihash actualMultihash = Multihash.FromCode(content, multihash.Algorithm);
			if(actualMultihash != multihash)
				throw new ContentIntegrityException(multihash, actualMultihash);

			return content;
		}
		#endregion

		#region constants
		/// <summary>
		/// The default multihash algorithm for new content.
		/// </summary>
		public const MultihashAlgorithm DefaultAlgorithm = MultihashAlgorithm.Sha256;
		#endregion
	}
}
