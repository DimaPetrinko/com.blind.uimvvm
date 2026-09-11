#nullable enable
using System;
using Blind.UiMvvm.Binding;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace Blind.UiMvvm.Navigation.Implementation
{
	/// <summary>
	/// The part of an opener that is the same for every screen: does a live instance exist, create it
	/// if not, bind it in that same call, report a refusal, and apply the push/pop policy. What varies
	/// is supplied by the project (how to create, how to find the binder and the root) and by the
	/// lifetime subclass (<see cref="ModalScreenOpener{TScreen,TInstance}"/> or
	/// <see cref="PersistentScreenOpener{TScreen,TInstance}"/>).
	///
	/// No container is named here. A project supplies its own creation in <see cref="CreateScreen"/>,
	/// so the DI specifics stay where the DI lives.
	///
	/// <b><see cref="CreateScreen"/> is synchronous and <see cref="Prepare"/> is the only place to
	/// await.</b> That is deliberate: binding has to happen in the same call that created the screen,
	/// before anything can yield, or the screen renders one frame of the placeholder values authored
	/// into its UXML. A screen that must load something awaits in <see cref="Prepare"/> first, then
	/// creates and binds atomically.
	/// </summary>
	public abstract class ScreenOpener<TScreen, TInstance> : IScreenOpener<TScreen>
		where TScreen : struct, Enum
		where TInstance : class
	{
		private readonly Action<string> mLogWarning;
		private readonly Action<string> mLogError;

		private TInstance? mInstance;

		protected ScreenOpener(TScreen screen, Action<string>? logWarning = null, Action<string>? logError = null)
		{
			Screen = screen;
			mLogWarning = logWarning ?? (message => UnityEngine.Debug.LogWarning(message));
			mLogError = logError ?? (message => UnityEngine.Debug.LogError(message));
		}

		public TScreen Screen { get; }

		/// <summary>The live screen, or null before the first open, after a modal close, or once it has been destroyed.</summary>
		protected TInstance? Instance
		{
			get
			{
				if (!IsAlive(mInstance))
					mInstance = null;

				return mInstance;
			}
		}

		/// <summary>
		/// The screen's root element, resolved on every read and never cached - a <see cref="UIDocument"/>
		/// rebuilds its visual tree whenever it is re-enabled, and a held reference would be to the old,
		/// detached one.
		/// </summary>
		protected VisualElement? Root
		{
			get
			{
				var instance = Instance;
				return instance == null ? null : ResolveRoot(instance);
			}
		}

		public async UniTask<bool> Open()
		{
			var instance = Instance;

			if (instance != null && !ReusesInstance)
			{
				mLogWarning($"The {Screen} screen is already open");
				return false;
			}

			if (!await Prepare())
				return false;

			if (Instance == null && !Create())
				return false;

			OnPushed();
			await PlayEnter();
			return true;
		}

		public async UniTask Close()
		{
			if (Instance == null)
				return;

			await PlayExit();
			OnPopped();
		}

		public abstract void SetCovered(bool covered);

		/// <summary>Instantiates the screen. Return null to decline; the base logs that as a fault.</summary>
		protected abstract TInstance? CreateScreen();

		/// <summary>The screen's own binder, resolved from whatever scope the screen brought with it.</summary>
		protected abstract IViewBinder? ResolveBinder(TInstance instance);

		protected virtual VisualElement? ResolveRoot(TInstance instance) => null;

		/// <summary>
		/// Anything that must be loaded before the screen exists. Return false to decline quietly - a
		/// precondition that is simply not met is not an error, so nothing is logged.
		/// </summary>
		protected virtual UniTask<bool> Prepare() => UniTask.FromResult(true);

		/// <summary>Runs once per creation, after the screen has been bound.</summary>
		protected virtual void OnScreenCreated(TInstance instance)
		{
		}

		/// <summary>
		/// The enter animation. Awaited before <see cref="IScreenService{TScreen}.Open"/> covers the
		/// screen underneath, so the outgoing screen is still visible behind it.
		///
		/// Not for a screen opened during gameplay: the stack push waits on this.
		/// </summary>
		protected virtual UniTask PlayEnter() => UniTask.CompletedTask;

		/// <summary>The exit animation. Awaited before the screen is destroyed or covered.</summary>
		protected virtual UniTask PlayExit() => UniTask.CompletedTask;

		/// <summary>Whether a second <see cref="Open"/> reuses the existing instance or refuses.</summary>
		protected abstract bool ReusesInstance { get; }

		protected abstract void OnPushed();

		protected abstract void OnPopped();

		/// <summary>Hands the instance over and clears the field, so a destroy cannot leave it dangling.</summary>
		protected TInstance? TakeInstance()
		{
			var taken = Instance;
			mInstance = null;
			return taken;
		}

		protected void LogWarning(string message) => mLogWarning(message);

		protected void LogError(string message) => mLogError(message);

		private bool Create()
		{
			var created = CreateScreen();
			if (created == null)
			{
				mLogError($"{GetType().Name} could not create the {Screen} screen");
				return false;
			}

			mInstance = created;

			// In this same call, before anything awaits. See the class summary.
			var binder = ResolveBinder(created);
			if (binder == null)
				mLogError(
					$"{GetType().Name} created {Screen} without a view binder; it will render the placeholder values in its UXML");
			else
				binder.Bind();

			OnScreenCreated(created);
			return true;
		}

		/// <summary>
		/// A generic <c>== null</c> is reference equality, so a destroyed <see cref="UnityEngine.Object"/>
		/// would read as a live screen and never be recreated. Unity's own operator has to be asked.
		/// </summary>
		private static bool IsAlive(TInstance? instance) =>
			instance != null && (instance is not UnityEngine.Object unityObject || unityObject != null);
	}
}
