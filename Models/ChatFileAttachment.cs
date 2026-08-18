using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace Rasid.Models
{
	internal class ChatFileAttachment : INotifyPropertyChanged
	{
		private BitmapImage _preview;
		private string _previewStatus;
		private string _actionText;
		private bool _isActionEnabled = true;
		private string _localPath;

		public string Url { get; init; }
		public string FileName { get; set; }
		public string Extension { get; set; }
		public bool IsImage { get; set; }

		public BitmapImage Preview
		{
			get => _preview;
			set => SetField(ref _preview, value);
		}

		public string PreviewStatus
		{
			get => _previewStatus;
			set => SetField(ref _previewStatus, value);
		}

		public string ActionText
		{
			get => _actionText;
			set => SetField(ref _actionText, value);
		}

		public bool IsActionEnabled
		{
			get => _isActionEnabled;
			set => SetField(ref _isActionEnabled, value);
		}

		public string LocalPath
		{
			get => _localPath;
			set => SetField(ref _localPath, value);
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private bool SetField<T>(
			ref T field,
			T value,
			[CallerMemberName] string propertyName = null)
		{
			if (EqualityComparer<T>.Default.Equals(field, value))
				return false;

			field = value;
			PropertyChanged?.Invoke(
				this,
				new PropertyChangedEventArgs(propertyName));
			return true;
		}
	}
}
