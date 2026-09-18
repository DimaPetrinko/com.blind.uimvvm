#nullable enable
using System;

namespace Blind.Reactive
{
	public interface IReadOnlyReactiveProperty<out T>
	{
		T Value { get; }
		event Action<T> Changed;
	}
}
