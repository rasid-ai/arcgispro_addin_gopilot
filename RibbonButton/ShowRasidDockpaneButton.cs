using ArcGIS.Desktop.Framework.Contracts;

namespace Rasid.RibbonButton
{
	internal class ShowRasidDockpaneButton : Button
	{
		protected override void OnClick()
		{
			try
			{
				System.Diagnostics.Debug.WriteLine("RASID Button clicked!");
				Dockpane.RasidDockpaneViewModel.Show();
			}
			catch (System.Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error showing dockpane: {ex.Message}\nStack: {ex.StackTrace}");
				ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"Error opening RASID panel:\n{ex.Message}", "Error");
			}
		}

		protected override void OnUpdate()
		{
			this.Enabled = true;
		}
	}
}