using PKHeX.Core;

namespace PKHeXMobile;

/// <summary>Event flags and event variables with the PC editor's names (Psyduck Yellow included), searchable by name or number.</summary>
public class FlagsPage : ThemedPage
{
    private readonly EventWorkspace<IEventFlag37, ushort> _ws;
    private readonly SearchBar _search = new() { Placeholder = "Search name or number (hex ok: 0xA7D)" };
    private readonly CollectionView _list = new() { SelectionMode = SelectionMode.Single };
    private readonly Button _flagsTab = new() { Text = "Flags" }, _varsTab = new() { Text = "Variables" };
    private readonly Dictionary<int, NamedEventValue> _flagNames, _workNames;
    private bool _showFlags = true;

    private sealed record Row(int Index, string Text);

    /// <summary>Returns the flag/variable block for the save, or null when the game has no such editor here.</summary>
    public static IEventFlag37? GetBlock(SaveFile sav) => sav as IEventFlag37 ?? (sav as IEventFlagProvider37)?.EventWork;

    public FlagsPage(SaveFile sav)
    {
        Title = "Event flags";
        var block = GetBlock(sav) ?? throw new NotSupportedException("This save has no event flag editor.");
        _ws = new EventWorkspace<IEventFlag37, ushort>(block, sav.Version);
        _flagNames = _ws.Labels.Flag.ToDictionary(f => f.Index);
        _workNames = _ws.Labels.Work.ToDictionary(w => w.Index, w => (NamedEventValue)w);

        _flagsTab.Clicked += (_, _) => { _showFlags = true; Refresh(); };
        _varsTab.Clicked += (_, _) => { _showFlags = false; Refresh(); };
        _search.TextChanged += (_, _) => Refresh();
        _list.ItemTemplate = new DataTemplate(() =>
        {
            var l = new Label { Padding = new Thickness(8, 10), FontSize = 14 };
            l.SetBinding(Label.TextProperty, nameof(Row.Text));
            return l;
        });
        _list.SelectionChanged += async (_, e) =>
        {
            if (e.CurrentSelection.FirstOrDefault() is not Row row) return;
            _list.SelectedItem = null;
            await EditAsync(row.Index);
        };

        Content = new Grid
        {
            Padding = 8, RowSpacing = 6,
            RowDefinitions = { new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Star) },
            Children =
            {
                new Grid { ColumnSpacing = 6, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star) }, Children = { _flagsTab, Column(_varsTab, 1) } },
                InRow(_search, 1),
                InRow(_list, 2),
            }
        };
        Refresh();
    }

    private static T InRow<T>(T v, int row) where T : View { Grid.SetRow(v, row); return v; }
    private static T Column<T>(T v, int column) where T : View { Grid.SetColumn(v, column); return v; }

    private static bool TryNumber(string text, out int n)
    {
        text = text.Trim();
        return text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? int.TryParse(text.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out n)
            : int.TryParse(text, out n);
    }

    private void Refresh()
    {
        _flagsTab.FontAttributes = _showFlags ? FontAttributes.Bold : FontAttributes.None;
        _varsTab.FontAttributes = _showFlags ? FontAttributes.None : FontAttributes.Bold;

        var q = _search.Text?.Trim() ?? string.Empty;
        var hasNumber = TryNumber(q, out var number);
        var count = _showFlags ? _ws.Flags.Length : _ws.Values.Length;
        var names = _showFlags ? _flagNames : _workNames;

        var rows = new List<Row>();
        for (int i = 0; i < count; i++)
        {
            names.TryGetValue(i, out var named);
            var label = named?.Name ?? "(unnamed)";
            if (q.Length != 0 && !(hasNumber && i == number) && !label.Contains(q, StringComparison.OrdinalIgnoreCase))
                continue;
            rows.Add(new Row(i, _showFlags
                ? $"{(_ws.Flags[i] ? "[x]" : "[ ]")} 0x{i:X3}  {label}"
                : $"0x{i:X3}  {label} = {_ws.Values[i]}"));
        }
        _list.ItemsSource = rows;
    }

    private async Task EditAsync(int index)
    {
        if (_showFlags)
        {
            _ws.Flags[index] = !_ws.Flags[index];
            _ws.Save();
            Refresh();
            return;
        }

        int? value = null;
        if (_workNames.TryGetValue(index, out var w) && w is NamedEventWork work && work.PredefinedValues.Count > 1)
        {
            var items = work.PredefinedValues.Select(v => new ComboItem(v.Value == NamedEventConst.CustomMagicValue ? "Custom value..." : $"{v.Name} ({v.Value})", v.Value)).ToList();
            var page = new ChoicePage(w.Name, items, _ws.Values[index]);
            await Navigation.PushAsync(page);
            if (await page.Result is not { } picked) return;
            if (picked != NamedEventConst.CustomMagicValue)
                value = picked;
        }
        if (value is null)
        {
            var text = await DisplayPromptAsync($"0x{index:X3}", "Value 0 - 65535", keyboard: Keyboard.Numeric, initialValue: _ws.Values[index].ToString());
            if (text is null) return;
            if (!ushort.TryParse(text, out var parsed)) { await DisplayAlertAsync("Variable", "Enter a number from 0 to 65535.", "OK"); return; }
            value = parsed;
        }
        _ws.Values[index] = (ushort)value.Value;
        _ws.Save();
        Refresh();
    }
}
