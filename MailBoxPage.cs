using PKHeX.Core;

namespace PKHeXMobile;

/// <summary>Gen 4 / Gen 5 mail: the mail held by party Pokemon and the 20 mailbox slots (Psyduck Yellow included).</summary>
public class MailBoxPage : ThemedPage
{
    /// <summary>Item ids of the twelve mail items in Gen 4 and Gen 5.</summary>
    public static readonly int[] MailItemIds = [137, 138, 139, 140, 141, 142, 143, 144, 145, 146, 147, 148];

    private readonly SaveFile _sav;
    private readonly IList<PKM> _party;
    private readonly MailDetail[] _mail;
    private readonly byte _resetVersion, _resetLanguage;
    private readonly VerticalStackLayout _stack = new() { Spacing = 4, Padding = 12 };

    public static bool IsSupported(SaveFile sav) => sav is SAV4 or SAV5;

    public MailBoxPage(SaveFile sav)
    {
        _sav = sav;
        _party = sav.PartyData;
        if (sav is SAV4 s4)
        {
            _mail = new MailDetail[_party.Count + 20];
            for (int i = 0; i < _party.Count; i++)
                _mail[i] = new Mail4(((PK4)_party[i]).HeldMail.ToArray());
            for (int i = _party.Count, j = 0; i < _mail.Length; i++, j++)
                _mail[i] = s4.GetMail(j);
            var last = (Mail4)_mail[^1];
            (_resetVersion, _resetLanguage) = (last.AuthorVersion, last.AuthorLanguage);
        }
        else
        {
            var s5 = (SAV5)sav;
            _mail = new MailDetail[_party.Count + 20];
            for (int i = 0; i < _party.Count; i++)
                _mail[i] = new Mail5(((PK5)_party[i]).HeldMail.ToArray());
            for (int i = _party.Count, j = 0; i < _mail.Length; i++, j++)
                _mail[i] = s5.GetMail(j);
            var last = (Mail5)_mail[^1];
            (_resetVersion, _resetLanguage) = (last.AuthorVersion, last.AuthorLanguage);
        }
        Title = "Mail box";
        Content = new ScrollView { Content = _stack };
        Build();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Build(); // an edit page may have changed a mail
    }

    private bool IsMailItem(int item) => MailItemIds.Contains(item);

    private void Build()
    {
        _stack.Clear();
        var species = Names.Species(_sav);
        var items = Names.Items(_sav);
        _stack.Add(FormRows.Header("Held by party Pokémon"));
        for (int i = 0; i < _party.Count; i++)
        {
            var pk = _party[i];
            var held = IsMailItem(pk.HeldItem) ? Names.At(items, pk.HeldItem) : "not holding mail";
            _stack.Add(Row(i, $"{i + 1}. {Names.At(species, pk.Species)} ({held}) - {Label(_mail[i])}"));
        }
        _stack.Add(FormRows.Header("Mailbox"));
        for (int i = _party.Count; i < _mail.Length; i++)
            _stack.Add(Row(i, $"{i - _party.Count + 1}. {Label(_mail[i])}"));

        var save = new Button { Text = "Save" };
        save.Clicked += async (_, _) => await SaveAsync();
        var cancel = new Button { Text = "Cancel" };
        cancel.Clicked += async (_, _) => await Navigation.PopAsync();
        _stack.Add(FormRows.Buttons(save, cancel));
        Theme.Style(_stack);
    }

    private static string Label(MailDetail m) => m.IsEmpty != true ? $"from {m.AuthorName}" : "(empty)";

