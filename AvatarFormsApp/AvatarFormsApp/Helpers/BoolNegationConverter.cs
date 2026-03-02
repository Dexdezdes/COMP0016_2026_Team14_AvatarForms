using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace AvatarFormsApp.Helpers;

/// <summary>Converts bool → inverted bool. Used for IsEnabled="{x:Bind IsBusy, Converter=...}"</summary>
public class BoolNegationConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool b && !b;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is bool b && !b;
}
