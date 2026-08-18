using System;
using System.Globalization;
using System.Windows.Data;

namespace Rasid.Converters
{
	//used for the navigation buttons,compares 2 string , case-senstive
	//If a toggle button becomes checked, the converter returns its parameter:
	//IsChecked = true
	//ConverterParameter = "Projects"
	//CurrentSection = "Projects"
	public class StringEqualsConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return string.Equals(value as string, parameter as string, StringComparison.Ordinal);
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			//Only write back when the box being toggled is the one being checked (true).
			if (value is bool isChecked && isChecked)
				return parameter as string;

			return Binding.DoNothing;
		}
	}
}