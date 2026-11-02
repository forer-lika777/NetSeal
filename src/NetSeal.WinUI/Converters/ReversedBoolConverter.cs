using Microsoft.UI.Xaml.Data;
using System;

namespace NetSeal.WinUI.Converters;

public class ReversedBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not bool boolean)
            return true;

        return !boolean;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
