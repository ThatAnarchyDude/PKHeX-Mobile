using PKHeX.Core;

namespace PKHeXMobile;

/// <summary>Item editor laid out like the PC Inventory Editor: pouch icon tabs, an icon/name/count list with striped rows, and Sort / Give All / Save / Cancel.</summary>
public class BagPage : ThemedPage
{
    private readonly SaveFile _sav;
    private readonly PlayerBag _bag;
    private readonly string[] _names;
    private readonly HorizontalStackLayout _pouchTabs = new() { Spacing = 4 };
    private readonly CollectionView _list = new() { SelectionMode = SelectionMode.Single };
    private readonly Entry _count = new() { Keyboard = Keyboard.Numeric, WidthRequest = 80, Text = "995" };
    private int _pouchIndex;

    private sealed record Row(InventoryItem Item, string Name, string Count, ImageSource? Icon, Color Back);

    public BagPage(SaveFile sav)
    {
        _sav = sav;
        _bag = sav.Inventory;
        _names = Names.Items(sav);
        Title = "Inventory Editor";

        _list.ItemTemplate = new DataTemplate(() =>
        {
            var icon = new Image { WidthRequest = 28, HeightRequest = 28, Aspect = Aspect.AspectFit, VerticalOptions = LayoutOptions.Center };
            icon.SetBinding(Image.SourceProperty, nameof(Row.Icon));
            var name = new Label { VerticalOptions = LayoutOptions.Center, TextColor = Colors.Black };
            name.SetBinding(Label.TextProperty, nameof(Row.Name));
            var count = new Label { VerticalOptions = LayoutOptions.Center, HorizontalTextAlignment = TextAlignment.Center, TextColor = Colors.Black };
            count.SetBinding(Label.TextProperty, nameof(Row.Count));
            var grid = new Grid
            {
                Padding = new Thickness(6, 4), ColumnSpacing = 8,
                ColumnDefinitions = { new(new GridLength(36)), new(GridLength.Star), new(new GridLength(64)) },
            };
            grid.SetBinding(VisualElement.BackgroundColorProperty, nameof(Row.Back));
            grid.Add(icon);
            grid.Add(name, 1);
            grid.Add(count, 2);
            return grid;
        });
        _list.SelectionChanged += async (_, e) =>
        {
            if (e.CurrentSelection.FirstOrDefault() is not Row row) return;
            _list.SelectedItem = null;
            await EditAsync(row);
        };

        var sort = new Button { Text = "Sort" };
        sort.Clicked += async (_, _) =>
        {
            Pouch.SortByName(_names);
            await RefreshAsync();
        };
        var save = new Button { Text = "Save" };
        save.Clicked += async (_, _) => { _bag.CopyTo(_sav); await Navigation.PopAsync(); };
        var give = new Button { Text = "Give All" };
        give.Clicked += async (_, _) =>
        {
            var n = int.TryParse(_count.Text, out var v) ? Math.Clamp(v, 1, Pouch.MaxCount) : Pouch.MaxCount;
            Pouch.GiveAllItems(_bag, n);
            await RefreshAsync();
        };
        var cancel = new Button { Text = "Cancel" };
        cancel.Clicked += async (_, _) => await Navigation.PopAsync();

        var bottom = new Grid
        {
            ColumnSpacing = 6, RowSpacing = 6,
            ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star), new(GridLength.Star) },
            RowDefinitions = { new(GridLength.Auto), new(GridLength.Auto) },
        };
        var countRow = new HorizontalStackLayout { Spacing = 8, Children = { new Label { Text = "Count:", VerticalOptions = LayoutOptions.Center }, _count, give } };
        bottom.Add(countRow, 0, 0);
        Grid.SetColumnSpan(countRow, 3);
        bottom.Add(sort, 0, 1);
        bottom.Add(save, 1, 1);
        bottom.Add(cancel, 2, 1);
        Content = new Grid
        {
            Padding = 6, RowSpacing = 6,
            RowDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) },
            Children =
            {
                new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = _pouchTabs },
                InRow(_list, 1),
                InRow(bottom, 2),
            }
        };
        _ = BuildTabsAsync();
    }

    private static T InRow<T>(T v, int row) where T : View { Grid.SetRow(v, row); return v; }

    private InventoryPouch Pouch => _bag.Pouches[_pouchIndex];

    private async Task BuildTabsAsync()
    {
        _pouchTabs.Clear();
        for (int i = 0; i < _bag.Pouches.Count; i++)
        {
            var index = i;
            var button = new ImageButton
            {
                Source = await Sprites.PouchAsync(_bag.Pouches[i].Type),
                WidthRequest = 44, HeightRequest = 40, Padding = 4, Aspect = Aspect.AspectFit,
                BorderWidth = i == _pouchIndex ? 2 : 0, BorderColor = Colors.DodgerBlue, BackgroundColor = Color.FromArgb("#40808080"),
            };
            button.Clicked += async (_, _) => { _pouchIndex = index; await BuildTabsAsync(); await RefreshAsync(); };
            _pouchTabs.Add(button);
        }
        if (_bag.Pouches.Count > 0)
        {
            _count.Text = Pouch.MaxCount.ToString();
            await RefreshAsync();
        }
    }

    private async Task RefreshAsync()
    {
        var dark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var even = dark ? Color.FromArgb("#D8D8D8") : Colors.White; // dark text stays readable on both stripes
        var odd = dark ? Color.FromArgb("#C4C4C4") : Color.FromArgb("#E8E8E8");
        var rows = new List<Row>();
        var items = Pouch.Items;
        for (int i = 0; i < items.Length; i++)
        {
            var it = items[i];
            var empty = it.Index == 0;
            rows.Add(new Row(it, empty ? "None" : Names.At(_names, it.Index), it.Count.ToString(), await Sprites.ItemAsync(_sav, it.Index), i % 2 == 0 ? even : odd));
        }
        _list.ItemsSource = rows;
    }

    private async Task EditAsync(Row row)
    {
        var pouch = Pouch;
        var action = await DisplayActionSheet(row.Name, "Cancel", "Clear slot", "Change item", "Change quantity");
        switch (action)
        {
            case "Clear slot":
                row.Item.Index = 0;
                row.Item.Count = 0;
                break;
            case "Change item":
                var allowed = pouch.GetAllItems().ToArray();
                var list = new List<ComboItem> { new("None", 0) };
                list.AddRange(allowed.Select(id => new ComboItem(Names.At(_names, id), id)));
                var page = new ChoicePage("Item", list, row.Item.Index);
                await Navigation.PushAsync(page);
                if (await page.Result is not { } picked) return;
                row.Item.Index = picked;
                if (picked != 0 && row.Item.Count == 0)
                    row.Item.Count = 1;
                break;
            case "Change quantity":
                var n = await DisplayPromptAsync("Quantity", $"0 - {pouch.MaxCount}", keyboard: Keyboard.Numeric, initialValue: row.Item.Count.ToString());
                if (int.TryParse(n, out var count))
                    row.Item.Count = Math.Clamp(count, 0, pouch.MaxCount);
                break;
            default:
                return;
        }
        await RefreshAsync();
    }
}
