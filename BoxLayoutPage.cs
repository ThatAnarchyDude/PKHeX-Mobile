using PKHeX.Core;

namespace PKHeXMobile;

/// <summary>Box Layout editor: rename boxes and pick their wallpapers, with a thumbnail of each.</summary>
public class BoxLayoutPage : ThemedPage
{
    private readonly SaveFile _sav;
    private readonly IBoxDetailName _names;
    private readonly IBoxDetailWallpaper? _wallpapers;
    private readonly CollectionView _list = new() { SelectionMode = SelectionMode.Single };

    private sealed record Row(int Box, string Name, string Wallpaper, ImageSource? Thumb);

    public static bool IsSupported(SaveFile sav) => sav is IBoxDetailName;

    public BoxLayoutPage(SaveFile sav)
    {
        _sav = sav;
        _names = (IBoxDetailName)sav;
        _wallpapers = sav as IBoxDetailWallpaper;
        Title = "Box Layout";

        _list.ItemTemplate = new DataTemplate(() =>
        {
            var thumb = new Image { WidthRequest = 72, HeightRequest = 52, Aspect = Aspect.Fill };
            thumb.SetBinding(Image.SourceProperty, nameof(Row.Thumb));
            var name = new Label { FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.End };
            name.SetBinding(Label.TextProperty, nameof(Row.Name));
            var wp = new Label { FontSize = 12, VerticalOptions = LayoutOptions.Start };
            wp.SetBinding(Label.TextProperty, nameof(Row.Wallpaper));
            var grid = new Grid { Padding = new Thickness(6, 4), ColumnSpacing = 10, ColumnDefinitions = { new(new GridLength(80)), new(GridLength.Star) }, RowDefinitions = { new(GridLength.Star), new(GridLength.Star) } };
            grid.Add(thumb);
            Grid.SetRowSpan(thumb, 2);
            grid.Add(name, 1, 0);
            grid.Add(wp, 1, 1);
            return grid;
        });
        _list.SelectionChanged += async (_, e) =>
        {
            if (e.CurrentSelection.FirstOrDefault() is not Row row) return;
            _list.SelectedItem = null;
            await EditAsync(row.Box);
        };
        Content = new Grid { Padding = 8, Children = { _list } };
        _ = RefreshAsync();
    }

    private string[] WallpaperNames => GameInfo.Strings.wallpapernames;

    private async Task RefreshAsync()
    {
        var rows = new List<Row>();
        for (int i = 0; i < _sav.BoxCount; i++)
        {
            var wp = _wallpapers?.GetBoxWallpaper(i) ?? 0;
            var wpName = _wallpapers is null ? string.Empty : Names.At(WallpaperNames, wp);
            var thumb = await Sprites.FileAsync($"box/{(_wallpapers is null ? "box_wp16xy" : BoxWallpaper.NameFor(_sav, wp))}.png");
            rows.Add(new Row(i, _names.GetBoxName(i), wpName, thumb));
        }
        _list.ItemsSource = rows;
    }

    private async Task EditAsync(int box)
    {
        var actions = _wallpapers is null ? new[] { "Rename" } : new[] { "Rename", "Change wallpaper" };
        var choice = await DisplayActionSheet($"Box {box + 1}", "Cancel", null, actions);
        if (choice == "Rename")
        {
            var text = await DisplayPromptAsync("Rename box", $"New name for box {box + 1}", initialValue: _names.GetBoxName(box), maxLength: 12);
            if (string.IsNullOrWhiteSpace(text)) return;
            _names.SetBoxName(box, text.Trim());
        }
        else if (choice == "Change wallpaper" && _wallpapers is not null)
        {
            // Offer only wallpapers this game has art for (the name list covers every game).
            var items = new List<ComboItem>();
            for (int i = 0; i < WallpaperNames.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(WallpaperNames[i])) continue;
                if (await Sprites.FileAsync($"box/{BoxWallpaper.NameFor(_sav, i)}.png") is null) continue;
                items.Add(new ComboItem(WallpaperNames[i], i));
            }
            var page = new ChoicePage($"Wallpaper for box {box + 1}", items, _wallpapers.GetBoxWallpaper(box));
            await Navigation.PushAsync(page);
            if (await page.Result is not { } picked) return;
            _wallpapers.SetBoxWallpaper(box, picked);
        }
        else
            return;
        await RefreshAsync();
    }
}