#nullable enable
using System;

namespace Blind.UiMvvm.Navigation.Implementation
{
	/// <summary>
	/// A screen created when it opens and destroyed when it closes. A fresh scope, a fresh visual tree
	/// and fresh views every time - and destroying the scope is what unsubscribes the screen's view
	/// models from whatever long-lived model they were reading.
	///
	/// Covered like any screen, through its <see cref="ScreenCover"/> (collapsed by default), so it
	/// needs <see cref="ScreenOpener{TScreen,TInstance}.ResolveRoot"/> once anything can cover it.
	/// </summary>
	public abstract class ModalScreenOpener<TScreen, TInstance> : ScreenOpener<TScreen, TInstance>
		where TScreen : struct, Enum
		where TInstance : class
	{
		protected ModalScreenOpener(
			TScreen screen,
			Action<string>? logWarning = null,
			Action<string>? logError = null)
			: base(screen, logWarning, logError)
		{
		}

		protected ModalScreenOpener(
			TScreen screen,
			ScreenCover cover,
			Action<string>? logWarning = null,
			Action<string>? logError = null)
			: base(screen, cover, logWarning, logError)
		{
		}

		protected sealed override bool ReusesInstance => false;

		protected abstract void DestroyScreen(TInstance instance);

		protected override void OnPushed()
		{
		}

		protected override void OnPopped()
		{
			var closing = TakeInstance();
			if (closing != null)
				DestroyScreen(closing);
		}
	}
}
