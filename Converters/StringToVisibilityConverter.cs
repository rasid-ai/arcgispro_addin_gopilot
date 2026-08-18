using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Rasid.Converters
{
	//This shows an element when a string contains something and collapses it when the string is empty
	public class StringToVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
