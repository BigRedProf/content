using BigRedProf.Data.Core;
using System.Threading.Tasks;

namespace BigRedProf.Content.Core
{
	/// <summary>
	/// A content store stores <see cref="Code"/>s by <see cref="Multihash"/> and records its
	/// durable history in a story. Content is immutable: a multihash always identifies the
	/// same bits, forever.
	/// </summary>
	/// <remarks>
	/// Every content store catalogs its writes in a story. This isn't an optional feature;
	/// it's what makes export, restore, and tape backup invariants of every store rather
	/// than capabilities of some stores.
	/// </remarks>
	public interface IContentStore : IContentSource
	{
		#region methods
		/// <summary>
		/// Stores content and catalogs it. Putting the same content twice is safe and returns
		/// the same multihash; duplicate catalog events are allowed by design and deduplicated
		/// at replay time.
		/// </summary>
		/// <param name="content">The content to store.</param>
		/// <returns>The multihash identifying the content.</returns>
		public Task<Multihash> PutContentAsync(Code content);
		#endregion
	}
}
