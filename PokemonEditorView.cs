using PKHeX.Core;

namespace PKHeXMobile;

/// <summary>Pokemon editor laid out like the PC PKHeX one: a sprite header and Main / Met / Stats / Moves / Cosmetic / Tools tabs.</summary>
public class PokemonEditorView : ContentView
{
    private static readonly string[] TabNames = ["Main", "Met", "Stats", "Moves", "Cosmetic", "Tools"];

    private readonly SaveFile _sav;
    private readonly PKM _pk; // working copy; only written to the save by "Apply to save"
    private readonly int _box, _index;
    private readonly PropertyEditor _editor;
    private readonly ContentView _tabHost = new() { Padding = new Thickness(0, 0, 12, 0) }; // right gutter keeps the last pixels of a row clear of the pane edge
    private readonly ScrollView _tabScroll = new();
    private readonly List<Button> _tabButtons = [];
    private readonly List<BoxView> _tabAccents = [];
    private static readonly Color[] TabColors = [Color.FromArgb("#F0A060"), Color.FromArgb("#8A8AF0"), Color.FromArgb("#F090D0"), Color.FromArgb("#80E080"), Color.FromArgb("#F0E040"), Color.FromArgb("#C09090")];
    private readonly Image _sprite = new() { WidthRequest = 72, HeightRequest = 60, Aspect = Aspect.AspectFit, HorizontalOptions = LayoutOptions.Center };
    private readonly Image _ballImage = new() { WidthRequest = 28, HeightRequest = 28, HorizontalOptions = LayoutOptions.Start };
    private readonly Label _title = new() { FontSize = 20, FontAttributes = FontAttributes.Bold };
    private readonly Label _subtitle = new();
    private readonly Label _legal = new() { FontAttributes = FontAttributes.Bold };
    private int _tab;
    private bool _shown;

    private readonly Page _host;
    private readonly Action<PKM>? _writer; // set when the Pokemon lives outside the party/boxes (for example the Day Care)
    private readonly Action _onFinished;

