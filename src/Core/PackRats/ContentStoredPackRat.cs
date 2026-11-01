using BigRedProf.Content.Core.Data;
using BigRedProf.Data.Core;
using System;

namespace BigRedProf.Content.Core.PackRats
{
	/// <summary>
	/// Packs and unpacks <see cref="ContentStored"/> catalog events.
	/// </summary>
	/// <remarks>
	/// Hand-written for now. If the catalog event family grows, consider migrating these
	/// declarations to a Core.Data project compiled with the pack rat compiler (prc), as
	/// BigRedProf.Stories does.
	/// </remarks>
	public class ContentStoredPackRat : PackRat<ContentStored>
	{
		#region constructors
		public ContentStoredPackRat(IPiedPiper piedPiper)
			: base(piedPiper)
		{
		}
		#endregion

		#region PackRat methods
		public override void PackModel(BitStream stream, ContentStored model)
		{
			if(stream == null)
				throw new ArgumentNullException(nameof(stream));

			if(model == null)
				throw new ArgumentNullException(nameof(model));

			if(model.Multihash == null)
				throw new ArgumentException("The Multihash property cannot be null.", nameof(model));

			PiedPiper.PackModel<Multihash>(stream, model.Multihash, CoreSchema.MultihashSchema);
		}

		public override ContentStored UnpackModel(BitStream stream)
		{
			if(stream == null)
				throw new ArgumentNullException(nameof(stream));

			Multihash multihash = PiedPiper.UnpackModel<Multihash>(stream, CoreSchema.MultihashSchema);

			ContentStored model = new ContentStored()
			{
				Multihash = multihash
			};
			return model;
		}
		#endregion
	}
}
