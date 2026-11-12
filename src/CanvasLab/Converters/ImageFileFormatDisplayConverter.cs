using System.Globalization;
using System.Windows.Data;
using CanvasLab.Models;

namespace CanvasLab.Converters;

public sealed class ImageFileFormatDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        ImageFileFormat.Jpeg => "JPEG",
        ImageFileFormat.Png => "PNG",
        ImageFileFormat.WebP => "WebP",
        _ => value?.ToString() ?? string.Empty
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
