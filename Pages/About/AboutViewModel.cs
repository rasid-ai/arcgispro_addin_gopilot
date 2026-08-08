using System.Diagnostics;
using System.Windows.Input;
using ArcGIS.Desktop.Framework;
using Rasid.Pages.Base;
using Rasid.Services;

namespace Rasid.Pages.About
{
	internal class AboutViewModel : PageViewModelBase
	{
		public string VersionText => "RASID SaaS 2.0.0";
		public string CopyrightText => "© 2026 RASID SaaS. All rights reserved.";
		public ICommand OpenWebsiteCommand { get; }
		public ICommand OpenTermsCommand { get; }

		public AboutViewModel()
		{
			OpenWebsiteCommand = new RelayCommand(() => Open("https://rasid.ai"));
			OpenTermsCommand = new RelayCommand(() => Open(ApiClient.Instance.AppBaseUrl + "/terms"));
		}

		private void Open(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
	}
}