    private View Row(int index, string text)
    {
        var row = new Grid { ColumnSpacing = 6, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto), new(GridLength.Auto) } };
        row.Add(new Label { Text = text, VerticalOptions = LayoutOptions.Center });
        var edit = new Button { Text = "Edit" };
        edit.Clicked += async (_, _) => await Navigation.PushAsync(new MailEditPage(_sav, _mail[index]));
        row.Add(edit, 1);
        var clear = new Button { Text = "Clear" };
        clear.Clicked += async (_, _) => await ClearAsync(index);
        row.Add(clear, 2);
        return row;
    }

    private async Task ClearAsync(int index)
    {
        if (index < _party.Count && IsMailItem(_party[index].HeldItem))
        {
            var take = await DisplayAlertAsync("Clear mail", "This Pokémon is holding the mail. Also take the mail item away?", "Yes, take it", "Keep the item");
            if (take)
                _party[index].HeldItem = 0;
        }
        switch (_mail[index])
        {
            case Mail4 m4: m4.SetBlank(_resetLanguage, _resetVersion); break;
            case Mail5 m5: m5.SetBlank(_resetLanguage, _resetVersion); break;
        }
        Build();
    }

    private async Task SaveAsync()
    {
        var problems = new List<string>();
        for (int i = 0; i < _party.Count; i++)
        {
            var holds = IsMailItem(_party[i].HeldItem);
            if (holds && _mail[i].IsEmpty == true)
                problems.Add($"Party {i + 1} holds mail but the mail is empty.");
            else if (!holds && _sav is SAV4 && _mail[i].IsEmpty == false)
                problems.Add($"Party {i + 1} has mail but isn't holding a mail item.");
        }
        for (int i = 0; i < _mail.Length; i++)
            if (_mail[i].IsEmpty is null)
                problems.Add($"Mail {i + 1} has an unknown mail type.");
        if (problems.Count != 0 && !await DisplayAlertAsync("Mail", "Possible problems:\n" + string.Join("\n", problems) + "\n\nSave anyway?", "Save", "Back"))
            return;

        for (int i = 0; i < _party.Count; i++)
        {
            switch (_party[i])
            {
                case PK4 pk4: _mail[i].CopyTo(pk4); break;
                case PK5 pk5: _mail[i].CopyTo(pk5); break;
            }
        }
        for (int i = _party.Count; i < _mail.Length; i++)
            _mail[i].CopyTo(_sav);
        if (_party.Count > 0)
            _sav.PartyData = _party;
        await Navigation.PopAsync();
    }
}

/// <summary>Edits one Gen 4 / Gen 5 mail in memory; the mailbox page writes it to the save.</summary>
public class MailEditPage : ThemedPage
{
    private readonly SaveFile _sav;
    private readonly MailDetail _mail;
    private readonly VerticalStackLayout _stack = new() { Spacing = 4, Padding = 12 };

    public MailEditPage(SaveFile sav, MailDetail mail)
    {
        (_sav, _mail) = (sav, mail);
        Title = "Edit mail";
        Content = new ScrollView { Content = _stack };
        Build();
    }

    private int MailTypeIndex => _mail.IsEmpty == false ? 1 + _mail.MailType : 0;

    private IReadOnlyList<ComboItem> MailTypes()
    {
        var items = Names.Items(_sav);
        var list = new List<ComboItem> { new("(no mail)", 0) };
        for (int i = 0; i < MailBoxPage.MailItemIds.Length; i++)
            list.Add(new ComboItem(Names.At(items, MailBoxPage.MailItemIds[i]), i + 1));
        return list;
    }

