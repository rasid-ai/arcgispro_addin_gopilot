using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Rasid.Converters
{
	public class RoleToColorConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var role = value as string;
			return role == "user"
				? new SolidColorBrush(Color.FromRgb(0x00, 0x85, 0x6F))   // BRAND_PRIMARY teal
				: new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5)); // light neutral for assistant
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> throw new NotSupportedException();
	}
}