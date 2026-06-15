using System.Globalization;
using System.Windows;

namespace SlnxMerger;

public partial class App : Application
{
    private ResourceDictionary? languageDictionary;

    internal string CurrentLanguageCode { get; private set; } = "en-US";

    protected override void OnStartup(StartupEventArgs e)
    {
        SetLanguage(CultureInfo.CurrentUICulture.Name);
        base.OnStartup(e);
    }

    internal void SetLanguage(string languageCode)
    {
        var normalizedCode = languageCode.StartsWith("fr", StringComparison.OrdinalIgnoreCase)
            ? "fr-FR"
            : "en-US";

        var culture = new CultureInfo(normalizedCode);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        var dictionaryPath = normalizedCode == "fr-FR"
            ? "Resources/Strings.fr-FR.xaml"
            : "Resources/Strings.en-US.xaml";

        if (languageDictionary != null)
            Resources.MergedDictionaries.Remove(languageDictionary);

        languageDictionary = new ResourceDictionary
        {
            Source = new Uri(dictionaryPath, UriKind.Relative),
        };

        Resources.MergedDictionaries.Add(languageDictionary);
        CurrentLanguageCode = normalizedCode;
    }
}
