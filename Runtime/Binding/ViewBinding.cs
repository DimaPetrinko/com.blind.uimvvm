#nullable enable
using System;
using UnityEngine;

namespace Blind.UiMvvm.Binding
{
	/// <summary>
	/// Helpers for the one thing every <see cref="IViewBinder"/> ends up writing: bind this view, and
	/// say something useful if its serialized reference was never assigned.
	/// </summary>
	public static class ViewBinding
	{
		/// <summary>
		/// Binds the view if it is there, and reports it by name if it is not.
		///
		/// An unassigned reference is a wiring mistake in a prefab, not a reason to take the rest of the
		/// screen down with it - the other blocks still work, and the message says which one did not.
		///
		/// Constrained to <see cref="IBindable{TVm}"/> rather than to a view type, because that is all
		/// the body needs and the bindable parts of a screen are not all UI Toolkit views.
		/// </summary>
		public static bool TryBind<TView, TViewModel>(
			TView? view,
			TViewModel viewModel,
			Action<string>? logError = null,
			string? name = null)
			where TView : class, IBindable<TViewModel>
			where TViewModel : class
		{
			// A destroyed or never-assigned UnityEngine.Object is not null by reference, and `!=` on a
			// type parameter does not reach Unity's overload, so the check has to be written out.
			if (view != null && (view is not UnityEngine.Object unityObject || unityObject != null))
			{
				view.Bind(viewModel);
				return true;
			}

			var described = name ?? typeof(TViewModel).Name;
			var message = $"The {described} view reference is not assigned; that part of the screen will not update";

			if (logError == null)
				Debug.LogError(message);
			else
				logError(message);

			return false;
		}
	}
}
