using BigRedProf.Content.Core.Models;
using BigRedProf.Content.Core.PackRats;
using BigRedProf.Data.Core;
using System;

namespace BigRedProf.Content.Core
{
	/// <summary>
	/// Registers the pack rats needed to work with BigRedProf.Content models. Call
	/// <see cref="RegisterContentPackRats"/> from consumers that need to decode catalog
	/// events without constructing a <see cref="ContentStore"/>, such as catalog
	/// projections and inspection tools.
	/// </summary>
	public static class ContentRegistrar
	{
		#region functions
		public static void RegisterContentPackRats(IPiedPiper piedPiper)
		{
			if(piedPiper == null)
				throw new ArgumentNullException(nameof(piedPiper));

			if(!piedPiper.IsPackRatRegistered(CoreSchema.Code))
				piedPiper.RegisterCorePackRats();

			if(!piedPiper.IsPackRatRegistered(ContentSchemaId.ContentStored))
				piedPiper.RegisterPackRat<ContentStored>(new ContentStoredPackRat(piedPiper), ContentSchemaId.ContentStored);
		}
		#endregion
	}
}
