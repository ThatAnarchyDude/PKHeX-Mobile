namespace PKHeXMobile;

/// <summary>Base page that applies the PC-style palette each time it appears and carries the light/dark bulb button.</summary>
public abstract class ThemedPage : ContentPage
{
    private readonly ToolbarItem _bulb = new() { Order = ToolbarItemOrder.Primary, Priority = 0, Text = "Theme" };

    protected ThemedPage()
    {
        _bulb.Clicked += async (_, _) =>
        {
            ThemeToggle.Toggle();
            await UpdateBulbAsync();
        };
        ToolbarItems.Add(_bulb);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        try { Theme.StylePage(this); }
        catch (Exception) { /* keep the default look */ }
        _ = UpdateBulbAsync();
    }

    /// <summary>Black bulb while in Dark Mode, yellow bulb while in Light Mode.</summary>
    private async Task UpdateBulbAsync()
    {
        try { _bulb.IconImageSource = await Sprites.FileAsync(ThemeToggle.IsDark ? "ui/bulb_dark.png" : "ui/bulb_light.png"); }
        catch (Exception) { /* the toolbar item keeps its text */ }
    }
}