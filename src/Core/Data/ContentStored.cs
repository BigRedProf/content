using BigRedProf.Data.Core;

namespace BigRedProf.Content.Core.Data
{
	/// <summary>
	/// A catalog event recording that a successful put was performed for a given multihash.
	/// </summary>
	/// <remarks>
	/// Duplicate <see cref="ContentStored"/> events for the same multihash are allowed by
	/// design. They preserve clean failure/retry behavior at put time; catalog projections
	/// deduplicate by multihash at replay time.
	/// </remarks>
	public class ContentStored
	{
		#region properties
		/// <summary>
		/// The multihash of the stored content.
		/// </summary>
		public Multihash Multihash
		{
			get;
			set;
		} = default!;
		#endregion
	}
}