    private void Build()
    {
        _stack.Clear();
        var m = _mail;

        _stack.Add(FormRows.Header("Mail type"));
        _stack.Add(PickRow("Type", () => ChoicePage.Name(MailTypes(), MailTypeIndex), MailTypes, MailTypeIndex, v => m.MailType = v > 0 ? v - 1 : 0xFF));

        _stack.Add(FormRows.Header("Author"));
        var name = new Entry { Text = m.AuthorName, MaxLength = 7 };
        name.Unfocused += (_, _) => m.AuthorName = name.Text ?? string.Empty;
        var nameRow = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star) }, MinimumHeightRequest = 40 };
        nameRow.Add(new Label { Text = "Name", VerticalOptions = LayoutOptions.Center });
        nameRow.Add(name, 1);
        _stack.Add(nameRow);
        _stack.Add(FormRows.Number("Trainer ID", m.AuthorTID, ushort.MaxValue, v => m.AuthorTID = (ushort)v));
        _stack.Add(FormRows.Number("Secret ID", m.AuthorSID, ushort.MaxValue, v => m.AuthorSID = (ushort)v));
        _stack.Add(FormRows.Switch("Female author", () => (m.AuthorGender & 1) != 0, v => m.AuthorGender = (byte)((m.AuthorGender & 0xFE) | (v ? 1 : 0))));
        _stack.Add(PickRow("Language", () => ChoicePage.Name(Languages(), m.AuthorLanguage), Languages, m.AuthorLanguage, v => m.AuthorLanguage = (byte)v));
        _stack.Add(PickRow("Game", () => ChoicePage.Name(Versions(), m.AuthorVersion), Versions, m.AuthorVersion, v => m.AuthorVersion = (byte)v));

        if (m is Mail4 m4)
        {
            _stack.Add(FormRows.Header("Pokémon shown on the mail"));
            var species = Names.Species(_sav);
            var list = Enumerable.Range(0, species.Length).Select(i => new ComboItem(i == 0 ? "(none)" : species[i], i)).Where(c => c.Value == 0 || !string.IsNullOrWhiteSpace(c.Text)).ToList();
            for (int i = 0; i < 3; i++)
            {
                var slot = i;
                int Current() => Math.Max(0, m4.GetAppearSpecies(slot) - 7); // the game stores these offset by 7
                _stack.Add(PickRow($"Pokémon {slot + 1}", () => ChoicePage.Name(list, Current()), () => list, Current(), v => m4.SetAppearSpecies(slot, (ushort)(v == 0 ? 0 : v + 7))));
            }
        }

        _stack.Add(FormRows.Header("Message (easy-chat word numbers)"));
        for (int y = 0; y < 3; y++)
        {
            var line = y;
            var row = new Grid { ColumnSpacing = 4 };
            for (int x = 0; x < 4; x++)
            {
                var word = x;
                row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                var e = new Entry { Text = m.GetMessage(line, word).ToString(), Keyboard = Keyboard.Numeric, FontSize = 13 };
                e.Unfocused += (_, _) =>
                {
                    var v = ushort.TryParse(e.Text, out var parsed) ? parsed : (ushort)0;
                    e.Text = v.ToString();
                    m.SetMessage(line, word, v);
                };
                row.Add(e, x);
            }
            _stack.Add(row);
        }

        if (m is Mail5 m5)
        {
            _stack.Add(FormRows.Header("Extra values"));
            for (int i = 0; i < 3; i++)
            {
                var idx = i;
                _stack.Add(FormRows.Number($"Misc {idx + 1}", m5.GetMisc(idx), ushort.MaxValue, v => m5.SetMisc(idx, (int)v)));
            }
            _stack.Add(FormRows.Number("Message ending", m5.MessageEnding, ushort.MaxValue, v => m5.MessageEnding = (ushort)v));
        }
        Theme.Style(_stack);
    }

    private IReadOnlyList<ComboItem> Languages() => GameInfo.Sources.LanguageDataSource(_sav.Generation, _sav.Context).ToList();

    private IReadOnlyList<ComboItem> Versions() => GameInfo.Sources.VersionDataSource.Where(z => ((GameVersion)z.Value).Context == _sav.Context).ToList();

    /// <summary>A label + button row; tapping opens a searchable list and rebuilds the page after a choice.</summary>
    private View PickRow(string label, Func<string> current, Func<IReadOnlyList<ComboItem>> items, int value, Action<int> set)
    {
        var button = new Button { Text = current(), HorizontalOptions = LayoutOptions.End };
        button.Clicked += async (_, _) =>
        {
            var page = new ChoicePage(label, items(), value);
            await Navigation.PushAsync(page);
            if (await page.Result is not int chosen) return;
            set(chosen);
            Build();
        };
        var grid = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) }, MinimumHeightRequest = 40 };
        grid.Add(new Label { Text = label, VerticalOptions = LayoutOptions.Center });
        grid.Add(button, 1);
        return grid;
    }
}
