#nullable enable
using System;

namespace Blind.UiMvvm.ReactiveProperties
{
	public interface IReadOnlyReactiveProperty<out T>
	{
		T Value { get; }
		event Action<T> Changed;
	}
}
