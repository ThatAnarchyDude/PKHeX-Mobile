using PKHeX.Core;

namespace PKHeXMobile;

/// <summary>Main screen modelled on the PC window: Box / Party / Other / SAV tabs over a wallpaper box grid.</summary>
public class MainPage : ThemedPage
{
    private static readonly string[] TabNames = ["Box", "Party", "Other", "SAV"];
    private const int BoxColumns = 6;

    private readonly Label _info = new() { Text = "Open a .sav or .dsv (work on a copy).", FontSize = 13 };
    private readonly Button _export = new() { Text = "Export", IsEnabled = false };
    private readonly Grid _partyGrid = new() { ColumnSpacing = 0, RowSpacing = 0 };
    private readonly Grid _boxGrid = new() { ColumnSpacing = 0, RowSpacing = 0 };
    private readonly Image _wallpaper = new() { Aspect = Aspect.Fill };
    private readonly Label _boxTitle = new() { HorizontalTextAlignment = TextAlignment.Center, VerticalOptions = LayoutOptions.Center, FontAttributes = FontAttributes.Bold };
    private readonly VerticalStackLayout _otherView = new() { Spacing = 8, Padding = 8 };
    private readonly ContentView _panel = new();
    private readonly List<Button> _tabButtons = [];
    private readonly View[] _views;
    private readonly Grid _root = new() { ColumnSpacing = 0 };
    private readonly Grid _editorPane = new() { IsVisible = false, RowSpacing = 4, Padding = 4 };
    private readonly ContentView _editorHost = new();
    private readonly Label _paneTitle = new() { FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.TailTruncation };
    private readonly Button _paneMove = new() { Text = "Move", HeightRequest = 38, Padding = new Thickness(10, 0) };
    private readonly Button _paneDelete = new() { Text = "Delete", HeightRequest = 38, Padding = new Thickness(10, 0) };
    private PokemonEditorView? _editor;
    private Slot? _editorSlot;
    private bool _wide;
    private SaveSession? _session;
    private int _boxIndex;
    private int _refreshId;
    private int _tab;

    private sealed record Slot(PKM Pk, int Box, int Index);

