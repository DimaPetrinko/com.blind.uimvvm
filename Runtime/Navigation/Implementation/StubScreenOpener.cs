#nullable enable
using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Blind.UiMvvm.Navigation.Implementation
{
	/// <summary>
	/// Placeholder for a screen that does not exist yet. Swap the registration for a real opener when
	/// the screen lands; nothing else changes.
	/// </summary>
	public class StubScreenOpener<TScreen> : IScreenOpener<TScreen> where TScreen : struct, Enum
	{
		private readonly Action<string> mLog;

		public StubScreenOpener(TScreen screen, Action<string>? log = null)
		{
			Screen = screen;
			mLog = log ?? (message => Debug.LogWarning(message));
		}

		public TScreen Screen { get; }

		public UniTask<bool> Open()
		{
			mLog($"{Screen} screen is not implemented yet");
			return UniTask.FromResult(false);
		}

		public UniTask Close() => UniTask.CompletedTask;

		public void SetCovered(bool covered)
		{
		}
	}
}
