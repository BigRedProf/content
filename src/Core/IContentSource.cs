using BigRedProf.Data.Core;
using System.Threading.Tasks;

namespace BigRedProf.Content.Core
{
	/// <summary>
	/// The read side of content-addressable storage. An <see cref="IContentSource"/> is any
	/// place content can be fetched from by its <see cref="Multihash"/>: a full-fledged
	/// <see cref="IContentStore"/>, a client-side cache, or a read-through chain of sources.
	/// </summary>
	/// <remarks>
	/// Consumers of content (goods, activities, projections) should depend on
	/// <see cref="IContentSource"/> rather than <see cref="IContentStore"/> so they can be
	/// composed with caches and other lightweight sources that don't carry the durability
	/// obligations of a true content store.
	/// </remarks>
	public interface IContentSource
	{
		#region methods
		/// <summary>
		/// Fetches content by its multihash.
		/// </summary>
		/// <param name="multihash">The multihash identifying the content.</param>
		/// <returns>The content, or null if this source doesn't have it.</returns>
		public Task<Code?> TryGetContentAsync(Multihash multihash);
		#endregion
	}
}