    public MainPage()
    {
        Title = "PKHeX Mobile";
        var open = new Button { Text = "Open save" };
        open.Clicked += async (_, _) => await OpenAsync();
        _export.Clicked += async (_, _) => { if (_session is { } s) await s.ExportAsync(); };

        var prev = new Button { Text = "◀", WidthRequest = 56 };
        prev.Clicked += (_, _) => ChangeBox(-1);
        var next = new Button { Text = "▶", WidthRequest = 56 };
        next.Clicked += (_, _) => ChangeBox(1);
        var boxBar = new Grid
        {
            ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) },
            Children = { prev, InColumn(_boxTitle, 1), InColumn(next, 2) }
        };
        _boxTitle.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => await PickBoxAsync()) });

        var boxView = new VerticalStackLayout
        {
            Spacing = 4, Padding = 6, MaximumWidthRequest = 6 * 70 + 12, HorizontalOptions = LayoutOptions.Fill,
            Children = { boxBar, new Grid { Children = { _wallpaper, _boxGrid } } }
        };
        _views = [boxView, new ContentView { Padding = 6, MaximumWidthRequest = 2 * 120 + 12, HorizontalOptions = LayoutOptions.Fill, Content = _partyGrid }, _otherView, BuildSavView()];

        var tabs = new Grid { ColumnSpacing = 0 };
        for (int i = 0; i < TabNames.Length; i++)
        {
            var index = i;
            tabs.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var b = new Button { Text = TabNames[i], CornerRadius = 0, HeightRequest = 38, Padding = 0 };
            b.Clicked += (_, _) => SelectTab(index);
            _tabButtons.Add(b);
            tabs.Add(b, i);
        }

        var frame = new Border { Padding = 0, StrokeThickness = 1, Content = _panel };
        frame.SetAppTheme<Brush>(Border.StrokeProperty, new SolidColorBrush(Theme.BorderLight), new SolidColorBrush(Theme.BorderDark));

        var rightStack = new VerticalStackLayout
        {
            Padding = 8, Spacing = 4,
            Children =
            {
                new Grid
                {
                    ColumnSpacing = 6,
                    ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) },
                    Children = { open, InColumn(_export, 1) }
                },
                _info, tabs, frame,
            }
        };

        // Landscape layout: the Pokemon editor on the left, Box / Party / Other / SAV on the right (like the PC window).
        _paneMove.Clicked += async (_, _) => await PaneMoveAsync();
        _paneDelete.Clicked += async (_, _) => await PaneDeleteAsync();
        _editorPane.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        _editorPane.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        _editorPane.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        _editorPane.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        _editorPane.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        _editorPane.Add(_paneTitle, 0, 0);
        _editorPane.Add(_paneMove, 1, 0);
        _editorPane.Add(_paneDelete, 2, 0);
        _editorPane.Add(_editorHost, 0, 1);
        Grid.SetColumnSpan(_editorHost, 3);

        _root.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(0)));
        _root.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        _root.Add(_editorPane, 0, 0);
        _root.Add(new ScrollView { Content = rightStack }, 1, 0);
        Content = _root;
        ShowPanePlaceholder();
        SelectTab(0);
    }

    /// <summary>Switches between the single-column phone layout and the two-pane landscape layout.</summary>
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        var wide = width > height && width >= 640;
        _root.ColumnDefinitions[0].Width = wide ? new GridLength(width * 0.47) : new GridLength(0);
        _editorPane.IsVisible = wide;
        if (wide != _wide)
        {
            _wide = wide;
            if (!wide) ShowPanePlaceholder();
        }
    }

    private readonly Dictionary<(int Box, int Index), (Border Cell, Brush Stroke, double Thickness)> _cells = [];

    /// <summary>Outlines the slot being edited in the left pane (landscape).</summary>
    private void HighlightSelected()
    {
        foreach (var ((box, index), (cell, stroke, thickness)) in _cells)
        {
            var selected = _wide && _editorSlot is { } s && s.Box == box && s.Index == index;
            cell.Stroke = selected ? new SolidColorBrush(Color.FromArgb("#F0A060")) : stroke;
            cell.StrokeThickness = selected ? 3 : thickness;
        }
    }

    private void ShowPanePlaceholder()
    {
        _editor = null;
        _editorSlot = null;
        HighlightSelected();
        _paneTitle.Text = string.Empty;
        _paneMove.IsVisible = _paneDelete.IsVisible = false;
        _editorHost.Content = new Label { Text = "Tap a Pokémon to edit it here.", HorizontalTextAlignment = TextAlignment.Center, VerticalOptions = LayoutOptions.Center, Margin = new Thickness(12) };
    }

    /// <summary>Landscape: shows the tapped Pokemon in the left-hand editor.</summary>
    private void ShowInPane(Slot slot)
    {
        var sav = _session!.Sav;
        _editorSlot = slot;
        _paneTitle.Text = slot.Box < 0 ? $"Party {slot.Index + 1}" : $"{BoxName(sav, slot.Box)} #{slot.Index + 1}";
        _paneMove.Text = slot.Box < 0 ? "To box" : "To party";
        _paneMove.IsVisible = _paneDelete.IsVisible = true;
        HighlightSelected();
        _editor = new PokemonEditorView(this, sav, slot.Pk, slot.Box, slot.Index, null, async () =>
        {
            // Apply / Discard / Import: redraw the grids, then keep the same slot open with its fresh data.
            await RefreshAsync();
            var fresh = slot.Box < 0 ? sav.GetPartySlotAtIndex(slot.Index) : sav.GetBoxSlotAtIndex(slot.Box, slot.Index);
            if (fresh.Species == 0) ShowPanePlaceholder();
            else ShowInPane(new Slot(fresh, slot.Box, slot.Index));
        });
        _editor.Reload(); // arm the "returned from a child screen" reload
        _editorHost.Content = _editor;
        Theme.Style(_editorHost);
    }

    private async Task PaneMoveAsync()
    {
        if (_editorSlot is not { } slot || _session is null) return;
        var sav = _session.Sav;
        if (slot.Box < 0)
        {
            if (!MoveToBox(slot)) { await DisplayAlertAsync("Move", "No empty box slot.", "OK"); return; }
        }
        else
        {
            if (sav.PartyCount >= 6) { await DisplayAlertAsync("Move", "Party is full.", "OK"); return; }
            sav.SetPartySlotAtIndex(slot.Pk, sav.PartyCount);
            Remove(slot);
        }
        ShowPanePlaceholder();
        await RefreshAsync();
    }

    private async Task PaneDeleteAsync()
    {
        if (_editorSlot is not { } slot || _session is null) return;
        if (!await DisplayAlertAsync("Delete", "Remove this Pokemon from the save?", "Delete", "Cancel")) return;
        Remove(slot);
        ShowPanePlaceholder();
        await RefreshAsync();
    }

    private static T InColumn<T>(T v, int column) where T : View { Grid.SetColumn(v, column); return v; }

    private void SelectTab(int tab)
    {
        _tab = tab;
        _panel.Content = _views[tab];
        UpdateTabStyles();
    }

    /// <summary>The selected tab looks raised (slot colour); the others sit back like the PC tab strip.</summary>
    private void UpdateTabStyles()
    {
        for (int i = 0; i < _tabButtons.Count; i++)
        {
            var b = _tabButtons[i];
            var selected = i == _tab;
            b.FontAttributes = selected ? FontAttributes.Bold : FontAttributes.None;
            if (selected)
                b.SetAppThemeColor(VisualElement.BackgroundColorProperty, Theme.SlotLight, Theme.SlotDark);
            else
                b.SetAppThemeColor(VisualElement.BackgroundColorProperty, Theme.ButtonLight, Theme.ButtonDark);
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateTabStyles();
        _editor?.Reload();
        if (_session is not null) _ = RefreshAsync();
    }

    private void ChangeBox(int delta)
    {
        if (_session is null) return;
        _boxIndex = (_boxIndex + delta + _session.Sav.BoxCount) % _session.Sav.BoxCount;
        _ = RefreshAsync();
    }

    private async Task PickBoxAsync()
    {
        if (_session is null) return;
        var sav = _session.Sav;
        var items = Enumerable.Range(0, sav.BoxCount).Select(i => new ComboItem(BoxName(sav, i), i)).ToList();
        var page = new ChoicePage("Box", items, _boxIndex);
        await Navigation.PushAsync(page);
        if (await page.Result is { } picked)
        {
            _boxIndex = picked;
            await RefreshAsync();
        }
    }

    private static string BoxName(SaveFile sav, int box)
    {
        try { return sav is IBoxDetailName named ? named.GetBoxName(box) : $"Box {box + 1}"; }
        catch (Exception) { return $"Box {box + 1}"; }
    }

    /// <summary>The SAV tab: one big button per editor, like the PC version's SAV tab.</summary>
    private View BuildSavView()
    {
        var grid = new Grid { ColumnSpacing = 6, RowSpacing = 6, Padding = 8 };
        for (int c = 0; c < 3; c++)
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        var buttons = new (string Text, Func<SaveSession, Task> Open)[]
        {
            ("Trainer Info", s => Navigation.PushAsync(new TrainerPage(s.Sav))),
            ("Items", s => Navigation.PushAsync(new BagPage(s.Sav))),
            ("Event Flags", async s =>
            {
                if (FlagsPage.GetBlock(s.Sav) is null)
                    await DisplayAlertAsync("Event Flags", "This game has no event flag editor here.", "OK");
                else
                    await Navigation.PushAsync(new FlagsPage(s.Sav));
            }),
            ("Pokédex", async s =>
            {
                if (!DexPage.IsSupported(s.Sav))
                    await DisplayAlertAsync("Pokédex", "The Pokédex editor doesn't cover this game yet.", "OK");
                else
                    await Navigation.PushAsync(new DexPage(s.Sav));
            }),
            ("Box Layout", async s =>
            {
                if (!BoxLayoutPage.IsSupported(s.Sav))
                    await DisplayAlertAsync("Box Layout", "This game has no box names to edit here.", "OK");
                else
                    await Navigation.PushAsync(new BoxLayoutPage(s.Sav));
            }),
            ("Misc Edits", async s =>
            {
                if (!GameDataPage.IsSupported(s.Sav))
                    await DisplayAlertAsync("Misc Edits", "This editor covers Generation 4 games and Psyduck Yellow so far.", "OK");
                else
                    await Navigation.PushAsync(new GameDataPage(s.Sav));
            }),
            ("Wondercards", async s =>
            {
                if (!WondercardPage.IsSupported(s.Sav))
                    await DisplayAlertAsync("Wondercards", "Wondercards are edited for Generation 4 to 7 games.", "OK");
                else
                    await Navigation.PushAsync(new WondercardPage(s.Sav));
            }),
            ("Mail Box", async s =>
            {
                if (!MailBoxPage.IsSupported(s.Sav))
                    await DisplayAlertAsync("Mail Box", "The mail editor covers Generation 4 and 5 games.", "OK");
                else
                    await Navigation.PushAsync(new MailBoxPage(s.Sav));
            }),
            ("Pokéathlon", async s =>
            {
                if (!PokeathlonPage.IsSupported(s.Sav))
                    await DisplayAlertAsync("Pokéathlon", "This editor is for HeartGold, SoulSilver and Psyduck Yellow.", "OK");
                else
                    await Navigation.PushAsync(new PokeathlonPage(s.Sav));
            }),
            ("Apricorns", async s =>
            {
                if (!ApricornPage.IsSupported(s.Sav))
                    await DisplayAlertAsync("Apricorns", "This editor is for HeartGold, SoulSilver and Psyduck Yellow.", "OK");
                else
                    await Navigation.PushAsync(new ApricornPage(s.Sav));
            }),
        };

        for (int i = 0; i < buttons.Length; i++)
        {
            var (text, open) = buttons[i];
            var b = new Button { Text = text, HeightRequest = 64 };
            b.Clicked += async (_, _) =>
            {
                if (_session is { } s) await open(s);
                else await DisplayAlertAsync(text, "Open a save first.", "OK");
            };
            grid.Add(b, i % 3, i / 3);
        }
        return grid;
    }

    private async Task OpenAsync()
    {
        var file = await FilePicker.PickAsync();
        if (file is null) return;
        var session = await SaveSession.OpenAsync(file);
        if (session is null) { _info.Text = "Unrecognized save file."; return; }

        _session = session;
        var sav = session.Sav;
        _info.Text = $"{GameInfo.GetVersionName(sav.Version)} - {sav.OT} (TID {sav.DisplayTID})  {sav.PlayTimeString}";
        _boxIndex = 0;
        _export.IsEnabled = true;
        await RefreshAsync();
    }

    private async Task SlotActionsAsync(Slot slot)
    {
        var sav = _session!.Sav;
        if (_wide && slot.Pk.Species != 0)
        {
            ShowInPane(slot);
            return;
        }
        var party = slot.Box < 0;
        if (slot.Pk.Species == 0)
        {
            if (party && slot.Index > sav.PartyCount)
            {
                await DisplayAlertAsync("Party", "Fill the party slots in order; use the first empty slot.", "OK");
                return;
            }
            var add = await DisplayActionSheet("Empty slot", "Cancel", null, "Add new Pokemon", "Create from Showdown set", "Import .pk file");
            if (add == "Add new Pokemon")
                await AddNewAsync(slot);
            else if (add == "Create from Showdown set")
            {
                await Navigation.PushAsync(new ShowdownPage(sav, async pk =>
                {
                    Place(slot, pk);
                    await RefreshAsync();
                }));
                return;
            }
            else if (add == "Import .pk file")
                await ImportAsync(slot);
            await RefreshAsync();
            return;
        }

        var move = party ? "Move to box" : "Move to party";
        var choice = await DisplayActionSheet($"{Names.At(Names.Species(sav), slot.Pk.Species)}", "Cancel", "Delete", "Edit", move);
        switch (choice)
        {
            case "Edit":
                await Navigation.PushAsync(new EditPage(sav, slot.Pk, slot.Box, slot.Index));
                return;
            case "Delete":
                if (await DisplayAlertAsync("Delete", "Remove this Pokemon from the save?", "Delete", "Cancel"))
                    Remove(slot);
                break;
            case "Move to box":
                if (!MoveToBox(slot)) await DisplayAlertAsync("Move", "No empty box slot.", "OK");
                break;
            case "Move to party":
                if (sav.PartyCount >= 6) { await DisplayAlertAsync("Move", "Party is full.", "OK"); break; }
                sav.SetPartySlotAtIndex(slot.Pk, sav.PartyCount);
                Remove(slot);
                break;
        }
        await RefreshAsync();
    }

    private void Remove(Slot slot)
    {
        var sav = _session!.Sav;
        if (slot.Box < 0)
            sav.DeletePartySlot(slot.Index);
        else
            sav.SetBoxSlotAtIndex(sav.BlankPKM, slot.Box, slot.Index);
    }

    private bool MoveToBox(Slot slot)
    {
        var sav = _session!.Sav;
        for (int b = 0; b < sav.BoxCount; b++)
        {
            var data = sav.GetBoxData(b);
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i].Species != 0)
                    continue;
                sav.SetBoxSlotAtIndex(slot.Pk, b, i);
                Remove(slot);
                return true;
            }
        }
        return false;
    }

    private async Task AddNewAsync(Slot slot)
    {
        var sav = _session!.Sav;
        var names = Names.Species(sav);
        var q = await DisplayPromptAsync("New Pokemon", "Type part of the species name");
        if (string.IsNullOrWhiteSpace(q)) return;
        var matches = Enumerable.Range(1, names.Length - 1)
            .Where(i => names[i].Contains(q, StringComparison.OrdinalIgnoreCase)).Take(12).ToList();
        if (matches.Count == 0) { await DisplayAlertAsync("New Pokemon", "No match.", "OK"); return; }
        var pick = await DisplayActionSheet("Pick species", "Cancel", null, matches.Select(i => names[i]).ToArray());
        var species = matches.FirstOrDefault(i => names[i] == pick);
        if (species == 0) return;

        var pk = NewPokemon.Create(sav, species);
        Place(slot, pk);
        await RefreshAsync();
        await Navigation.PushAsync(new EditPage(sav, pk, slot.Box, slot.Index));
    }

    private async Task ImportAsync(Slot slot)
    {
        var sav = _session!.Sav;
        var file = await FilePicker.PickAsync();
        if (file is null) return;
        await using var s = await file.OpenReadAsync();
        using var ms = new MemoryStream();
        await s.CopyToAsync(ms);
        var pk = EntityFormat.GetFromBytes(ms.ToArray(), sav.Context);
        if (pk is null || pk.GetType() != sav.BlankPKM.GetType())
        {
            await DisplayAlertAsync("Import", "That file is not a Pokemon this save can hold.", "OK");
            return;
        }
        Place(slot, pk);
    }

    private void Place(Slot slot, PKM pk)
    {
        var sav = _session!.Sav;
        if (slot.Box < 0)
            sav.SetPartySlotAtIndex(pk, slot.Index);
        else
            sav.SetBoxSlotAtIndex(pk, slot.Box, slot.Index);
    }

    /// <summary>Redraws the box (with its wallpaper), the party and the Other tab.</summary>
    private async Task RefreshAsync()
    {
        if (_session is null) return;
        var id = ++_refreshId;
        var sav = _session.Sav;
        _boxIndex = Math.Clamp(_boxIndex, 0, sav.BoxCount - 1);
        _boxTitle.Text = BoxName(sav, _boxIndex);

        var party = sav.PartyData.Concat(Enumerable.Range(0, 6).Select(_ => sav.BlankPKM)).Take(6).ToList();
        var box = sav.GetBoxData(_boxIndex);

        // Legality for every slot, computed off the UI thread; a red triangle marks the illegal ones like the PC grid.
        var all = party.Concat(box).ToList();
        var illegal = await Task.Run(() => all.Select(pk =>
        {
            if (pk.Species == 0) return false;
            try { return !new LegalityAnalysis(pk, sav.Personal).Valid; }
            catch (Exception) { return false; }
        }).ToArray());

        _cells.Clear();
        var partyCells = new List<(View, int)>();
        var boxCells = new List<(View, int)>();
        for (int i = 0; i < party.Count; i++)
            partyCells.Add((await CellAsync(new Slot(party[i], -1, i), sav, illegal[i], onWallpaper: false), i));
        for (int i = 0; i < box.Length; i++)
            boxCells.Add((await CellAsync(new Slot(box[i], _boxIndex, i), sav, illegal[party.Count + i], onWallpaper: true), i));
        var wallpaper = await Sprites.FileAsync($"box/{BoxWallpaper.Name(sav, _boxIndex)}.png") ?? await Sprites.FileAsync("box/box_wp16xy.png");
        if (id != _refreshId) return; // a newer refresh is in flight

        _wallpaper.Source = wallpaper;
        Fill(_partyGrid, partyCells, 2);
        Fill(_boxGrid, boxCells, BoxColumns);
        HighlightSelected();
        await RefreshOtherAsync(sav);
    }

    private static void Fill(Grid grid, List<(View Cell, int Index)> cells, int columns)
    {
        grid.Children.Clear();
        grid.RowDefinitions.Clear();
        grid.ColumnDefinitions.Clear();
        for (int c = 0; c < columns; c++)
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (int r = 0; r < (cells.Count + columns - 1) / columns; r++)
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        foreach (var (cell, i) in cells)
            grid.Add(cell, i % columns, i / columns);
    }

    /// <summary>The Other tab: Day Care contents (Gen 4) and a shortcut to edit them.</summary>
    private async Task RefreshOtherAsync(SaveFile sav)
    {
        _otherView.Clear();
        if (sav is not SAV4 s4)
        {
            _otherView.Add(new Label { Text = "Nothing extra to show for this game." });
            return;
        }
        _otherView.Add(new Label { Text = "Day Care", FontAttributes = FontAttributes.Bold });
        for (int i = 0; i < s4.DaycareSlotCount; i++)
        {
            var pk = s4.GetStoredSlot(s4.GetDaycareSlot(i).Span);
            var row = new HorizontalStackLayout { Spacing = 8 };
            var cell = await CellAsync(new Slot(pk, -2, i), sav, illegal: false, onWallpaper: false);
            cell.WidthRequest = 80;
            row.Add(cell);
            row.Add(new Label
            {
                VerticalOptions = LayoutOptions.Center,
                Text = pk.Species == 0 ? $"{i + 1}: empty" : $"{i + 1}: {Names.At(Names.Species(sav), pk.Species)}  +XP {s4.GetDaycareEXP(i)}",
            });
            _otherView.Add(row);
        }
        _otherView.Add(new Label { Text = s4.IsEggAvailable ? "An egg is waiting." : "No egg available." });
        var edit = new Button { Text = "Edit Day Care and more (Misc Edits)" };
        edit.Clicked += async (_, _) => await Navigation.PushAsync(new GameDataPage(sav));
        _otherView.Add(edit);
        Theme.Style(_otherView);
    }

    /// <summary>One slot: sprite, level, ball, shiny star, held-item marker and a red triangle for illegal Pokemon.</summary>
    private async Task<Border> CellAsync(Slot slot, SaveFile sav, bool illegal, bool onWallpaper)
    {
        var pk = slot.Pk;
        var layout = new Grid { HeightRequest = onWallpaper ? 62 : 72 };
        if (pk.Species != 0)
        {
            var sprite = await Sprites.PokemonAsync(pk, sav);
            layout.Add(new Image
            {
                Source = sprite, Aspect = Aspect.AspectFit, WidthRequest = 58, HeightRequest = 48,
                HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Start, Margin = new Thickness(0, 2, 0, 0),
            });
            layout.Add(new Label { Text = $"Lv.{pk.CurrentLevel}", FontSize = 9, VerticalOptions = LayoutOptions.End, HorizontalOptions = LayoutOptions.Center });
            if (pk.IsShiny && await Sprites.FileAsync("ui/rare_icon.png") is { } star)
                layout.Add(new Image { Source = star, WidthRequest = 14, HeightRequest = 14, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start });
            if (illegal && await Sprites.FileAsync("ui/warn.png") is { } warn)
                layout.Add(new Image { Source = warn, WidthRequest = 14, HeightRequest = 14, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.End });
            if (pk.HeldItem != 0 && await Sprites.FileAsync("helditem.png") is { } item)
                layout.Add(new Image { Source = item, WidthRequest = 12, HeightRequest = 12, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.End });
            if (pk.Ball > 0 && await Sprites.BallAsync(pk.Ball) is { } ball)
                layout.Add(new Image { Source = ball, WidthRequest = 14, HeightRequest = 14, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Start });
        }

        Border border;
        if (onWallpaper)
        {
            // Box slots are see-through so the wallpaper shows, with thin dark grid lines like the PC box.
            border = new Border { Padding = 0, Content = layout, BackgroundColor = Colors.Transparent, StrokeThickness = 0.6, Stroke = new SolidColorBrush(Color.FromArgb("#66202020")) };
        }
        else
            border = Theme.Slot(layout);

        if (slot.Box != -2) // Day Care slots are edited from their own screen
            _cells[(slot.Box, slot.Index)] = (border, border.Stroke, border.StrokeThickness);
        if (slot.Box != -2)
            border.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => await SlotActionsAsync(slot)) });
        return border;
    }
}
