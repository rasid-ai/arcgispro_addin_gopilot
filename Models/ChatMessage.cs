using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Rasid.Models
{
	internal class ChatMessage : INotifyPropertyChanged //interface lets the object tell WPF when a property has changed, so the UI can update automatically
	{
		private int _id;
		private int _sessionId;
		private string _content;
		private bool _isUser;
		private DateTime _systemRegistrationDate;
		private bool _isPending;
		private string _attachmentPath;
		private bool _isAoiAttachment;
		private string _aoiTitle;
		private string _aoiDetails;
		private bool _useHistoricalTimestamp;

		[JsonPropertyName("id")]
		public int Id
		{
			get => _id;
			set
			{
				if (SetField(ref _id, value))
					OnPropertyChanged(nameof(TimestampText));
			}
		}

		[JsonPropertyName("session")]
		public int SessionId
		{
			get => _sessionId;
			set => SetField(ref _sessionId, value);
		}

		[JsonPropertyName("content")]
		public string Content
		{
			get => _content;
			set
			{
				if (!SetField(ref _content, value))
					return;

				//refresh XAML bindings that use Text.
				OnPropertyChanged(nameof(Text));
			}
		}

		[JsonPropertyName("is_user")]
		public bool IsUser
		{
			get => _isUser;
			set
			{
				if (!SetField(ref _isUser, value))
					return;

				OnPropertyChanged(nameof(Role));
			}
		}

		[JsonPropertyName("system_registration_date")]
		public DateTime SystemRegistrationDate
		{
			get => _systemRegistrationDate;
			set
			{
				if (!SetField(ref _systemRegistrationDate, value))
					return;

				OnPropertyChanged(nameof(Timestamp));
				OnPropertyChanged(nameof(TimestampText));
			}
		}

		[JsonPropertyName("is_pending")]
		public bool IsPending
		{
			get => _isPending;
			set => SetField(ref _isPending, value);
		}

		[JsonPropertyName("attachment")]
		public string AttachmentPath
		{
			get => _attachmentPath;
			set => SetField(ref _attachmentPath, value);
		}

		[JsonPropertyName("IsAoiAttachment")]
		public bool IsAoiAttachment
		{
			get => _isAoiAttachment;
			set => SetField(ref _isAoiAttachment, value);
		}

		[JsonPropertyName("AoiTitle")]
		public string AoiTitle
		{
			get => _aoiTitle;
			set => SetField(ref _aoiTitle, value);
		}

		[JsonPropertyName("AoiDetails")]
		public string AoiDetails
		{
			get => _aoiDetails;
			set => SetField(ref _aoiDetails, value);
		}

		[JsonIgnore]
		public string Role => IsUser ? "user" : "assistant";

		[JsonIgnore]
		public ObservableCollection<ChatFileAttachment> Files { get; } = new();

		//Text is only a UI alias for Content.
		[JsonIgnore]
		public string Text
		{
			get => Content;
			set => Content = value;
		}

		[JsonIgnore]
		public DateTime Timestamp
		{
			get => SystemRegistrationDate;
			set => SystemRegistrationDate = value;
		}

		[JsonIgnore]
		public string TimestampText
		{
			get
			{
				var timestamp = SystemRegistrationDate == default
					? DateTime.Now
					: SystemRegistrationDate;
				if (timestamp.Kind == DateTimeKind.Utc)
					timestamp = timestamp.ToLocalTime();

				return timestamp.ToString(
					UseHistoricalTimestamp ? "MMM d HH:mm" : "hh:mm",
					CultureInfo.CurrentCulture);
			}
		}

		[JsonIgnore]
		public bool UseHistoricalTimestamp
		{
			get => _useHistoricalTimestamp;
			set
			{
				if (SetField(ref _useHistoricalTimestamp, value))
					OnPropertyChanged(nameof(TimestampText));
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged(
			[CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(
				this,
				new PropertyChangedEventArgs(propertyName));
		}

		private bool SetField<T>(
			ref T field,
			T value,
			[CallerMemberName] string propertyName = null)
		{
			if (EqualityComparer<T>.Default.Equals(field, value))
				return false;

			field = value;
			OnPropertyChanged(propertyName);
			return true;
		}
	}
}
