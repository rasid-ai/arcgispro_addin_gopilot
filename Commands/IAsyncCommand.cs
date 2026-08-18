#nullable enable

using System.Threading.Tasks;
using System.Windows.Input;

namespace Rasid.Commands
{
	internal interface IAsyncCommand : ICommand
	{
		bool IsExecuting { get; }

		Task ExecuteAsync(object? parameter);

		void RaiseCanExecuteChanged();
	}
}
