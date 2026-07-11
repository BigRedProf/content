using BigRedProf.Data.Core;
using BigRedProf.Stories;
using System;
using System.Threading.Tasks;

namespace BigRedProf.Content.Test.TestDoubles
{
	/// <summary>
	/// A test scribe that always fails, simulating a catalog story outage.
	/// </summary>
	public class ThrowingScribe : IScribe
	{
		#region IScribe methods
		public void RecordSomething(params Code[] things)
		{
			throw new InvalidOperationException("This scribe always fails.");
		}

		public Task RecordSomethingAsync(params Code[] things)
		{
			throw new InvalidOperationException("This scribe always fails.");
		}
		#endregion
	}
}
