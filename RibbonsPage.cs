using PKHeX.Core;

namespace PKHeXMobile;

/// <summary>Toggles every ribbon the Pokemon's format supports (and sets contest-ribbon counts).</summary>
public class RibbonsPage : ThemedPage
{
    private readonly PKM _pk;
    private readonly List<RibbonInfo> _ribbons;
    private readonly CollectionView _list = new() { SelectionMode = SelectionMode.Single };

    private sealed record Row(RibbonInfo Info, string Text);

    public RibbonsPage(PKM pk)
    {
        _pk = pk;
        Title = "Ribbons";
        _ribbons = RibbonInfo.GetRibbonInfo(pk);

        var all = new Button { Text = "Give all" };
        all.Clicked += (_, _) => SetAll(true);
        var none = new Button { Text = "Clear all" };
        none.Clicked += (_, _) => SetAll(false);

        _list.ItemTemplate = new DataTemplate(() =>
        {
            var l = new Label { Padding = new Thickness(8, 10) };
            l.SetBinding(Label.TextProperty, nameof(Row.Text));
            return l;
        });
        _list.SelectionChanged += async (_, e) =>
        {
            if (e.CurrentSelection.FirstOrDefault() is not Row row) return;
            _list.SelectedItem = null;
            await ToggleAsync(row.Info);
        };

        Content = new Grid
        {
            Padding = 12,
            RowDefinitions = { new(GridLength.Auto), new(GridLength.Star) },
            Children = { new HorizontalStackLayout { Spacing = 8, Children = { all, none } }, At(_list, 1) }
        };
        Refresh();
    }

    private static T At<T>(T v, int row) where T : View { Grid.SetRow(v, row); return v; }

    private static string Pretty(string name) =>
        System.Text.RegularExpressions.Regex.Replace(name[RibbonInfo.PropertyPrefix.Length..], "(?<!^)([A-Z])", " $1");

    private void Refresh() => _list.ItemsSource = _ribbons
        .Select(r => new Row(r, r.Type == RibbonValueType.Boolean
            ? $"{(r.HasRibbon ? "[x]" : "[ ]")} {Pretty(r.Name)}"
            : $"{Pretty(r.Name)}: {r.RibbonCount}"))
        .ToList();

    private void Write(RibbonInfo r)
    {
        var prop = _pk.GetType().GetProperty(r.Name)!;
        prop.SetValue(_pk, r.Type == RibbonValueType.Boolean ? r.HasRibbon : r.RibbonCount);
    }

    private async Task ToggleAsync(RibbonInfo r)
    {
        if (r.Type == RibbonValueType.Boolean)
            r.HasRibbon = !r.HasRibbon;
        else
        {
            var text = await DisplayPromptAsync(Pretty(r.Name), $"0 - {r.MaxCount}", keyboard: Keyboard.Numeric, initialValue: r.RibbonCount.ToString());
            if (!byte.TryParse(text, out var n)) return;
            r.RibbonCount = (byte)Math.Min(n, r.MaxCount);
        }
        Write(r);
        Refresh();
    }

    private void SetAll(bool on)
    {
        foreach (var r in _ribbons)
        {
            if (r.Type == RibbonValueType.Boolean)
                r.HasRibbon = on;
            else
                r.RibbonCount = on ? (byte)r.MaxCount : (byte)0;
            Write(r);
        }
        Refresh();
    }
}
