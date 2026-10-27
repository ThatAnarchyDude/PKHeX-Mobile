using PKHeX.Core;

namespace PKHeXMobile;

/// <summary>HeartGold / SoulSilver / Psyduck Yellow Apricorn pouch counts.</summary>
public class ApricornPage : ThemedPage
{
    private const int ItemNameBase = 485; // Red Apricorn
    // Save slot order -> item id offset (the game stores Yellow and Blue out of item order), as in the PC editor.
    private static readonly byte[] ItemNameOffset = [0, 2, 1, 3, 4, 5, 6];

    private readonly SAV4HGSS _sav;
    private readonly int[] _counts = new int[ItemNameOffset.Length];
    private readonly Entry[] _entries = new Entry[ItemNameOffset.Length];

    public static bool IsSupported(SaveFile sav) => sav is SAV4HGSS;

    public ApricornPage(SaveFile sav)
    {
        _sav = (SAV4HGSS)sav;
        Title = "Apricorns";
        var names = Names.Items(sav);
        var stack = new VerticalStackLayout { Spacing = 4, Padding = 12 };
        for (int i = 0; i < _counts.Length; i++)
        {
            var slot = i;
            _counts[i] = _sav.GetApricornCount(i);
            stack.Add(FormRows.Number(Names.At(names, ItemNameBase + ItemNameOffset[i]), _counts[i], 99, v => _counts[slot] = (int)v, out _entries[i]));
        }

        var all = new Button { Text = "All 99" };
        all.Clicked += (_, _) => Fill(99);
        var none = new Button { Text = "None" };
        none.Clicked += (_, _) => Fill(0);
        var save = new Button { Text = "Save" };
        save.Clicked += async (_, _) =>
        {
            foreach (var e in _entries) e.Unfocus(); // commit a value still being typed
            for (int i = 0; i < _counts.Length; i++)
                _sav.SetApricornCount(i, _counts[i]);
            await Navigation.PopAsync();
        };
        stack.Add(FormRows.Buttons(all, none, save));
        Content = new ScrollView { Content = stack };
    }

    private void Fill(int count)
    {
        for (int i = 0; i < _counts.Length; i++)
        {
            _counts[i] = count;
            _entries[i].Text = count.ToString();
        }
    }
}
