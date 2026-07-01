using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Vault.Converters;

public class FileSizeConverter : IValueConverter
{
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("FileSizeConverter sadece tek yönlü (OneWay) binding destekler.");
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ulong bytes) return value;
        
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        if (bytes == 0) return "0B";

        var i = 0;
        double dblSSize = bytes;
                
        while (dblSSize >= 1024 && i < suffixes.Length - 1)
        {
            i++;
            dblSSize /= 1024;
        }
            
        return $"{dblSSize:0.#}{suffixes[i]}";
    }
}