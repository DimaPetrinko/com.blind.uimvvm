#nullable enable
using System;
using Blind.UiMvvm.Views;
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
		/// </summary>
		public static bool TryBind<TViewModel>(
			View<TViewModel>? view,
			TViewModel viewModel,
			Action<string>? logError = null,
			string? name = null)
			where TViewModel : class
		{
			if (view != null)
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
