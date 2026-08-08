using System.Collections.ObjectModel;
using System.Windows.Input;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using Rasid.Commands;
using Rasid.Services;

namespace Rasid.Pages.Shared
{
	internal class FeedbackDialogViewModel : PropertyChangedBase
	{
		private readonly RasidApiClient _api;
		private readonly string _currentPage;

		public ObservableCollection<StarViewModel> Stars { get; } = new();

		private int _rating;
		public int Rating { get => _rating; set { SetProperty(ref _rating, value); UpdateStars(); } }

		private string _message;
		public string Message { get => _message; set => SetProperty(ref _message, value); }

		private bool _isSubmitting;
		public bool IsSubmitting { get => _isSubmitting; set => SetProperty(ref _isSubmitting, value); }

		public ICommand SubmitCommand { get; }
		public event System.Action Submitted;

		public FeedbackDialogViewModel(string currentPage = null)
		{
			_api = new RasidApiClient(ApiClient.Instance);
			_currentPage = currentPage ?? "unknown";
			for (int i = 1; i <= 5; i++)
			{
				var ratingValue = i;
				Stars.Add(new StarViewModel
				{
					Value = ratingValue,
					SetRatingCommand = new RelayCommand(() => Rating = ratingValue)
				});
			}

			SubmitCommand = new AsyncRelayCommand(SubmitAsync);
		}

		private void UpdateStars()
		{
			foreach (var s in Stars) s.IsFilled = s.Value <= Rating;
		}

		private async System.Threading.Tasks.Task SubmitAsync()
		{
			if (IsSubmitting)
				return;

			if (Rating < 1)
			{
				ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("Please select a rating.", "Missing Rating");
				return;
			}

			if (string.IsNullOrWhiteSpace(Message))
			{
				ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("Please enter your feedback message.", "Missing Message");
				return;
			}

			IsSubmitting = true;
			try
			{
				var payload = new
				{
					message = Message,
					rating = Rating,
					feedback_infos = new
					{
						plugin_version = "2.0.0",
						platform = System.Environment.OSVersion.Platform.ToString(),
						platform_version = System.Environment.OSVersion.VersionString,
						current_page = _currentPage
					}
				};
				await _api.SubmitFeedbackAsync(payload);
				ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
					"Your feedback has been submitted successfully.", "Thank You!");
				Submitted?.Invoke();
			}
			catch (System.Exception ex)
			{
				ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"Failed to submit feedback:\n\n{ex.Message}", "Submission Failed");
			}
			finally { IsSubmitting = false; }
		}
	}

	internal class StarViewModel : PropertyChangedBase
	{
		public int Value { get; set; }
		public ICommand SetRatingCommand { get; set; }
		private bool _isFilled;
		public bool IsFilled { get => _isFilled; set => SetProperty(ref _isFilled, value); }
	}
}
