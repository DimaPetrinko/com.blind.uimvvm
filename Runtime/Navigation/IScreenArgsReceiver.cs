#nullable enable

namespace Blind.UiMvvm.Navigation
{
	/// <summary>
	/// Takes data handed to a screen that is already open, through
	/// <see cref="IScreenService{TScreen}.Deliver{TArgs}"/>. The package never resolves it: an opener
	/// finds the receiver in whatever scope its screen brought and passes the args on.
	/// </summary>
	public interface IScreenArgsReceiver<in TArgs> where TArgs : class
	{
		void Receive(TArgs args);
	}
}
