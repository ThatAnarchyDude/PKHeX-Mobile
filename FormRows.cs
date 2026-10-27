namespace PKHeXMobile;

/// <summary>Small label + control rows shared by the extra save editors.</summary>
public static class FormRows
{
    public static Label Header(string text) => new() { Text = text, FontAttributes = FontAttributes.Bold, FontSize = 16, Margin = new Thickness(0, 10, 0, 2) };

    public static View Switch(string label, Func<bool> get, Action<bool> set)
    {
        var sw = new Switch { IsToggled = get(), HorizontalOptions = LayoutOptions.End };
        sw.Toggled += (_, e) => { if (e.Value != get()) set(e.Value); };
        var grid = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) }, MinimumHeightRequest = 40 };
        grid.Add(new Label { Text = label, VerticalOptions = LayoutOptions.Center });
        grid.Add(sw, 1);
        return grid;
    }

    /// <summary>A number entry that clamps to 0..max when focus leaves it; <paramref name="entry"/> lets callers refresh the text later.</summary>
    public static View Number(string label, long value, long max, Action<long> set, out Entry entry)
    {
        var e = entry = new Entry { Text = value.ToString(), Keyboard = Keyboard.Numeric, HorizontalTextAlignment = TextAlignment.End, WidthRequest = 130 };
        e.Unfocused += (_, _) =>
        {
            var v = long.TryParse(e.Text, out var parsed) ? Math.Clamp(parsed, 0, max) : 0;
            e.Text = v.ToString();
            set(v);
        };
        var grid = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) }, MinimumHeightRequest = 40 };
        grid.Add(new Label { Text = label, VerticalOptions = LayoutOptions.Center });
        grid.Add(e, 1);
        return grid;
    }

    public static View Number(string label, long value, long max, Action<long> set) => Number(label, value, max, set, out _);

    public static View Buttons(params Button[] buttons)
    {
        var row = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap };
        foreach (var b in buttons)
        {
            b.Margin = new Thickness(0, 0, 6, 4);
            row.Add(b);
        }
        return row;
    }
}
