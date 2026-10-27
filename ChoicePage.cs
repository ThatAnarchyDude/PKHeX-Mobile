using PKHeX.Core;

namespace PKHeXMobile;

/// <summary>A searchable list of named choices (like a PC PKHeX dropdown); completes with the chosen value, or null when dismissed.</summary>
public class ChoicePage : ThemedPage
{
    private readonly IReadOnlyList<ComboItem> _items;
    private readonly TaskCompletionSource<int?> _result = new();
    private readonly SearchBar _search = new() { Placeholder = "Search" };
    private readonly CollectionView _list = new() { SelectionMode = SelectionMode.Single };

    public Task<int?> Result => _result.Task;

    public ChoicePage(string title, IReadOnlyList<ComboItem> items, int current)
    {
        Title = title;
        _items = items;
        _search.TextChanged += (_, _) => Refresh();
        _list.ItemTemplate = new DataTemplate(() =>
        {
            var l = new Label { Padding = new Thickness(8, 10) };
            l.SetBinding(Label.TextProperty, nameof(ComboItem.Text));
            return l;
        });
        _list.SelectionChanged += async (_, e) =>
        {
            if (e.CurrentSelection.FirstOrDefault() is not ComboItem item) return;
            _result.TrySetResult(item.Value);
            await Navigation.PopAsync();
        };

        Content = new Grid
        {
            Padding = 12,
            RowDefinitions = { new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Star) },
            Children = { _search, At(new Label { Text = $"Current: {Name(items, current)}" }, 1), At(_list, 2) }
        };
        Refresh();
    }

    public static string Name(IReadOnlyList<ComboItem> items, int value) =>
        items.FirstOrDefault(i => i.Value == value)?.Text ?? value.ToString();

    private static T At<T>(T v, int row) where T : View { Grid.SetRow(v, row); return v; }

    private void Refresh()
    {
        var q = _search.Text?.Trim() ?? string.Empty;
        _list.ItemsSource = _items
            .Where(i => q.Length == 0 || i.Text.Contains(q, StringComparison.OrdinalIgnoreCase))
            .Take(400)
            .ToList();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _result.TrySetResult(null);
    }
}
