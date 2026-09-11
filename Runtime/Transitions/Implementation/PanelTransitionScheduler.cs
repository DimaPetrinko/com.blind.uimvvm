#nullable enable
using System;
using UnityEngine.UIElements;

namespace Blind.UiMvvm.Transitions.Implementation
{
	/// <summary>
	/// Frames from the element's own panel. The panel runs scheduled callbacks <i>before</i> its style
	/// pass, which is what makes one tick a sufficient deferral: a value written in a callback is
	/// resolved later in that same frame.
	/// </summary>
	public sealed class PanelTransitionScheduler : ITransitionScheduler
	{
		private readonly VisualElement mElement;

		public PanelTransitionScheduler(VisualElement element)
		{
			mElement = element ?? throw new ArgumentNullException(nameof(element));
		}

		public IDisposable NextTick(Action action) => new Handle(mElement.schedule.Execute(action));

		public IDisposable After(float seconds, Action action) =>
			new Handle(mElement.schedule.Execute(action).StartingIn((long)(seconds * 1000f)));

		private sealed class Handle : IDisposable
		{
			private IVisualElementScheduledItem? mItem;

			public Handle(IVisualElementScheduledItem item)
			{
				mItem = item;
			}

			public void Dispose()
			{
				mItem?.Pause();
				mItem = null;
			}
		}
	}
}
