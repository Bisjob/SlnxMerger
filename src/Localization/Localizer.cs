using System.Globalization;
using System.Windows;

namespace SlnxMerger.Localization;

internal static class Localizer
{
    internal static string Get(string key)
    {
        var value = Application.Current.TryFindResource(key) as string;
        return string.IsNullOrWhiteSpace(value) ? key : value;
    }

    internal static string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentUICulture, Get(key), args);
}
