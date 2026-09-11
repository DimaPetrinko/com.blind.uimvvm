#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Blind.UiMvvm.ReactiveProperties.Implementation
{
	/// <summary>
	/// A change-notifying value. <see cref="Changed"/> fires only when the value actually differs, so a
	/// re-entrant publish from inside a handler terminates instead of looping.
	///
	/// Handler exceptions are logged rather than propagated: one bad subscriber must not stop the
	/// others from seeing a change, because they are usually unrelated parts of a screen.
	///
	/// Both hot paths are allocation-free on purpose - a UI-facing property is written every frame in
	/// the worst case. Equality goes through <see cref="EqualityComparer{T}"/> rather than the static
	/// <c>object.Equals</c> (which boxes both operands for a value type), and notification walks a
	/// reused snapshot rather than the array <c>Delegate.GetInvocationList</c> allocates per raise.
	/// </summary>
	public class ReactiveProperty<T> : IReactiveProperty<T>
	{
		private static readonly EqualityComparer<T> s_comparer = EqualityComparer<T>.Default;

		private readonly List<Action<T>> mHandlers = new();

		private Action<T>[] mSnapshot = Array.Empty<Action<T>>();
		private bool mIsNotifying;
		private T mValue;

		public ReactiveProperty(T initialValue = default!)
		{
			mValue = initialValue;
		}

		/// <summary>
		/// Explicit accessors rather than a field-like event: the subscriber list has to be walkable
		/// without allocating, which a delegate chain is not.
		/// </summary>
		public event Action<T> Changed
		{
			add
			{
				if (value != null)
					mHandlers.Add(value);
			}
			remove
			{
				if (value != null)
					mHandlers.Remove(value);
			}
		}

		public T Value
		{
			get => mValue;
			set
			{
				if (s_comparer.Equals(mValue, value))
					return;

				mValue = value;
				InvokeChanged(mValue);
			}
		}

		/// <summary>
		/// Raises <see cref="Changed"/> without a comparison. For a reference type whose contents
		/// changed while the reference did not - the only case the equality guard gets wrong.
		/// </summary>
		public void ForceNotify() => InvokeChanged(mValue);

		public static implicit operator T(ReactiveProperty<T> property)
		{
			return property.Value;
		}

		private void InvokeChanged(T value)
		{
			var count = mHandlers.Count;
			if (count == 0)
				return;

			// A handler that writes back to this property re-enters here. The shared snapshot is still
			// being walked by the outer call, so the nested one takes a copy of its own; that is rare
			// enough to be worth an allocation and cheap enough not to matter.
			if (mIsNotifying)
			{
				Notify(mHandlers.ToArray(), count, value);
				return;
			}

			if (mSnapshot.Length < count)
				mSnapshot = new Action<T>[count];

			mHandlers.CopyTo(0, mSnapshot, 0, count);

			mIsNotifying = true;
			try
			{
				Notify(mSnapshot, count, value);
			}
			finally
			{
				mIsNotifying = false;

				// A handler unsubscribed since the copy would otherwise be kept alive by the snapshot
				// until the next raise - and the things that unsubscribe here are destroyed screens.
				// The whole buffer, not the first `count`: the interesting case is the subscriber list
				// shrinking, which leaves exactly those stale entries past the end of the new copy.
				Array.Clear(mSnapshot, 0, mSnapshot.Length);
			}
		}

		private static void Notify(Action<T>[] handlers, int count, T value)
		{
			for (var i = 0; i < count; i++)
			{
				try
				{
					handlers[i](value);
				}
				catch (Exception e)
				{
					Debug.LogException(e);
				}
			}
		}
	}
}
