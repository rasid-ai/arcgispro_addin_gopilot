using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Rasid.Converters
{
	//this converts a boolean into a Visibility value
	//true	Visibility.Visible
	//false	Visibility.Collapsed
	//null	Visibility.Collapsed
	public class BoolToVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			bool boolValue = value is bool b && b;
			if (string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase))
				boolValue = !boolValue;
			return boolValue ? Visibility.Visible : Visibility.Collapsed;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> throw new NotSupportedException();
	}
}