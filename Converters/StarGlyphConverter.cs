using System;
using System.Globalization;
using System.Windows.Data;

namespace Rasid.Converters
{
    //converts a Boolean into a filled or empty Unicode star
    public class StarGlyphConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is bool isFilled && isFilled ? "★" : "☆";

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
