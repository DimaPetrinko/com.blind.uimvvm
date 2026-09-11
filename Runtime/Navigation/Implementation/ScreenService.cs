#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Blind.UiMvvm.Navigation.Implementation
{
	/// <summary>
	/// Keeps a stack of open screens and dispatches to the opener that claims each one. The screen set
	/// is the consuming project's enum; this only knows how to key openers by it and in what order they
	/// were opened.
	///
	/// The default value of <typeparamref name="TScreen"/> is reserved as "no screen": give the enum a
	/// zero member such as <c>None</c> so an unset or defaulted field can never name a real screen.
	///
	/// Transitions are awaited, so an opener may load before it reports success. One transition runs at
	/// a time: a request that arrives while another is still in flight is refused rather than queued,
	/// because interleaving two transitions would push onto a stack whose top is still moving.
	///
	/// Logging goes through delegates rather than a logging framework, so a project can route these
	/// messages into whatever logger it uses.
	/// </summary>
	public class ScreenService<TScreen> : IScreenService<TScreen> where TScreen : struct, Enum
	{
		private static readonly EqualityComparer<TScreen> s_comparer = EqualityComparer<TScreen>.Default;

		private readonly IReadOnlyDictionary<TScreen, IScreenOpener<TScreen>> mOpeners;
		private readonly List<TScreen> mStack = new();
		private readonly Action<string> mLogWarning;
		private readonly Action<string> mLogError;

		private bool mIsTransitioning;

		public ScreenService(
			IReadOnlyList<IScreenOpener<TScreen>> openers,
			Action<string>? logWarning = null,
			Action<string>? logError = null)
		{
			mLogWarning = logWarning ?? (message => Debug.LogWarning(message));
			mLogError = logError ?? (message => Debug.LogError(message));

			foreach (var unset in openers.Where(opener => IsUnset(opener.Screen)))
				mLogError(
					$"{unset.GetType().Name} claims the default {typeof(TScreen).Name}, which is not a screen; ignoring it");

			var addressable = openers.Where(opener => !IsUnset(opener.Screen)).ToList();

			foreach (var duplicate in addressable.GroupBy(opener => opener.Screen).Where(group => group.Count() > 1))
				mLogWarning($"More than one opener registered for {duplicate.Key}; using the last");

			mOpeners = addressable
				.GroupBy(opener => opener.Screen)
				.ToDictionary(group => group.Key, group => group.Last());
		}

		public TScreen Current => mStack.Count > 0 ? mStack[mStack.Count - 1] : default;

		public bool IsOpen(TScreen screen) => mStack.Contains(screen);

		public async UniTask<bool> Open(TScreen screen)
		{
			if (IsUnset(screen))
			{
				mLogError($"Open was called with the default {typeof(TScreen).Name}, which does not name a screen");
				return false;
			}

			if (!mOpeners.TryGetValue(screen, out var opener))
			{
				mLogWarning($"No opener registered for {screen}");
				return false;
			}

			// Anywhere in the stack, not just on top. One opener owns one screen, so a second entry
			// would hand it Open, SetCovered and Close for two stack slots at once.
			if (IsOpen(screen))
			{
				mLogWarning(s_comparer.Equals(Current, screen)
					? $"{screen} is already the open screen"
					: $"{screen} is already open underneath {Current}");
				return false;
			}

			if (mIsTransitioning)
			{
				mLogWarning($"Ignoring Open({screen}) while another screen transition is still running");
				return false;
			}

			mIsTransitioning = true;
			try
			{
				if (!await opener.Open())
					return false;

				if (mStack.Count > 0)
					mOpeners[Current].SetCovered(true);

				mStack.Add(screen);
				return true;
			}
			finally
			{
				mIsTransitioning = false;
			}
		}

		public async UniTask<bool> Close()
		{
			if (mStack.Count == 0)
			{
				mLogWarning("Close was called with no screen open");
				return false;
			}

			if (mStack.Count == 1)
			{
				mLogWarning($"{Current} is the root screen and cannot be closed");
				return false;
			}

			if (mIsTransitioning)
			{
				mLogWarning($"Ignoring Close of {Current} while another screen transition is still running");
				return false;
			}

			mIsTransitioning = true;
			try
			{
				var closing = Current;
				var revealing = mStack[mStack.Count - 2];

				// Uncovered before the close is awaited, not after, so an exit animation plays over the
				// screen it is revealing rather than over a hidden one. Popped only once the close has
				// finished, so Current names the screen that is actually on top the whole way through.
				mOpeners[revealing].SetCovered(false);
				await mOpeners[closing].Close();
				mStack.RemoveAt(mStack.Count - 1);

				return true;
			}
			finally
			{
				mIsTransitioning = false;
			}
		}

		private static bool IsUnset(TScreen screen) => s_comparer.Equals(screen, default);
	}
}