    /// <param name="host">The page that shows dialogs and pushes screens for this editor.</param>
    /// <param name="writer">Saves the Pokemon somewhere other than a party/box slot (Day Care); null for party/box slots.</param>
    /// <param name="onFinished">Called after "Apply to save", "Discard changes" or an import.</param>
    public PokemonEditorView(Page host, SaveFile sav, PKM pk, int box, int index, Action<PKM>? writer, Action onFinished)
    {
        (_host, _sav, _box, _index, _writer, _onFinished) = (host, sav, box, index, writer, onFinished);        _pk = pk.Clone();
        _editor = new PropertyEditor(host, _pk, sav);

        // Left column like the PC editor: sprite on top, then one coloured-accent button per tab.
        var left = new VerticalStackLayout { Spacing = 2, WidthRequest = 76 };
        left.Add(_sprite);
        for (int i = 0; i < TabNames.Length; i++)
        {
            var index2 = i;
            var accent = new BoxView { Color = Colors.Transparent, WidthRequest = 6 };
            _tabAccents.Add(accent);
            var b = new Button { Text = TabNames[i], CornerRadius = 0, HeightRequest = 46, Padding = 0, FontSize = 13 };
            b.Clicked += async (_, _) => await SelectTabAsync(index2);
            _tabButtons.Add(b);
            left.Add(new Grid { ColumnSpacing = 0, ColumnDefinitions = { new(new GridLength(6)), new(GridLength.Star) }, Children = { accent, Column(b, 1) } });
        }

        // Two fixed lines (a wrapping FlexLayout under-measured its height and let the legality tag overlap the fields).
        foreach (var v in new View[] { _title, _subtitle, _ballImage, _legal })
        {
            v.Margin = new Thickness(0, 0, 10, 0);
            v.VerticalOptions = LayoutOptions.Center;
        }
        _ballImage.WidthRequest = _ballImage.HeightRequest = 22;
        _subtitle.LineBreakMode = LineBreakMode.TailTruncation;
        _title.LineBreakMode = LineBreakMode.TailTruncation;
        var headline = new VerticalStackLayout
        {
            Spacing = 0,
            Children =
            {
                new Grid { ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) }, Children = { _title, Column(_subtitle, 1) } },
                new HorizontalStackLayout { Children = { _ballImage, _legal } },
            },
        };
        _title.FontSize = 18;
        _legal.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => await ShowLegalityAsync()) });

        var apply = new Button { Text = "Apply to save" };
        apply.Clicked += async (_, _) => { Apply(); _onFinished(); };
        var cancel = new Button { Text = "Discard changes" };
        cancel.Clicked += (_, _) => _onFinished();

        _tabScroll.Content = _tabHost;
        _tabScroll.SizeChanged += (_, _) => { if (_tabScroll.Width > 0) _tabHost.WidthRequest = _tabScroll.Width; };
        var right = new Grid
        {
            RowSpacing = 4,
            RowDefinitions = { new(GridLength.Auto), new(GridLength.Star) },
            Children = { headline, Row(_tabScroll, 1) }
        };

        Content = new Grid
        {
            Padding = 6, RowSpacing = 6, ColumnSpacing = 6,
            ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) },
            RowDefinitions = { new(GridLength.Star), new(GridLength.Auto) },
            Children =
            {
                new ScrollView { Content = left },
                Column(right, 1),
            }
        };
        var bottom = new Grid { ColumnSpacing = 6, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star) }, Children = { apply, Column(cancel, 1) } };
        Grid.SetRow(bottom, 1);
        Grid.SetColumnSpan(bottom, 2);
        ((Grid)Content).Add(bottom);
        _ = SelectTabAsync(0);
    }

    /// <summary>Tightens the left tab column and sprite when the phone is turned sideways so more fields fit.</summary>
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        var landscape = width > height;
        _sprite.IsVisible = !landscape; // the header already names the species; the room is better spent on the tab buttons
        _sprite.WidthRequest = 72;
        _sprite.HeightRequest = 60;
        foreach (var b in _tabButtons)
        {
            b.HeightRequest = landscape ? 30 : 46;
            b.FontSize = landscape ? 12 : 13;
        }
    }

    private static T Row<T>(T v, int row) where T : View { Grid.SetRow(v, row); return v; }
    private static T Column<T>(T v, int column) where T : View { Grid.SetColumn(v, column); return v; }

    /// <summary>Call when the host page reappears: a child screen (ribbons, all fields, lists) may have changed the Pokemon.</summary>
    public void Reload()
    {
        if (!_shown) { _shown = true; return; }
        // Returned from a child page (ribbons, all fields, lists) that may have changed the Pokemon.
        _ = SelectTabAsync(_tab);
    }

    #region Tabs

    private async Task SelectTabAsync(int tab)
    {
        _tab = tab;
        _editor.ClearCache();
        for (int i = 0; i < _tabButtons.Count; i++)
            _tabButtons[i].FontAttributes = i == tab ? FontAttributes.Bold : FontAttributes.None;
        for (int i = 0; i < _tabAccents.Count; i++)
            _tabAccents[i].Color = i == tab ? TabColors[i] : Colors.Transparent;

        View view = tab switch
        {
            0 => FieldsTab("Species", "Nickname", "IsNicknamed", "EXP", "CurrentLevel", "HeldItem", "Ability", "Form", "Gender", "CurrentFriendship", "PokerusStrain", "PokerusDays"),
            1 => FieldsTab("OriginalTrainerName", "TID16", "SID16", "OriginalTrainerGender", "Version", "Language", "Ball", "MetLocation", "MetLevel", "MetDate", "FatefulEncounter", "GroundTile", "IsEgg", "EggLocation", "EggMetDate"),
            2 => StatsTab(),
            3 => MovesTab(),
            4 => CosmeticTab(),
            _ => ToolsTab(),
        };
        Theme.Style(view);
        _tabHost.Content = view;
        await UpdateHeaderAsync();
    }

    private VerticalStackLayout FieldsTab(params string[] props)
    {
        var stack = new VerticalStackLayout { Spacing = 2 };
        if (_tab == 0)
        {
            stack.Add(PidRow());
            stack.Add(new Label { Text = $"Nature: {_pk.Nature}   (tap the star to toggle shiny, the gender icon to flip gender, Reroll for a new PID)", FontSize = 12 });
        }
        foreach (var name in props)
            if (FieldRow(name) is { } row)
                stack.Add(row);
        return stack;
    }

    /// <summary>A label-and-value row for one property: an inline box, a switch, or a tap-to-pick named list.</summary>
    private View? FieldRow(string prop, string? label = null)
    {
        var p = _editor.Prop(prop);
        if (p is null || !p.CanRead || !p.CanWrite)
            return null;

        var grid = new Grid { ColumnDefinitions = { new(new GridLength(0.42, GridUnitType.Star)), new(new GridLength(0.58, GridUnitType.Star)) }, MinimumHeightRequest = 40 };
        grid.Add(new Label { Text = label ?? Pretty(prop), VerticalOptions = LayoutOptions.Center });

        View value;
        if (p.PropertyType == typeof(bool))
        {
            var sw = new Switch { IsToggled = (bool)p.GetValue(_pk)!, HorizontalOptions = LayoutOptions.Start };
            sw.Toggled += async (_, e) =>
            {
                if (e.Value == (bool)p.GetValue(_pk)!) return;
                p.SetValue(_pk, e.Value);
                await ChangedAsync(prop);
            };
            value = sw;
        }
        else if (_editor.IsInlineText(p))
        {
            var entry = new Entry
            {
                Text = _editor.Read(p),
                Keyboard = p.PropertyType == typeof(string) ? Keyboard.Text : Keyboard.Numeric,
            };
            entry.Unfocused += async (_, _) =>
            {
                if (entry.Text == _editor.Read(p)) return;
                if (_editor.TrySetText(p, entry.Text))
                    await ChangedAsync(prop);
                else
                    entry.Text = _editor.Read(p);
            };
            value = entry;
        }
        else
        {
            var text = new Label { Text = _editor.Read(p), VerticalOptions = LayoutOptions.Center, TextDecorations = TextDecorations.Underline };
            text.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () =>
                {
                    if (await _editor.EditAsync(p))
                        await ChangedAsync(prop);
                })
            });
            value = text;
        }
        grid.Add(value, 1);
        return grid;
    }

    private static string Pretty(string name) => name switch
    {
        "CurrentLevel" => "Level",
        "EXP" => "Exp. Points",
        "HeldItem" => "Held Item",
        "CurrentFriendship" => "Friendship",
        "OriginalTrainerName" => "OT",
        "TID16" => "TID",
        "SID16" => "SID",
        "OriginalTrainerGender" => "OT Gender",
        "Version" => "Origin Game",
        "MetLocation" => "Met Location",
        "MetLevel" => "Met Level",
        "MetDate" => "Met Date",
        "EggLocation" => "Egg Location",
        "EggMetDate" => "Egg Date",
        "FatefulEncounter" => "Fateful Encounter",
        "GroundTile" => "Encounter Type",
        "IsEgg" => "Is Egg",
        "IsNicknamed" => "Nicknamed",
        "PokerusStrain" => "Pok\u00E9rus Strain",
        "PokerusDays" => "Pok\u00E9rus Days",
        _ => System.Text.RegularExpressions.Regex.Replace(name, "(?<!^)([A-Z])", " $1"),
    };

    /// <summary>Applies the side effects the PC editor performs when a field changes.</summary>
    private async Task ChangedAsync(string prop)
    {
        switch (prop)
        {
            case "Species":
                _pk.RefreshAbility(0);
                if (!_pk.IsNicknamed)
                    _pk.Nickname = Names.At(Names.Species(_sav), _pk.Species);
                break;
            case "Nickname":
                _pk.IsNicknamed = !string.Equals(_pk.Nickname, Names.At(Names.Species(_sav), _pk.Species), StringComparison.OrdinalIgnoreCase);
                break;
            case "Move1": case "Move2": case "Move3": case "Move4":
            {
                var n = prop[^1] - '1';
                _pk.GetType().GetProperty($"{prop}_PPUps")?.SetValue(_pk, (byte)0);
                _pk.HealPPIndex(n);
                break;
            }
        }
        _pk.RefreshChecksum();
        await SelectTabAsync(_tab);
    }

    /// <summary>PC-style Stats tab: base stats (heat-coloured), IVs (green when hyper trained), EVs, final stats, totals, Hidden Power and Characteristic.</summary>
    private View StatsTab()
    {
        string[] names = ["HP", "Atk", "Def", "SpA", "SpD", "Spe"];
        var pi = _pk.PersonalInfo;
        int[] baseStats = [pi.HP, pi.ATK, pi.DEF, pi.SPA, pi.SPD, pi.SPE];
        var shown = _pk.Clone();
        shown.ResetPartyStats();
        int[] stats = [shown.Stat_HPMax, shown.Stat_ATK, shown.Stat_DEF, shown.Stat_SPA, shown.Stat_SPD, shown.Stat_SPE];
        int[] ivs = [_pk.IV_HP, _pk.IV_ATK, _pk.IV_DEF, _pk.IV_SPA, _pk.IV_SPD, _pk.IV_SPE];
        int[] evs = [_pk.EV_HP, _pk.EV_ATK, _pk.EV_DEF, _pk.EV_SPA, _pk.EV_SPD, _pk.EV_SPE];
        var ht = _pk as IHyperTrain;
        bool[] trained = ht is null ? new bool[6] : [ht.HT_HP, ht.HT_ATK, ht.HT_DEF, ht.HT_SPA, ht.HT_SPD, ht.HT_SPE];

        var grid = new Grid
        {
            ColumnSpacing = 3, RowSpacing = 4,
            ColumnDefinitions =
            {
                new(new GridLength(40)), new(GridLength.Star), new(GridLength.Star), new(GridLength.Star), new(GridLength.Star),
            },
        };
        string[] heads = ["", "Base", "IVs", "EVs", "Stats"];
        grid.RowDefinitions.Add(new(GridLength.Auto));
        for (int c = 1; c < heads.Length; c++)
            grid.Add(new Label { Text = heads[c], HorizontalTextAlignment = TextAlignment.Center, FontSize = 12 }, c, 0);

        var ivEntries = new LongPressEntry[6];
        var evEntries = new Entry[6];
        
        for (int i = 0; i < 6; i++)
        {
            grid.RowDefinitions.Add(new(GridLength.Auto));
            grid.Add(new Label { Text = names[i] + ":", VerticalOptions = LayoutOptions.Center, HorizontalTextAlignment = TextAlignment.End }, 0, i + 1);

            // base stat tile, coloured from low (orange) to high (green) like the PC editor
            grid.Add(StatTile($"{baseStats[i]:000}", HeatColor(baseStats[i])), 1, i + 1);

            ivEntries[i] = new LongPressEntry { Text = ivs[i].ToString(), Keyboard = Keyboard.Numeric, HorizontalTextAlignment = TextAlignment.Center, FontSize = 13 };
            if (trained[i])
                ivEntries[i].BackgroundColor = Color.FromArgb("#8AE88A");
            evEntries[i] = new Entry { Text = evs[i].ToString(), Keyboard = Keyboard.Numeric, HorizontalTextAlignment = TextAlignment.Center, FontSize = 13 };
            grid.Add(ivEntries[i], 2, i + 1);
            grid.Add(evEntries[i], 3, i + 1);
            grid.Add(StatTile(stats[i].ToString(), Color.FromArgb("#E4E4E4")), 4, i + 1);

            var index = i;
            ivEntries[i].LongPressed += async (_, _) =>
            {
                if (_pk is not IHyperTrain h) return;
                switch (index)
                {
                    case 0: h.HT_HP = !h.HT_HP; break;
                    case 1: h.HT_ATK = !h.HT_ATK; break;
                    case 2: h.HT_DEF = !h.HT_DEF; break;
                    case 3: h.HT_SPA = !h.HT_SPA; break;
                    case 4: h.HT_SPD = !h.HT_SPD; break;
                    default: h.HT_SPE = !h.HT_SPE; break;
                }
                _pk.RefreshChecksum();
                try { HapticFeedback.Default.Perform(HapticFeedbackType.LongPress); } catch (Exception) { /* no haptics */ }
                await SelectTabAsync(_tab);
            };
        }

        // totals row
        grid.RowDefinitions.Add(new(GridLength.Auto));
        grid.Add(new Label { Text = "Total:", VerticalOptions = LayoutOptions.Center, HorizontalTextAlignment = TextAlignment.End }, 0, 7);
        grid.Add(StatTile(baseStats.Sum().ToString("000"), HeatColor(baseStats.Sum() / 5)), 1, 7);
        grid.Add(StatTile(ivs.Sum().ToString(), Color.FromArgb("#E4E4E4")), 2, 7);
        grid.Add(StatTile(evs.Sum().ToString(), evs.Sum() > 510 ? Color.FromArgb("#F4A0A0") : Color.FromArgb("#E4E4E4")), 3, 7);
        grid.Add(new Label { Text = new string('\u2605', _pk.PotentialRating + 1), VerticalOptions = LayoutOptions.Center, FontSize = 14 }, 4, 7);

        async Task CommitAsync()
        {
            static int Num(Entry e, int max) => int.TryParse(e.Text, out var v) ? Math.Clamp(v, 0, max) : 0;
            // PC order is HP, Atk, Def, SpA, SpD, Spe; the core's arrays use HP, Atk, Def, Spe, SpA, SpD.
            int[] iv = [Num(ivEntries[0], 31), Num(ivEntries[1], 31), Num(ivEntries[2], 31), Num(ivEntries[5], 31), Num(ivEntries[3], 31), Num(ivEntries[4], 31)];
            int[] ev = [Num(evEntries[0], 252), Num(evEntries[1], 252), Num(evEntries[2], 252), Num(evEntries[5], 252), Num(evEntries[3], 252), Num(evEntries[4], 252)];
            _pk.SetIVs(iv);
            _pk.SetEVs(ev);
            _pk.RefreshChecksum();
            await SelectTabAsync(_tab);
        }
        foreach (var e in ivEntries.Concat(evEntries))
            e.Unfocused += async (_, _) => await CommitAsync();

        var stack = new VerticalStackLayout { Spacing = 6 };
        stack.Add(grid);
        if (ht is null)
            stack.Add(new Label { Text = "(This game has no Hyper Training.)", FontSize = 11 });

        // Hidden Power type + power, characteristic
        var types = GameInfo.Strings.Types.Skip(1).Take(16).ToArray(); // Fighting .. Dark (the Hidden Power types)
        var hpRow = new HorizontalStackLayout { Spacing = 8 };
        hpRow.Add(new Label { Text = "Hidden Power Type:", VerticalOptions = LayoutOptions.Center });
        var hpType = new Label { Text = _pk.HPType < types.Length ? types[_pk.HPType] : "?", VerticalOptions = LayoutOptions.Center, TextDecorations = TextDecorations.Underline };
        hpType.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () =>
            {
                var items = types.Select((t, i) => new ComboItem(t, i)).ToList();
                var page = new ChoicePage("Hidden Power Type", items, _pk.HPType);
                await _host.Navigation.PushAsync(page);
                if (await page.Result is not { } picked) return;
                _pk.HPType = picked;
                _pk.RefreshChecksum();
                await SelectTabAsync(_tab);
            })
        });
        hpRow.Add(hpType);
        hpRow.Add(new Label { Text = _pk.HPPower.ToString(), VerticalOptions = LayoutOptions.Center });
        stack.Add(hpRow);

        var chars = GameInfo.Strings.characteristics;
        stack.Add(new Label { Text = $"Characteristic:  {(_pk.Characteristic >= 0 && _pk.Characteristic < chars.Length ? chars[_pk.Characteristic] : "")}" });

        var buttons = new HorizontalStackLayout { Spacing = 8 };
        var rIv = new Button { Text = "Randomize IVs" };
        rIv.Clicked += async (_, _) =>
        {
            var rnd = Random.Shared;
            _pk.SetIVs([rnd.Next(32), rnd.Next(32), rnd.Next(32), rnd.Next(32), rnd.Next(32), rnd.Next(32)]);
            _pk.RefreshChecksum();
            await SelectTabAsync(_tab);
        };
        var rEv = new Button { Text = "Randomize EVs" };
        rEv.Clicked += async (_, _) =>
        {
            var rnd = Random.Shared;
            int[] ev = new int[6];
            for (int left = 510; left > 0;) // spread up to 510 with at most 252 per stat
            {
                var s = rnd.Next(6);
                var add = Math.Min(Math.Min(rnd.Next(1, 64), left), 252 - ev[s]);
                ev[s] += add;
                left -= add;
                if (ev.All(v => v >= 252)) break;
            }
            _pk.SetEVs(ev);
            _pk.RefreshChecksum();
            await SelectTabAsync(_tab);
        };
        buttons.Add(rIv);
        buttons.Add(rEv);
        stack.Add(buttons);

        foreach (var name in new[] { "ContestCool", "ContestBeauty", "ContestCute", "ContestSmart", "ContestTough", "ContestSheen" })
            if (FieldRow(name, Pretty(name).Replace("Contest ", "Contest: ")) is { } row)
                stack.Add(row);
        return stack;
    }

    private static Border StatTile(string text, Color back) => new()
    {
        BackgroundColor = back,
        Stroke = new SolidColorBrush(Color.FromArgb("#808080")),
        StrokeThickness = 1,
        Padding = new Thickness(2, 6),
        Content = new Label { Text = text, TextColor = Colors.Black, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center, FontSize = 14 },
    };

    /// <summary>Pastel colour from orange (low) through yellow to green (high) for a base stat value.</summary>
    private static Color HeatColor(int value)
    {
        var t = Math.Clamp(value / 140f, 0f, 1f);
        return Color.FromHsla(0.08f + 0.25f * t, 0.65f, 0.78f);
    }

    private View MovesTab()
    {
        var stack = new VerticalStackLayout { Spacing = 2 };
        for (int i = 1; i <= 4; i++)
        {
            var typeIcon = new Image { WidthRequest = 28, HeightRequest = 28, VerticalOptions = LayoutOptions.Center };
            _ = ShowMoveTypeAsync(typeIcon, i);
            stack.Add(new HorizontalStackLayout
            {
                Spacing = 8, Margin = new Thickness(0, 8, 0, 0),
                Children = { typeIcon, new Label { Text = $"Move {i}", FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center } }
            });
            foreach (var (prop, label) in new[] { ($"Move{i}", "Move"), ($"Move{i}_PP", "PP"), ($"Move{i}_PPUps", "PP Ups") })
                if (FieldRow(prop, label) is { } row)
                    stack.Add(row);
        }
        var heal = new Button { Text = "Heal PP" };
        heal.Clicked += async (_, _) => { _pk.HealPP(); await SelectTabAsync(_tab); };
        stack.Add(heal);
        return stack;
    }

    /// <summary>Shows the move's type icon next to its name, like the PC Moves tab.</summary>
    private async Task ShowMoveTypeAsync(Image icon, int slot)
    {
        ushort move = slot switch { 1 => _pk.Move1, 2 => _pk.Move2, 3 => _pk.Move3, _ => _pk.Move4 };
        if (move == 0) return;
        byte type;
        try
        {
            type = _sav.Version == GameVersion.PY && move < PsyduckYellowExpanded.MoveTypes.Length
                ? PsyduckYellowExpanded.MoveTypes[move]
                : MoveInfo.GetType(move, _pk.Context);
        }
        catch (Exception) { return; }
        icon.Source = await Sprites.FileAsync($"types/type_icon_{type:00}.png");
    }

    /// <summary>PC-style PID line: shiny star (tap to toggle), the PID in hex, a gender icon (tap to flip) and a Reroll button.</summary>
    private View PidRow()
    {
        var pk = _pk;
        var star = new Image { WidthRequest = 24, HeightRequest = 24, VerticalOptions = LayoutOptions.Center, Opacity = pk.IsShiny ? 1 : 0.25 };
        _ = SetImageAsync(star, "ui/rare_icon.png");
        star.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => await RebuildPidAsync(shiny: !pk.IsShiny)) });

        var pid = new Entry { Text = pk.PID.ToString("X8"), MaxLength = 8, Keyboard = Keyboard.Text, FontFamily = "monospace", FontSize = 14, VerticalOptions = LayoutOptions.Center, MinimumWidthRequest = 60 };
        pid.Unfocused += async (_, _) =>
        {
            if (!uint.TryParse(pid.Text, System.Globalization.NumberStyles.HexNumber, null, out var value) || value == pk.PID)
            {
                pid.Text = pk.PID.ToString("X8");
                return;
            }
            pk.PID = value;
            var ratio = pk.PersonalInfo.Gender;
            if (ratio is not (0 or 254 or 255))
                pk.Gender = EntityGender.GetFromPIDAndRatio(value, ratio);
            await ChangedAsync("PID");
        };

        var gender = new Image { WidthRequest = 24, HeightRequest = 24, VerticalOptions = LayoutOptions.Center };
        _ = SetImageAsync(gender, $"ui/gender_{Math.Min(pk.Gender, (byte)2)}.png");
        gender.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () =>
            {
                if (pk.PersonalInfo.Gender is 0 or 254 or 255) return; // single-gender species can't flip
                await RebuildPidAsync(gender: (byte)(pk.Gender == 0 ? 1 : 0));
            })
        });

        var reroll = new Button { Text = "Reroll", Padding = new Thickness(4, 0), FontSize = 12, HeightRequest = 34, MinimumWidthRequest = 0, VerticalOptions = LayoutOptions.Center };
        reroll.Clicked += async (_, _) => await RebuildPidAsync();

        // Star-width entry so the Reroll button always stays inside a narrow (landscape pane) editor.
        return new Grid
        {
            ColumnSpacing = 4,
            ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto), new(GridLength.Auto) },
            Children = { star, Column(pid, 1), Column(gender, 2), Column(reroll, 3) },
        };
    }

    private static async Task SetImageAsync(Image image, string file) => image.Source = await Sprites.FileAsync(file);

    /// <summary>Builds a new PID that keeps nature, ability slot and (unless changed) gender and shininess, like the PC Reroll.</summary>
    private async Task RebuildPidAsync(bool? shiny = null, byte? gender = null)
    {
        var pk = _pk;
        var slot = Math.Clamp(System.Numerics.BitOperations.TrailingZeroCount(Math.Max(pk.AbilityNumber, 1)), 0, 2);
        if (PidTools.Recalculate(pk, gender ?? pk.Gender, pk.Nature, slot, shiny ?? pk.IsShiny))
            await ChangedAsync("PID");
        else
            await _host.DisplayAlertAsync("PID", "No PID matched that combination.", "OK");
    }

    private View CosmeticTab()
    {
        var stack = new VerticalStackLayout { Spacing = 2 };
        foreach (var name in new[] { "MarkingCircle", "MarkingTriangle", "MarkingSquare", "MarkingHeart", "MarkingStar", "MarkingDiamond" })
            if (FieldRow(name, Pretty(name).Replace("Marking ", "Marking: ")) is { } row)
                stack.Add(row);
        var ribbons = new Button { Text = "Ribbons\u2026" };
        ribbons.Clicked += async (_, _) => await _host.Navigation.PushAsync(new RibbonsPage(_pk));
        stack.Add(ribbons);
        return stack;
    }

    private View ToolsTab()
    {
        var stack = new VerticalStackLayout { Spacing = 6 };
        foreach (var (text, action) in new (string, Func<Task>)[]
        {
            ("Check legality", ShowLegalityAsync),
            ("Legalize (auto-fix)", LegalizeAsync),
            ("Copy as Showdown text", CopyShowdownAsync),
            ("Recalculate PID", RecalculatePidAsync),
            ("Copy encounter data", CopyEncounterAsync),
            ("All fields (advanced)", async () => await _host.Navigation.PushAsync(new AdvancedPage(_sav, _pk))),
            ("Export .pk file", ExportPkAsync),
            ("Import .pk file into this slot", ImportPkAsync),
        })
        {
            var b = new Button { Text = text, LineBreakMode = LineBreakMode.WordWrap };
            b.Clicked += async (_, _) => await action();
            stack.Add(b);
        }
        return stack;
    }

    #endregion

    #region Header

    private async Task UpdateHeaderAsync()
    {
        var species = Names.Species(_sav);
        var name = _pk.IsNicknamed && !string.IsNullOrWhiteSpace(_pk.Nickname) ? $"{_pk.Nickname} ({Names.At(species, _pk.Species)})" : Names.At(species, _pk.Species);
        _title.Text = name;
        var gender = _pk.Gender switch { 0 => "\u2642", 1 => "\u2640", _ => "\u2013" };
        var item = _pk.HeldItem == 0 ? "no item" : Names.At(Names.Items(_sav), _pk.HeldItem);
        _subtitle.Text = $"Lv.{_pk.CurrentLevel}  {gender}  {(_pk.IsShiny ? "\u2605 " : "")}{item}";
        _sprite.Source = await Sprites.PokemonAsync(_pk, _sav);
        _ballImage.Source = await Sprites.BallAsync(_pk.Ball);

        try
        {
            var la = new LegalityAnalysis(_pk, _sav.Personal);
            _legal.Text = la.Valid ? "\u2714 Legal" : "\u2718 Illegal (tap for report)";
            _legal.TextColor = la.Valid ? Colors.Green : Colors.Red;
        }
        catch (Exception)
        {
            _legal.Text = string.Empty;
        }
    }

    #endregion

    #region Tools

    private async Task ShowLegalityAsync()
    {
        var la = new LegalityAnalysis(_pk, _sav.Personal);
        await _host.DisplayAlertAsync(la.Valid ? "Legal" : "Illegal", la.Report(), "OK");
    }

    /// <summary>Asks the Auto-Legality library to rebuild this Pokemon as a legal one; keeps the current Pokemon if that is not possible.</summary>
    private async Task LegalizeAsync()
    {
        if (new LegalityAnalysis(_pk, _sav.Personal).Valid)
        {
            await _host.DisplayAlertAsync("Legalize", "This Pokemon is already legal.", "OK");
            return;
        }

        PKM? result = null;
        try
        {
            var current = _pk;
            result = await Task.Run(() => PKHeX.Core.AutoMod.Legalizer.Legalize(_sav, current));
        }
        catch (Exception ex)
        {
            await _host.DisplayAlertAsync("Legalize", $"Could not legalize: {ex.Message}", "OK");
            return;
        }

        var la = new LegalityAnalysis(result, _sav.Personal);
        if (!la.Valid || result.GetType() != _pk.GetType() || result.Species == 0)
        {
            await _host.DisplayAlertAsync("Legalize", $"A legal version could not be built, so nothing was changed.\n\n{la.Report()}", "OK");
            return;
        }
        result.Data.CopyTo(_pk.Data);
        _pk.RefreshChecksum();
        await SelectTabAsync(_tab);
        await _host.DisplayAlertAsync("Legalize", "Rebuilt as a legal Pokemon. Review the fields, then Apply to save.", "OK");
    }

    private async Task CopyShowdownAsync()
    {
        var text = ShowdownParsing.GetShowdownText(_pk);
        await Clipboard.SetTextAsync(text);
        await _host.DisplayAlertAsync("Showdown text", $"Copied to the clipboard:\n\n{text}", "OK");
    }

    /// <summary>Copies game, met data, ball, ground tile, egg data and trainer identity from a chosen party or box Pokemon.</summary>
    private async Task CopyEncounterAsync()
    {
        var species = Names.Species(_sav);
        var items = new List<ComboItem>();
        var party = _sav.PartyData;
        for (int i = 0; i < party.Count; i++)
            if (party[i].Species != 0)
                items.Add(new($"Party {i + 1}: {Names.At(species, party[i].Species)} Lv.{party[i].CurrentLevel}", i));
        int per = _sav.BoxSlotCount;
        for (int b = 0; b < _sav.BoxCount; b++)
        {
            var box = _sav.GetBoxData(b);
            for (int s = 0; s < box.Length; s++)
                if (box[s].Species != 0)
                    items.Add(new($"Box {b + 1} slot {s + 1}: {Names.At(species, box[s].Species)} Lv.{box[s].CurrentLevel}", 1000 + b * per + s));
        }
        if (items.Count == 0)
        {
            await _host.DisplayAlertAsync("Copy encounter data", "There are no other Pokemon in this save to copy from.", "OK");
            return;
        }

        var page = new ChoicePage("Copy encounter data from", items, -1);
        await _host.Navigation.PushAsync(page);
        if (await page.Result is not { } code) return;
        var source = code < 1000 ? party[code] : _sav.GetBoxData((code - 1000) / per)[(code - 1000) % per];
        if (source.GetType() != _pk.GetType())
        {
            await _host.DisplayAlertAsync("Copy encounter data", "Pick a Pokemon of the same game format.", "OK");
            return;
        }
        EncounterCopy.Apply(_pk, source);
        await SelectTabAsync(_tab);
        await _host.DisplayAlertAsync("Copy encounter data", $"Copied from {Names.At(species, source.Species)}. Check the Legal indicator at the top.", "OK");
    }

    /// <summary>Asks for gender, nature, ability slot and shininess, then builds a PID the game would accept for that combination.</summary>
    private async Task RecalculatePidAsync()
    {
        var pk = _pk;
        var genders = pk.PersonalInfo.Gender switch
        {
            255 => new[] { "Genderless" },
            254 => new[] { "Female" },
            0 => new[] { "Male" },
            _ => new[] { "Male", "Female" },
        };
        var current = pk.Gender switch { 0 => "Male", 1 => "Female", _ => "Genderless" };
        var genderName = genders.Length == 1 ? genders[0]
            : await _host.DisplayActionSheet($"Gender (now {current})", "Cancel", null, genders);
        if (genderName is null or "Cancel") return;
        byte gender = genderName switch { "Male" => 0, "Female" => 1, _ => 2 };

        var page = new ChoicePage($"Nature (now {pk.Nature})", GameInfo.Sources.NatureDataSource, (int)pk.Nature);
        await _host.Navigation.PushAsync(page);
        if (await page.Result is not { } nature) return;

        var slot = await _host.DisplayActionSheet("Ability slot", "Cancel", null, "First ability", "Second ability");
        if (slot is null or "Cancel") return;

        var shiny = await _host.DisplayActionSheet($"Shiny (now {(pk.IsShiny ? "yes" : "no")})", "Cancel", null, "Shiny", "Not shiny");
        if (shiny is null or "Cancel") return;

        if (PidTools.Recalculate(pk, gender, (Nature)nature, slot == "First ability" ? 0 : 1, shiny == "Shiny"))
        {
            await SelectTabAsync(_tab);
            await _host.DisplayAlertAsync("PID", $"New PID {pk.PID:X8}: {genderName}, {pk.Nature}, ability slot {(slot == "First ability" ? 1 : 2)}, {(pk.IsShiny ? "shiny" : "not shiny")}.", "OK");
        }
        else
            await _host.DisplayAlertAsync("PID", "No PID matched those choices.", "OK");
    }

    private async Task ExportPkAsync()
    {
        _pk.RefreshChecksum();
        var path = Path.Combine(FileSystem.CacheDirectory, _pk.FileName);
        await File.WriteAllBytesAsync(path, _pk.Data.ToArray());
        await Share.RequestAsync(new ShareFileRequest("Export Pokemon", new ShareFile(path)));
    }

    private async Task ImportPkAsync()
    {
        var file = await FilePicker.PickAsync();
        if (file is null) return;
        await using var s = await file.OpenReadAsync();
        using var ms = new MemoryStream();
        await s.CopyToAsync(ms);
        var pk = EntityFormat.GetFromBytes(ms.ToArray(), _sav.Context);
        if (pk is null || pk.GetType() != _pk.GetType())
        {
            await _host.DisplayAlertAsync("Import", "That file is not a Pokemon this save can hold.", "OK");
            return;
        }
        Write(pk);
        _onFinished();
    }

    #endregion

    private void Apply()
    {
        _pk.ResetPartyStats();
        _pk.RefreshChecksum();
        Write(_pk);
    }

    private void Write(PKM pk)
    {
        if (_writer is not null)
        {
            _writer(pk);
            return;
        }
        if (_box < 0)
            _sav.SetPartySlotAtIndex(pk, _index);
        else
            _sav.SetBoxSlotAtIndex(pk, _box, _index);
    }
}
