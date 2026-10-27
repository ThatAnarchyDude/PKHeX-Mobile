namespace PKHeXMobile;

/// <summary>Light/dark choice: follows the phone until the bulb is tapped, then remembers the user's pick.</summary>
public static class ThemeToggle
{
    private const string Key = "pkhex_theme";

    public static bool IsDark => Application.Current?.RequestedTheme == AppTheme.Dark;

    /// <summary>Re-applies a saved choice at startup.</summary>
    public static void Restore()
    {
        try
        {
            var saved = Preferences.Default.Get(Key, string.Empty);
            if (Application.Current is { } app && saved is "light" or "dark")
                app.UserAppTheme = saved == "dark" ? AppTheme.Dark : AppTheme.Light;
        }
        catch (Exception) { /* follow the phone */ }
    }

    public static void Toggle()
    {
        if (Application.Current is not { } app) return;
        var next = IsDark ? AppTheme.Light : AppTheme.Dark;
        app.UserAppTheme = next;
        try { Preferences.Default.Set(Key, next == AppTheme.Dark ? "dark" : "light"); }
        catch (Exception) { /* not saved this time */ }
    }
}