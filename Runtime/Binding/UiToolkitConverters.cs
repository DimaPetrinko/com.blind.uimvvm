#nullable enable
using UnityEngine;
using UnityEngine.UIElements;

namespace Blind.UiMvvm.Binding
{
	/// <summary>
	/// Global UXML binding converters, registered once per play session.
	/// A <see cref="DataBinding"/> needs the source type to match the target property, so view models
	/// that express visibility as a plain <c>bool</c> need this bridge. Everything else in this
	/// project binds a target-typed value directly.
	/// </summary>
	public static class UiToolkitConverters
	{
		private static bool s_registered;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		public static void Register()
		{
			if (s_registered)
				return;

			s_registered = true;

			ConverterGroups.RegisterGlobalConverter<bool, StyleEnum<DisplayStyle>>(
				(ref bool isVisible) => isVisible ? DisplayStyle.Flex : DisplayStyle.None);

			ConverterGroups.RegisterGlobalConverter<bool, DisplayStyle>(
				(ref bool isVisible) => isVisible ? DisplayStyle.Flex : DisplayStyle.None);
		}
	}
}
