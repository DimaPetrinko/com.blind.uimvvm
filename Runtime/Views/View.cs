#nullable enable
using System;
using System.Collections.Generic;
using Blind.Reactive;
using Blind.Reactive.Disposables;
using Blind.UiMvvm.Binding;
using UnityEngine;
using UnityEngine.UIElements;

namespace Blind.UiMvvm.Views
{
	/// <summary>
	/// A view: element lookup and event wiring, nothing else. Formatting and game-state decisions
	/// belong to the view model, which Unity's runtime binding reads directly through
	/// <see cref="VisualElement.dataSource"/>.
	///
	/// Everything wired through <see cref="OnClick"/>, <see cref="Subscribe{T}"/>, <see cref="Track"/>
	/// and <see cref="Every"/> is undone in <see cref="Unbind"/>, which <c>OnDestroy</c> calls. That is
	/// the point of them: a hand-mirrored teardown is one line per subscription in two places, and the
	/// line that gets forgotten is the one added last.
	///
	/// <b>Do not declare <c>OnDestroy</c> in a subclass.</b> Unity dispatches the most derived one only,
	/// so a private <c>OnDestroy</c> hides this class's and nothing is ever unwired. Override
	/// <see cref="OnUnbind"/> instead.
	/// </summary>
	public abstract class View<TViewModel> : ViewRoot, IBindable<TViewModel> where TViewModel : class
	{
		protected VisualElement mRoot = null!;
		protected TViewModel mViewModel = default!;

		private readonly CompositeDisposable mSubscriptions = new();
		private readonly List<(Button Button, Action Handler)> mClicks = new();
		private readonly List<IVisualElementScheduledItem> mScheduled = new();

		public bool IsBound { get; private set; }

		public virtual void Bind(TViewModel viewModel)
		{
			if (viewModel == null)
				throw new ArgumentNullException(nameof(viewModel));

			if (IsBound)
				Unbind();

			mViewModel = viewModel;
			mRoot = ResolveRoot();
			mRoot.dataSource = viewModel;
			IsBound = true;
		}

		/// <summary>
		/// Undoes everything <see cref="Bind"/> wired. Safe to call twice, and a view that has been
		/// unbound can be bound again - which is what a screen shown by re-enabling its
		/// <see cref="UIDocument"/> needs, since that rebuilds the visual tree.
		/// </summary>
		public void Unbind()
		{
			if (!IsBound)
				return;

			IsBound = false;

			// Before the wiring is torn down, so an override still sees a live view model and root.
			OnUnbind();

			for (var i = mClicks.Count - 1; i >= 0; i--)
			{
				var (button, handler) = mClicks[i];
				if (button != null)
					button.clicked -= handler;
			}

			mClicks.Clear();

			for (var i = mScheduled.Count - 1; i >= 0; i--)
				mScheduled[i]?.Pause();

			mScheduled.Clear();
			mSubscriptions.Clear();

			if (mRoot != null)
				mRoot.dataSource = null;

			mRoot = null!;
			mViewModel = default!;

			// The tree this view was bound to may be gone; the next Bind must look its root up again.
			InvalidateRoot();
		}

		/// <summary>Teardown a subclass cannot express through the helpers below. Called while still bound.</summary>
		protected virtual void OnUnbind()
		{
		}

		protected virtual void OnDestroy() => Unbind();

		/// <summary>
		/// The named element, or a thrown <see cref="MissingComponentException"/> naming this view, the
		/// element and the root it looked under. <see cref="VisualElement.Q"/> returns null for a
		/// misspelled or renamed name, and a null that far from its cause is the hardest kind of UI bug
		/// to place - the same reason <see cref="ViewRoot.ResolveRoot"/> throws.
		/// </summary>
		protected T Require<T>(string name) where T : VisualElement
		{
			var element = RequireRoot().Q<T>(name);
			if (element == null)
				throw new MissingComponentException(
					$"{GetType().Name}: no {typeof(T).Name} named '{name}' under '{RootName}'");

			return element;
		}

		protected VisualElement Require(string name) => Require<VisualElement>(name);

		/// <summary>For an element a screen may legitimately be authored without.</summary>
		protected T? Optional<T>(string name) where T : VisualElement => RequireRoot().Q<T>(name);

		protected void OnClick(Button button, Action handler)
		{
			if (button == null)
				throw new ArgumentNullException(nameof(button));
			if (handler == null)
				throw new ArgumentNullException(nameof(handler));

			button.clicked += handler;
			mClicks.Add((button, handler));
		}

		/// <summary>Looks the button up and wires it in one step. The common case.</summary>
		protected Button OnClick(string name, Action handler)
		{
			var button = Require<Button>(name);
			OnClick(button, handler);
			return button;
		}

		/// <summary>
		/// Subscribes for the life of the binding. A USS class is the one display decision a view has
		/// to make for itself, because no binding can toggle one.
		/// </summary>
		protected void Subscribe<T>(IReadOnlyReactiveProperty<T> property, Action<T> handler, bool fireImmediately = true) =>
			Track(property.Subscribe(handler, fireImmediately));

		protected void Track(IDisposable disposable) => mSubscriptions.Add(disposable);

		/// <summary>
		/// A repeating callback on the panel's scheduler, paused on unbind. For a countdown that has to
		/// re-read a value nothing pushes - not for anything a reactive source already reports.
		/// </summary>
		protected IVisualElementScheduledItem Every(long milliseconds, Action action)
		{
			var scheduled = RequireRoot().schedule.Execute(action).Every(milliseconds);
			mScheduled.Add(scheduled);
			return scheduled;
		}

		private VisualElement RequireRoot()
		{
			if (mRoot == null)
				throw new InvalidOperationException(
					$"{GetType().Name} is not bound yet; call base.Bind(viewModel) first");

			return mRoot;
		}
	}
}
