using System.Collections.Specialized;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Rasid.Pages.GoPilot
{
	public partial class GoPilotView : UserControl
	{
		private INotifyCollectionChanged _messages;

		public GoPilotView()
		{
			InitializeComponent();
			ChatScrollViewer.AddHandler(
				MouseWheelEvent,
				new MouseWheelEventHandler(OnChatMouseWheel),
				true);
			Loaded += (_, _) => AttachMessages();
			Unloaded += (_, _) => DetachMessages();
			DataContextChanged += (_, _) =>
			{
				if (IsLoaded)
					AttachMessages();
			};
		}

		private void AttachMessages()
		{
			DetachMessages();
			if (DataContext is not GoPilotViewModel viewModel)
				return;

			_messages = viewModel.Messages;
			_messages.CollectionChanged += OnMessagesChanged;
			ScrollToLatest();
		}

		private void DetachMessages()
		{
			if (_messages != null)
				_messages.CollectionChanged -= OnMessagesChanged;
			_messages = null;
		}

		private void OnMessagesChanged(
			object sender,
			NotifyCollectionChangedEventArgs args) =>
			ScrollToLatest();

		private void OnChatMouseWheel(
			object sender,
			MouseWheelEventArgs args)
		{
			ChatScrollViewer.ScrollToVerticalOffset(
				ChatScrollViewer.VerticalOffset - args.Delta);
			args.Handled = true;
		}

		private void ScrollToLatest() =>
			Dispatcher.InvokeAsync(
				() =>
				{
					ChatScrollViewer.UpdateLayout();
					ChatScrollViewer.ScrollToEnd();
				},
				DispatcherPriority.ContextIdle);
	}
}
