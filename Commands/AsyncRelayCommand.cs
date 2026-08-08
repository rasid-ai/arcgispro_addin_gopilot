#nullable enable

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Rasid.Commands
{
	/// <summary>
	/// Task-aware command that does not depend on ArcGIS RelayCommand overloads.
	/// </summary>
	internal sealed class AsyncRelayCommand : IAsyncCommand
	{
		private readonly Func<object?, Task> _execute;
		private readonly Predicate<object?>? _canExecute;
		private readonly Action<Exception>? _onException;
		private int _isExecuting;

		public AsyncRelayCommand(
			Func<object?, Task> execute,
			Predicate<object?>? canExecute = null,
			Action<Exception>? onException = null)
		{
			_execute = execute ?? throw new ArgumentNullException(nameof(execute));
			_canExecute = canExecute;
			_onException = onException;
		}

		public AsyncRelayCommand(
			Func<Task> execute,
			Func<bool>? canExecute = null,
			Action<Exception>? onException = null)
			: this(
				_ => execute(),
				canExecute == null ? null : _ => canExecute(),
				onException)
		{
			if (execute == null)
				throw new ArgumentNullException(nameof(execute));
		}

		public event EventHandler? CanExecuteChanged;

		public bool IsExecuting => Volatile.Read(ref _isExecuting) == 1;

		public bool CanExecute(object? parameter) =>
			!IsExecuting && (_canExecute?.Invoke(parameter) ?? true);

		public async void Execute(object? parameter)
		{
			try
			{
				await ExecuteAsync(parameter);
			}
			catch (Exception exception)
			{
				HandleException(exception);
			}
		}

		public async Task ExecuteAsync(object? parameter)
		{
			if (_canExecute != null && !_canExecute(parameter))
				return;

			if (Interlocked.CompareExchange(ref _isExecuting, 1, 0) != 0)
				return;

			RaiseCanExecuteChanged();

			try
			{
				await _execute(parameter);
			}
			finally
			{
				Interlocked.Exchange(ref _isExecuting, 0);
				RaiseCanExecuteChanged();
			}
		}

		public void RaiseCanExecuteChanged()
		{
			var handler = CanExecuteChanged;
			if (handler == null)
				return;

			var dispatcher = Application.Current?.Dispatcher;
			if (dispatcher == null || dispatcher.CheckAccess())
			{
				handler(this, EventArgs.Empty);
				return;
			}

			dispatcher.BeginInvoke(
				new Action(() => handler(this, EventArgs.Empty)));
		}

		private void HandleException(Exception exception)
		{
			if (_onException == null)
			{
				Trace.TraceError(exception.ToString());
				return;
			}

			try
			{
				_onException(exception);
			}
			catch (Exception handlerException)
			{
				Trace.TraceError(
					$"Async command failed: {exception}{Environment.NewLine}" +
					$"Exception handler also failed: {handlerException}");
			}
		}
	}
}
