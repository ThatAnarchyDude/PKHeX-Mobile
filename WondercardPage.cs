using PKHeX.Core;

namespace PKHeXMobile;

/// <summary>Wondercard / Mystery Gift slots for Gen 4-7 saves: view, import from file, export, delete and reset the received flags.</summary>
public class WondercardPage : ThemedPage
{
    private readonly SaveFile _sav;
    private readonly IMysteryGiftStorage _cards;
    private readonly IMysteryGiftFlags? _flags;
    private readonly DataMysteryGift[] _album;
    private readonly string[] _species;
    private readonly VerticalStackLayout _stack = new() { Spacing = 4, Padding = 12 };

    public static bool IsSupported(SaveFile sav) => sav is IMysteryGiftStorageProvider && sav.Generation is >= 4 and <= 7;

    public WondercardPage(SaveFile sav)
    {
        _sav = sav;
        _cards = ((IMysteryGiftStorageProvider)sav).MysteryGiftStorage;
        _flags = _cards as IMysteryGiftFlags;
        _species = Names.Species(sav);
        _album = Load();
        Title = "Wondercards";
        Content = new ScrollView { Content = _stack };
        Build();
    }

    private DataMysteryGift[] Load()
    {
        var count = _cards.GiftCountMax;
        var album = new DataMysteryGift[_sav is SAV4HGSS ? count + 1 : count];
        for (int i = 0; i < count; i++)
            album[i] = _cards.GetMysteryGift(i);
        if (_sav is SAV4HGSS hg)
            album[^1] = hg.LockCapsuleSlot; // the Lock Capsule gift lives in its own extra slot
        return album;
    }

    private bool IsLockCapsuleSlot(int index) => _sav is SAV4HGSS && index == _album.Length - 1;

    private void Build()
    {
        _stack.Clear();
        var import = new Button { Text = "Import gift file…" };
        import.Clicked += async (_, _) => await ImportAsync(-1);
        _stack.Add(FormRows.Buttons(import));

        for (int i = 0; i < _album.Length; i++)
        {
            var index = i;
            var gift = _album[i];
            var row = new Grid { ColumnSpacing = 6, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
            row.Add(new Label { Text = Describe(i, gift), VerticalOptions = LayoutOptions.Center });
            var actions = new Button { Text = "Actions" };
            actions.Clicked += async (_, _) => await ActionsAsync(index);
            row.Add(actions, 1);
            _stack.Add(row);
        }

        if (_flags is { } flags)
        {
            _stack.Add(FormRows.Header("Received flags"));
            int set = 0;
            for (int i = 1; i < flags.MysteryGiftReceivedFlagMax; i++)
                if (flags.GetMysteryGiftReceivedFlag(i)) set++;
            _stack.Add(new Label { Text = $"{set} cards are marked as received, so the game won't deliver them again." });
            var clear = new Button { Text = "Clear all received flags" };
            clear.Clicked += async (_, _) =>
            {
                if (!await DisplayAlertAsync("Received flags", "Clear every received flag? The game can then deliver those cards again.", "Clear", "Cancel"))
                    return;
                for (int i = 1; i < flags.MysteryGiftReceivedFlagMax; i++)
                    flags.SetMysteryGiftReceivedFlag(i, false);
                Build();
            };
            _stack.Add(FormRows.Buttons(clear));
        }
        Theme.Style(_stack);
    }

    private string Describe(int index, DataMysteryGift gift)
    {
        var label = IsLockCapsuleSlot(index) ? "Lock Capsule" : $"Slot {index + 1}";
        if (gift.IsEmpty)
            return $"{label}: (empty)";
        var what = gift.Species != 0 ? Names.At(_species, gift.Species) : gift.CardTitle;
        return $"{label}: {gift.CardTitle} - {what}{(gift.GiftUsed ? " (used)" : string.Empty)}";
    }

    private async Task ActionsAsync(int index)
    {
        var gift = _album[index];
        var options = gift.IsEmpty
            ? new[] { "Import file here" }
            : new[] { "View details", "Export file", "Import file here", "Mark as unused", "Delete" };
        var choice = await DisplayActionSheet(Describe(index, gift), "Cancel", null, options);
        switch (choice)
        {
            case "View details":
                await DisplayAlertAsync(gift.CardTitle, string.Join(Environment.NewLine, gift.GetDescription()), "OK");
                break;
            case "Export file":
                var path = Path.Combine(FileSystem.CacheDirectory, gift.FileName);
                await File.WriteAllBytesAsync(path, gift.Write().ToArray());
                await Share.RequestAsync(new ShareFileRequest("Export gift", new ShareFile(path)));
                break;
            case "Import file here":
                await ImportAsync(index);
                break;
            case "Mark as unused":
                gift.GiftUsed = false;
                Write(index, gift);
                Build();
                break;
            case "Delete":
                if (!await DisplayAlertAsync("Delete", "Remove this gift from the save?", "Delete", "Cancel"))
                    return;
                var blank = gift.Clone();
                blank.Clear();
                Write(index, blank);
                Build();
                break;
        }
    }

    private void Write(int index, DataMysteryGift gift)
    {
        _album[index] = gift;
        if (IsLockCapsuleSlot(index) && _sav is SAV4HGSS hg && gift is PCD pcd)
            hg.LockCapsuleSlot = pcd;
        else if (index < _cards.GiftCountMax)
            _cards.SetMysteryGift(index, gift);
    }

    /// <summary>Imports a gift file into <paramref name="index"/>, or into the first empty slot of the same type when it is -1.</summary>
    private async Task ImportAsync(int index)
    {
        var file = await FilePicker.PickAsync(new PickOptions { PickerTitle = "Pick a gift file (.pgt .pcd .pgf .wc6 .wc7 ...)" });
        if (file is null) return;
        byte[] data;
        await using (var s = await file.OpenReadAsync())
        using (var ms = new MemoryStream())
        {
            await s.CopyToAsync(ms);
            data = ms.ToArray();
        }
        var gift = MysteryGift.GetMysteryGift(data, Path.GetExtension(file.FileName)) as DataMysteryGift;
        if (gift is null)
        {
            await DisplayAlertAsync("Import", "That file isn't a recognised gift.", "OK");
            return;
        }
        if (!gift.IsCardCompatible(_sav, out var msg))
        {
            await DisplayAlertAsync("Import", $"This gift can't go in this save: {msg}", "OK");
            return;
        }

        if (gift is PCD { IsLockCapsule: true } && _sav is SAV4HGSS)
            index = _album.Length - 1;
        else if (index < 0)
        {
            index = Array.FindIndex(_album, g => g.IsEmpty && g.Type == gift.Type);
            if (index < 0)
            {
                await DisplayAlertAsync("Import", "There is no empty slot for this kind of gift. Delete one first or pick a slot.", "OK");
                return;
            }
        }

        var other = _album[index];
        if (gift is PCD { CanConvertToPGT: true } pcd && other is PGT)
            gift = pcd.Gift;
        else if (gift.Type != other.Type)
        {
            await DisplayAlertAsync("Import", $"That slot holds {other.Type} gifts, but this file is a {gift.Type}.", "OK");
            return;
        }
        else if (gift is PCD p && p.IsLockCapsule != IsLockCapsuleSlot(index))
        {
            await DisplayAlertAsync("Import", "Lock Capsule gifts only fit the Lock Capsule slot (and the other way round).", "OK");
            return;
        }

        Write(index, gift);
        Build();
    }
}
