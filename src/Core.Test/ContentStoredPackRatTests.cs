using BigRedProf.Content.Core;
using BigRedProf.Content.Core.Models;
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
			ContentRegistrar.RegisterContentPackRats(piedPiper);

			ContentStored model = new ContentStored()
			{
				Multihash = Multihash.FromBytes(new byte[] { 0x42, 0x49, 0x47 }, MultihashAlgorithm.Sha256)
			};

			Code code = piedPiper.EncodeModel<ContentStored>(model, ContentSchemaId.ContentStored);
			ContentStored roundTrippedModel = piedPiper.DecodeModel<ContentStored>(code, ContentSchemaId.ContentStored);

			Assert.Equal(model.Multihash, roundTrippedModel.Multihash);
		}
		#endregion
	}
}
