#nullable enable
namespace Blind.Reactive
{
	public interface IReactiveProperty<T> : IReadOnlyReactiveProperty<T>
	{
		new T Value { get; set; }
	}
}
