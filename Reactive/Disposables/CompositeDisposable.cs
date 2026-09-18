#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Blind.Reactive.Disposables
{
	/// <summary>
	/// Owns a set of subscriptions and disposes them together, so a view model or view keeps one field
	/// instead of one mirrored line per subscription. Mirroring by hand is what produces the classic
	/// failure: a subscription added later and never removed.
	///
	/// Disposal is idempotent and runs in reverse order, and anything added after disposal is disposed
	/// immediately rather than retained - a late <see cref="Add"/> from an async continuation cannot
	/// resurrect a torn-down screen.
	///
	/// One throwing disposable does not strand the rest: exceptions are logged and disposal continues,
	/// the same contract <see cref="Implementation.ReactiveProperty{T}"/> gives its
	/// handlers.
	/// </summary>
	public sealed class CompositeDisposable : IDisposable
	{
		private readonly List<IDisposable> mDisposables = new();

		public bool IsDisposed { get; private set; }

		public int Count => mDisposables.Count;

		public void Add(IDisposable disposable)
		{
			if (disposable == null)
				throw new ArgumentNullException(nameof(disposable));

			if (IsDisposed)
			{
				DisposeOne(disposable);
				return;
			}

			mDisposables.Add(disposable);
		}

		/// <summary>Disposes everything held so far and stays usable.</summary>
		public void Clear()
		{
			for (var i = mDisposables.Count - 1; i >= 0; i--)
				DisposeOne(mDisposables[i]);

			mDisposables.Clear();
		}

		public void Dispose()
		{
			if (IsDisposed)
				return;

			IsDisposed = true;
			Clear();
		}

		private static void DisposeOne(IDisposable disposable)
		{
			try
			{
				disposable.Dispose();
			}
			catch (Exception e)
			{
				Debug.LogException(e);
			}
		}
	}
}
