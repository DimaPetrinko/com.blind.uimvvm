#nullable enable
namespace Blind.UiMvvm.Binding
{
	public interface IBindable<in TVm>
	{
		void Bind(TVm vm);
	}
}
