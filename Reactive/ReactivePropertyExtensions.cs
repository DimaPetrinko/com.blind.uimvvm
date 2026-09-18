#nullable enable
using System;

namespace Blind.Reactive
{
	/// <summary>
	/// Subscription helpers for <see cref="IReadOnlyReactiveProperty{T}"/>.
	///
	/// Extension methods rather than interface members, so every existing implementation - including a
	/// project's own fakes and gateways - keeps compiling untouched.
	/// </summary>
	public static class ReactivePropertyExtensions
	{
		/// <summary>
		/// Subscribes <paramref name="handler"/> and, by default, calls it once with the current value.
		///
		/// Priming defaults to on because it is what a display almost always wants: a view model that
		/// subscribes without priming shows whatever its field was initialised to until the source
		/// happens to change. Doing it by hand is the same work split across two places, and the half
		/// that gets forgotten is silent.
		///
		/// Dispose the result to unsubscribe. Disposing twice is a no-op.
		/// </summary>
		public static IDisposable Subscribe<T>(
			this IReadOnlyReactiveProperty<T> property,
			Action<T> handler,
			bool fireImmediately = true)
		{
			if (property == null)
				throw new ArgumentNullException(nameof(property));
			if (handler == null)
				throw new ArgumentNullException(nameof(handler));

			property.Changed += handler;
			var subscription = new Subscription<T>(property, handler);

			if (!fireImmediately)
				return subscription;

			// Unsubscribe before letting the throw out: the caller never receives the handle it would
			// need to clean up, so leaving the subscription registered would leak it permanently.
			try
			{
				handler(property.Value);
			}
			catch
			{
				subscription.Dispose();
				throw;
			}

			return subscription;
		}

		private sealed class Subscription<T> : IDisposable
		{
			private IReadOnlyReactiveProperty<T>? mProperty;
			private Action<T>? mHandler;

			public Subscription(IReadOnlyReactiveProperty<T> property, Action<T> handler)
			{
				mProperty = property;
				mHandler = handler;
			}

			public void Dispose()
			{
				if (mProperty == null || mHandler == null)
					return;

				mProperty.Changed -= mHandler;
				mProperty = null;
				mHandler = null;
			}
		}
	}
}
