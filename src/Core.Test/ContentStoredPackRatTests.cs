using BigRedProf.Content.Core.Data;
using BigRedProf.Content.Core.PackRats;
using BigRedProf.Data.Core;
using Xunit;

namespace BigRedProf.Content.Test
{
	public class ContentStoredPackRatTests
	{
		#region pack rat tests
		[Fact]
		public void ContentStoredShouldRoundTrip()
		{
			IPiedPiper piedPiper = new PiedPiper();
			piedPiper.RegisterCorePackRats();
			piedPiper.RegisterPackRat<ContentStored>(
				new ContentStoredPackRat(piedPiper),
				ContentSchemaId.ContentStored
			);

			ContentStored model = new ContentStored()
			{
				Multihash = Multihash.FromBytes(new byte[] { 0x42, 0x49, 0x47 }, MultihashAlgorithm.Sha256)
			};

			Code code = piedPiper.PackModel<ContentStored>(model, ContentSchemaId.ContentStored);
			ContentStored roundTrippedModel = piedPiper.UnpackModel<ContentStored>(code, ContentSchemaId.ContentStored);

			Assert.Equal(model.Multihash, roundTrippedModel.Multihash);
		}
		#endregion
	}
}
