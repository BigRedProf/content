using BigRedProf.Content.Core;
using BigRedProf.Content.Core.Providers;
using BigRedProf.Content.Test.TestDoubles;
using BigRedProf.Data.Core;

namespace BigRedProf.Content.Test
{
	/// <summary>
	/// Bundles a <see cref="ContentStore"/> with its collaborators so tests can inspect
	/// the storage provider and catalog scribe behind the store.
	/// </summary>
	public class ContentStoreTestHarness
	{
		#region constructors
		public ContentStoreTestHarness()
		{
			PiedPiper = new PiedPiper();
			StorageProvider = new MemoryContentStoreStorageProvider();
			CatalogScribe = new ListScribe();
			ContentStore = new ContentStore(PiedPiper, StorageProvider, CatalogScribe);
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
