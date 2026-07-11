using BigRedProf.Data.Core;
using BigRedProf.Stories;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BigRedProf.Content.Test.TestDoubles
{
	/// <summary>
	/// A test scribe that records things into an in-memory list so tests can inspect
	/// exactly what was appended to the catalog story.
	/// </summary>
	public class ListScribe : IScribe
	{
		#region fields
		private readonly List<Code> _things;
		#endregion

		#region constructors
		public ListScribe()
		{
			_things = new List<Code>();
		}
		#endregion

		#region properties
		public IReadOnlyList<Code> Things
		{
			get
			{
				return _things;
			}
		}
		#endregion

		#region IScribe methods
		public void RecordSomething(params Code[] things)
		{
			Task task = RecordSomethingAsync(things);
			task.Wait();
		}

		public Task RecordSomethingAsync(params Code[] things)
		{
			if(things == null)
				throw new ArgumentNullException(nameof(things));

			_things.AddRange(things);

			return Task.CompletedTask;
		}
		#endregion
	}
}
