#nullable enable
namespace Blind.UiMvvm.ReactiveProperties
{
	public interface IReactiveProperty<T> : IReadOnlyReactiveProperty<T>
	{
		new T Value { get; set; }
	}
}
