// DrawingToolbarViewModel.cs
using System;
using System.Windows.Input;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace Rasid.Services.Geometry
{
	internal class DrawingToolbarViewModel : PropertyChangedBase
	{
		private string _infoText =
			"0 points\nArea: 0.0 m²  •  Perimeter: 0.0 m";
		public string InfoText { get => _infoText; set => SetProperty(ref _infoText, value); }

		private bool _canModify;
		public bool CanModify
		{
			get => _canModify;
			set => SetProperty(ref _canModify, value);
		}

		private bool _canFinish;
		public bool CanFinish
		{
			get => _canFinish;
			set => SetProperty(ref _canFinish, value);
		}

		public ICommand CancelCommand { get; }
		public ICommand UndoCommand { get; }
		public ICommand ClearCommand { get; }
		public ICommand FinishCommand { get; }
		public event Action CancelRequested;
		public event Action UndoRequested;
		public event Action ClearRequested;
		public event Action FinishRequested;

		public DrawingToolbarViewModel()
		{
			CancelCommand = new RelayCommand(() => CancelRequested?.Invoke());
			UndoCommand = new RelayCommand(() => UndoRequested?.Invoke());
			ClearCommand = new RelayCommand(() => ClearRequested?.Invoke());
			FinishCommand = new RelayCommand(() => FinishRequested?.Invoke());
		}

		public void UpdateInfo(int pointCount, double areaSqM, double perimeterM)
		{
			string Format(double v, string small, double divisor, string big) =>
				v < divisor ? $"{v:F1} {small}" : $"{v / divisor:F2} {big}";

			var areaText = Format(areaSqM, "m²", 1_000_000, "km²");
			var perimeterText = Format(perimeterM, "m", 1000, "km");
			InfoText =
				$"{pointCount} {(pointCount == 1 ? "point" : "points")}\n" +
				$"Area: {areaText}  •  Perimeter: {perimeterText}";
			CanModify = pointCount > 0;
			CanFinish = pointCount >= 3;
		}
	}
}
