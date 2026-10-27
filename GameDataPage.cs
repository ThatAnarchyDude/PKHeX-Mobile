using PKHeX.Core;

namespace PKHeXMobile;

/// <summary>Gen 4 / Psyduck Yellow extras: badges, coins, Battle Points, Pokewalker, story flags and the Day Care.</summary>
public class GameDataPage : ThemedPage
{
    private static readonly string[] Johto = ["Zephyr", "Hive", "Plain", "Fog", "Storm", "Mineral", "Glacier", "Rising"];
    private static readonly string[] Kanto = ["Boulder", "Cascade", "Thunder", "Rainbow", "Soul", "Marsh", "Volcano", "Earth"];

    private readonly SAV4 _sav;
    private readonly VerticalStackLayout _stack = new() { Spacing = 4, Padding = 12 };

    public static bool IsSupported(SaveFile sav) => sav is SAV4;

    public GameDataPage(SaveFile sav)
    {
        _sav = (SAV4)sav;
        Title = "Game data";
        Content = new ScrollView { Content = _stack };
        Build();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Build(); // the Day Care Pokemon may have been edited
    }

    private void Build()
    {
        _stack.Clear();
        Header("Badges");
        if (_sav is SAV4HGSS hg)
        {
            Header("Johto badges");
            for (int i = 0; i < 8; i++)
                _stack.Add(BitSwitch($"{Johto[i]} Badge", () => (_sav.Badges >> i & 1) == 1, v => _sav.Badges = SetBit(_sav.Badges, i, v)));
            Header("Kanto badges");
            for (int i = 0; i < 8; i++)
                _stack.Add(BitSwitch($"{Kanto[i]} Badge", () => (hg.Badges16 >> i & 1) == 1, v => hg.Badges16 = SetBit((byte)hg.Badges16, i, v)));
        }
        else
        {
            for (int i = 0; i < 8; i++)
                _stack.Add(BitSwitch($"Badge {i + 1}", () => (_sav.Badges >> i & 1) == 1, v => _sav.Badges = SetBit(_sav.Badges, i, v)));
        }

        Header("Progress");
        _stack.Add(BitSwitch("Game cleared", () => _sav.GameClear, v => _sav.GameClear = v));
        _stack.Add(BitSwitch("National Dex unlocked", () => _sav.NationalDex, v => _sav.NationalDex = v));

        Header("Counters");
        _stack.Add(NumberRow("Game Corner coins", (int)_sav.Coin, _sav.MaxCoins, v => _sav.Coin = (uint)v));
        _stack.Add(NumberRow("Battle Points", _sav.BP, 9999, v => _sav.BP = v));
        if (_sav is SAV4HGSS walker)
        {
            _stack.Add(NumberRow("Pokéwalker steps", (int)Math.Min(walker.PokewalkerSteps, int.MaxValue), 99_999_999, v => walker.PokewalkerSteps = (uint)v));
            _stack.Add(NumberRow("Pokéwalker watts", (int)Math.Min(walker.PokewalkerWatts, int.MaxValue), 99_999, v => walker.PokewalkerWatts = (uint)v));
        }

        Header("Day Care");
        var species = Names.Species(_sav);
        for (int i = 0; i < _sav.DaycareSlotCount; i++)
        {
            var slot = i;
            var pk = _sav.GetStoredSlot(_sav.GetDaycareSlot(slot).Span);
            var text = pk.Species == 0 ? $"Slot {slot + 1}: empty" : $"Slot {slot + 1}: {Names.At(species, pk.Species)}  Lv.{pk.CurrentLevel}";
            var row = new Grid { ColumnSpacing = 6, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto), new(GridLength.Auto) } };
            row.Add(new Label { Text = text, VerticalOptions = LayoutOptions.Center });
            var edit = new Button { Text = pk.Species == 0 ? "Add" : "Edit" };
            edit.Clicked += async (_, _) => await EditDaycareAsync(slot);
            row.Add(edit, 1);
            if (pk.Species != 0)
            {
                var clear = new Button { Text = "Remove" };
                clear.Clicked += (_, _) => { _sav.SetDaycareOccupied(slot, false); Build(); };
                row.Add(clear, 2);
            }
            _stack.Add(row);
        }
        _stack.Add(BitSwitch("Egg ready to collect", () => _sav.IsEggAvailable, v => _sav.IsEggAvailable = v));
        _stack.Add(NumberRow("Day Care step counter", _sav.DaycareStepCounter, 255, v => _sav.DaycareStepCounter = (byte)v));

        Theme.Style(_stack);
    }

    private static byte SetBit(byte value, int bit, bool on) => (byte)(on ? value | 1 << bit : value & ~(1 << bit));

    private void Header(string text) => _stack.Add(new Label { Text = text, FontAttributes = FontAttributes.Bold, FontSize = 16, Margin = new Thickness(0, 10, 0, 2) });

    private static View BitSwitch(string label, Func<bool> get, Action<bool> set)
    {
        var sw = new Switch { IsToggled = get(), HorizontalOptions = LayoutOptions.End };
        sw.Toggled += (_, e) => { if (e.Value != get()) set(e.Value); };
        var grid = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) }, MinimumHeightRequest = 40 };
        grid.Add(new Label { Text = label, VerticalOptions = LayoutOptions.Center });
        grid.Add(sw, 1);
        return grid;
    }

    private static View NumberRow(string label, int value, int max, Action<int> set)
    {
        var entry = new Entry { Text = value.ToString(), Keyboard = Keyboard.Numeric, HorizontalTextAlignment = TextAlignment.End, WidthRequest = 130 };
        entry.Unfocused += (_, _) =>
        {
            var v = int.TryParse(entry.Text, out var parsed) ? Math.Clamp(parsed, 0, max) : 0;
            entry.Text = v.ToString();
            set(v);
        };
        var grid = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) }, MinimumHeightRequest = 40 };
        grid.Add(new Label { Text = label, VerticalOptions = LayoutOptions.Center });
        grid.Add(entry, 1);
        return grid;
    }

    private async Task EditDaycareAsync(int slot)
    {
        var span = _sav.GetDaycareSlot(slot).Span;
        var pk = _sav.GetStoredSlot(span);
        if (pk.Species == 0)
        {
            var choice = await DisplayActionSheet("Day Care slot", "Cancel", null, "Add new Pokemon", "Create from Showdown set");
            if (choice == "Add new Pokemon")
            {
                var names = Names.Species(_sav);
                var q = await DisplayPromptAsync("New Pokemon", "Type part of the species name");
                if (string.IsNullOrWhiteSpace(q)) return;
                var matches = Enumerable.Range(1, names.Length - 1).Where(i => names[i].Contains(q, StringComparison.OrdinalIgnoreCase)).Take(12).ToList();
                if (matches.Count == 0) { await DisplayAlertAsync("New Pokemon", "No match.", "OK"); return; }
                var pick = await DisplayActionSheet("Pick species", "Cancel", null, matches.Select(i => names[i]).ToArray());
                var id = matches.FirstOrDefault(i => names[i] == pick);
                if (id == 0) return;
                pk = NewPokemon.Create(_sav, id);
            }
            else if (choice == "Create from Showdown set")
            {
                await Navigation.PushAsync(new ShowdownPage(_sav, p =>
                {
                    SaveDaycare(slot, p);
                    return Task.CompletedTask;
                }));
                return;
            }
            else
                return;
            SaveDaycare(slot, pk);
        }
        await Navigation.PushAsync(new EditPage(_sav, pk, p => SaveDaycare(slot, p)));
    }

    private void SaveDaycare(int slot, PKM pk) => _sav.SetBoxSlot(pk, _sav.GetDaycareSlot(slot).Span);
}
