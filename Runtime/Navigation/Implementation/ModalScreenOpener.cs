#nullable enable
using System;

namespace Blind.UiMvvm.Navigation.Implementation
{
	/// <summary>
	/// A screen created when it opens and destroyed when it closes. A fresh scope, a fresh visual tree
	/// and fresh views every time - and destroying the scope is what unsubscribes the screen's view
	/// models from whatever long-lived model they were reading.
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

		protected sealed override bool ReusesInstance => false;

		protected abstract void DestroyScreen(TInstance instance);

		/// <summary>Nothing by default. Override to dim a modal that another modal has opened over.</summary>
		public override void SetCovered(bool covered)
		{
		}

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
