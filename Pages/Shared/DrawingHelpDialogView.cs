using System.Threading.Tasks;
using ArcGIS.Desktop.Framework;

namespace Rasid.Pages.Shared
{
	internal static class DrawingHelpDialogView
	{
		private static bool _hiddenForSession;

		public static bool ShouldShow() => !_hiddenForSession;

		public static Task<bool> ShowHelpAsync(string context)
		{
			var dialog = new DrawingHelpDialog
			{
				Owner = FrameworkApplication.Current.MainWindow
			};
			var accepted = dialog.ShowDialog() == true;

			if (accepted && dialog.DontShowAgain)
				_hiddenForSession = true;

			return Task.FromResult(accepted);
		}
	}
}
