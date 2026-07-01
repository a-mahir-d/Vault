using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Vault.Converters;

public class EmailUsernameConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var email    = values[0] as string;
        var username = values[1] as string;

        var hasEmail    = !string.IsNullOrWhiteSpace(email);
        var hasUsername = !string.IsNullOrWhiteSpace(username);

        return hasEmail switch
        {
            true when hasUsername => $"{email} | {username}",
            true => email,
            _ => hasUsername ? username : "—"
        };
    }
}