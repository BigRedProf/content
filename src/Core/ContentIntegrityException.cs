using BigRedProf.Data.Core;
using System;

namespace BigRedProf.Content.Core
{
	/// <summary>
	/// Thrown when content fetched from storage fails hash verification. This indicates
	/// corruption (or tampering) in the underlying storage provider.
	/// </summary>
	public class ContentIntegrityException : Exception
	{
		#region constructors
		public ContentIntegrityException(Multihash expectedMultihash, Multihash actualMultihash)
			: base(
				$"Content integrity check failed. Expected multihash '{expectedMultihash}' " +
				$"but computed '{actualMultihash}'."
			)
		{
			if(expectedMultihash == null)
				throw new ArgumentNullException(nameof(expectedMultihash));

			if(actualMultihash == null)
				throw new ArgumentNullException(nameof(actualMultihash));

			ExpectedMultihash = expectedMultihash;
			ActualMultihash = actualMultihash;
		}
		#endregion

		#region properties
		/// <summary>
		/// The multihash the content was requested by.
		/// </summary>
		public Multihash ExpectedMultihash
		{
			get;
			private set;
		}

		/// <summary>
		/// The multihash actually computed from the fetched content.
		/// </summary>
		public Multihash ActualMultihash
		{
			get;
			private set;
		}
		#endregion
	}
}
