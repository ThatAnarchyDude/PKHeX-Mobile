using PKHeX.Core;

namespace PKHeXMobile;

/// <summary>Pokedex seen/caught flags per species, with the whole-dex shortcuts the PC editor has.</summary>
public class DexPage : ThemedPage
{
    private readonly SaveFile _sav;
    private readonly Ops? _ops;
    private readonly string[] _names;
    private readonly SearchBar _search = new() { Placeholder = "Search name or number" };
    private readonly Label _summary = new() { FontAttributes = FontAttributes.Bold };
    private readonly CollectionView _list = new() { SelectionMode = SelectionMode.Single };

    private sealed record Row(ushort Species, string Text);

    /// <summary>Whole-dex and per-species operations for the games whose Pokédex is a ZukanBase block (Gen 4 and the Gen 8/9 games).</summary>
    private sealed record Ops(Action SeenAll, Action Complete, Action Clear, Action<ushort> Entry, Action<ushort> Remove);

    private static Ops From<T>(ZukanBase<T> z) where T : SaveFile =>
        new(() => z.SeenAll(), () => z.CompleteDex(), () => { z.CaughtNone(); z.SeenNone(); }, sp => z.SetDexEntryAll(sp), sp => z.ClearDexEntryAll(sp));

    private static Ops? OpsFor(SaveFile sav) => sav switch
    {
        SAV4 s => From(s.Dex),
        SAV8SWSH s => From(s.Zukan),
        SAV8BS s => From(s.Zukan),
        SAV9SV s => From(s.Zukan),
        SAV9ZA s => From(s.Zukan),
        _ => null,
    };

    /// <summary>True when the open save has a Pokedex this editor can read and change.</summary>
    public static bool IsSupported(SaveFile sav) => sav is SAV4 or SAV1 or SAV2 or SAV3 or SAV5 or SAV6 or SAV7 or SAV7b or SAV8SWSH or SAV8BS or SAV9SV or SAV9ZA;

    public DexPage(SaveFile sav)
    {
        _sav = sav;
        _ops = OpsFor(sav);
        _names = Names.Species(sav);
        Title = "Pokédex";

        var seenAll = new Button { Text = "Seen all" };
        seenAll.Clicked += async (_, _) => await BulkAsync("Mark every species as seen?", () => SeenAll());
        var complete = new Button { Text = "Complete dex" };
        complete.Clicked += async (_, _) => await BulkAsync("Mark every species as seen and caught?", () => CompleteAll());
        var clear = new Button { Text = "Clear all" };
        clear.Clicked += async (_, _) => await BulkAsync("Clear the whole Pokédex? This removes every seen and caught flag.", () => ClearAll());

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
            await EditAsync(row.Species);
        };

        Content = new Grid
        {
            Padding = 8, RowSpacing = 6,
            RowDefinitions = { new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Star) },
            Children =
            {
                _summary,
                InRow(new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, Children = { seenAll, complete, clear } }, 1),
                InRow(_search, 2),
                InRow(_list, 3),
            }
        };
        Refresh();
    }

    private static T InRow<T>(T v, int row) where T : View { Grid.SetRow(v, row); return v; }

    private IEnumerable<ushort> Species()
    {
        var max = Math.Min(_sav.MaxSpeciesID, _names.Length - 1);
        for (ushort s = 1; s <= max; s++)
            if (!string.IsNullOrWhiteSpace(_names[s]) && _names[s] != "-----")
                yield return s;
    }

    private void Refresh()
    {
        var q = _search.Text?.Trim() ?? string.Empty;
        int seen = 0, caught = 0, total = 0;
        var rows = new List<Row>();
        foreach (var s in Species())
        {
            total++;
            bool sn = _sav.GetSeen(s), ct = _sav.GetCaught(s);
            if (sn) seen++;
            if (ct) caught++;
            var name = _names[s];
            if (q.Length != 0 && !name.Contains(q, StringComparison.OrdinalIgnoreCase) && !s.ToString().Contains(q))
                continue;
            rows.Add(new Row(s, $"#{s:000}  {name}   {(sn ? "● seen" : "○ not seen")}   {(ct ? "● caught" : "○ not caught")}"));
        }
        _summary.Text = $"Seen {seen} / {total}    Caught {caught} / {total}";
        _list.ItemsSource = rows;
    }

    private async Task EditAsync(ushort species)
    {
        // Gen 8/9 has no "seen only" setter per species, so that choice is only offered where it exists.
        var canSeenOnly = _sav is not (SAV8SWSH or SAV8BS or SAV9SV or SAV9ZA);
        var choice = await (canSeenOnly
            ? DisplayActionSheet($"#{species} {_names[species]}", "Cancel", null, "Seen and caught", "Seen only", "Not seen")
            : DisplayActionSheet($"#{species} {_names[species]}", "Cancel", null, "Seen and caught", "Not seen"));
        switch (choice)
        {
            case "Seen and caught": SetEntry(species, true, true); break;
            case "Seen only": SetEntry(species, true, false); break;
            case "Not seen": SetEntry(species, false, false); break;
            default: return;
        }
        Refresh();
    }

    private void SetEntry(ushort species, bool seen, bool caught)
    {
        if (_ops is { } ops)
        {
            // These dexes keep genders, forms and languages in step when an entry is completed or cleared.
            if (seen && caught)
                ops.Entry(species);
            else
            {
                ops.Remove(species);
                if (seen && _sav is SAV4 s4)
                    s4.Dex.SetSeen(species);
            }
            return;
        }
        _sav.SetCaught(species, caught);
        _sav.SetSeen(species, seen);
    }

    private async Task BulkAsync(string question, Action action)
    {
        if (!await DisplayAlertAsync("Pokédex", question, "Yes", "No"))
            return;
        action();
        Refresh();
    }

    private void SeenAll()
    {
        if (_ops is { } ops) ops.SeenAll();
        else foreach (var s in Species()) _sav.SetSeen(s, true);
    }

    private void CompleteAll()
    {
        if (_ops is { } ops) ops.Complete();
        else foreach (var s in Species()) { _sav.SetSeen(s, true); _sav.SetCaught(s, true); }
    }

    private void ClearAll()
    {
        if (_ops is { } ops) ops.Clear();
        else foreach (var s in Species()) { _sav.SetCaught(s, false); _sav.SetSeen(s, false); }
    }
}
