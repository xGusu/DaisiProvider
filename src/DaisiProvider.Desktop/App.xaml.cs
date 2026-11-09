using System.Globalization;
using System.Windows;

namespace DaisiProvider.Desktop;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs eventArgs)
    {
        // Dates are shown and typed as yyyy-MM-dd on every PC, like the Day column (user decision, T055).
        // The date picker formats with CurrentCulture; without this it showed US month/day order.
        var culture = (CultureInfo)CultureInfo.CurrentCulture.Clone();
        culture.DateTimeFormat.ShortDatePattern = "yyyy-MM-dd";
        culture.DateTimeFormat.DateSeparator = "-";
        CultureInfo.CurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;

        base.OnStartup(eventArgs);
    }
}
