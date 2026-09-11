#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace Blind.UiMvvm.TestSupport
{
	/// <summary>
	/// What an audit found. A result rather than an assertion, so the consuming project keeps its own
	/// assertion style and this assembly needs no test framework of its own - the same checks are then
	/// usable from editor tooling too.
	/// </summary>
	public readonly struct AuditResult
	{
		private static readonly string[] s_none = new string[0];

		private readonly string[]? mProblems;

		private AuditResult(string[]? problems)
		{
			mProblems = problems;
		}

		public static AuditResult Passed => new(null);

		public static AuditResult Failed(IEnumerable<string> problems) => new(problems.ToArray());

		public IReadOnlyList<string> Problems => mProblems ?? s_none;

		public bool IsValid => Problems.Count == 0;

		/// <summary>A single message listing everything found, or an empty string when nothing was.</summary>
		public string Describe() => IsValid ? string.Empty : string.Join("\n", Problems);

		public override string ToString() => IsValid ? "passed" : Describe();
	}
}
