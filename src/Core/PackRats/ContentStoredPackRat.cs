using BigRedProf.Content.Core.Models;
using BigRedProf.Data.Core;
using System;

namespace BigRedProf.Content.Core.PackRats
{
	/// <summary>
	/// Packs and unpacks <see cref="ContentStored"/> catalog events.
	/// </summary>
	/// <remarks>
	/// Hand-written for now. If the catalog event family grows, consider migrating the models
	/// to a Core.Models project compiled with the pack rat compiler (prc), as BigRedProf.Stories
	/// does.
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
		public override void PackModel(CodeWriter writer, ContentStored model)
		{
			if(writer == null)
				throw new ArgumentNullException(nameof(writer));

			if(model == null)
				throw new ArgumentNullException(nameof(model));

			if(model.Multihash == null)
				throw new ArgumentException("The Multihash property cannot be null.", nameof(model));

			PiedPiper.PackModel<Multihash>(writer, model.Multihash, CoreSchema.MultihashSchema);
		}

		public override ContentStored UnpackModel(CodeReader reader)
		{
			if(reader == null)
				throw new ArgumentNullException(nameof(reader));

			Multihash multihash = PiedPiper.UnpackModel<Multihash>(reader, CoreSchema.MultihashSchema);

			ContentStored model = new ContentStored()
			{
				Multihash = multihash
			};
			return model;
		}
		#endregion
	}
}
