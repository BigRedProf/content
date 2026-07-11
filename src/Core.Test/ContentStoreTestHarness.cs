using BigRedProf.Content.Core;
using BigRedProf.Content.Core.Models;
using BigRedProf.Content.Core.PackRats;
using BigRedProf.Content.Core.Providers;
using BigRedProf.Content.Test.TestDoubles;
using BigRedProf.Data.Core;

namespace BigRedProf.Content.Test
{
	/// <summary>
	/// Bundles a <see cref="ContentStore"/> with its collaborators so tests can inspect
	/// the storage provider and catalog scribe behind the store. Also provides a pied
	/// piper prepared the way a catalog-reading consumer would prepare one, for decoding
	/// catalog events.
	/// </summary>
	public class ContentStoreTestHarness
	{
		#region constructors
		public ContentStoreTestHarness()
		{
			PiedPiper piedPiper = new PiedPiper();
			piedPiper.RegisterCorePackRats();
			piedPiper.RegisterPackRat<ContentStored>(
				new ContentStoredPackRat(piedPiper),
				ContentSchemaId.ContentStored
			);
			PiedPiper = piedPiper;

			StorageProvider = new MemoryContentStoreStorageProvider();
			CatalogScribe = new ListScribe();
			ContentStore = new ContentStore(StorageProvider, CatalogScribe);
		}
		#endregion

		#region properties
		public IPiedPiper PiedPiper
		{
			get;
			private set;
		}

		public MemoryContentStoreStorageProvider StorageProvider
		{
			get;
			private set;
		}

		public ListScribe CatalogScribe
		{
			get;
			private set;
		}

		public ContentStore ContentStore
		{
			get;
			private set;
		}
		#endregion
	}
}
