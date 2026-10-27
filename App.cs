namespace PKHeXMobile;

public class App : Application
{
    public App()
    {
        ThemeToggle.Restore();
        try { Theme.Apply(Resources); }
        catch (Exception) { /* fall back to the default look */ }
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var nav = new NavigationPage(new MainPage());
        try { Theme.StyleBar(nav); }
        catch (Exception) { /* default navigation bar */ }
        return new Window(nav);
    }
}
