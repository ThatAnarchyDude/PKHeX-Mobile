using PKHeX.Core;

namespace PKHeXMobile;

/// <summary>HeartGold / SoulSilver / Psyduck Yellow Pokéathlon: Athlete Points, Data Cards, daily shop and best event scores.</summary>
public class PokeathlonPage : ThemedPage
{
    private static readonly string[] EventNames =
        ["Hurdle Dash", "Pennant Capture", "Circle Push", "Block Smash", "Disc Catch", "Lamp Jump", "Relay Run", "Ring Drop", "Snow Throw", "Goal Roll"];

    private readonly Pokeathlon4 _data;
    private readonly VerticalStackLayout _stack = new() { Spacing = 4, Padding = 12 };

    public static bool IsSupported(SaveFile sav) => sav is SAV4HGSS;

    public PokeathlonPage(SaveFile sav)
    {
        _data = ((SAV4HGSS)sav).Pokeathlon;
        Title = "Pokéathlon";
        Content = new ScrollView { Content = _stack };
        Build();
    }

    private void Build()
    {
        _stack.Clear();
        _stack.Add(FormRows.Header("Athlete Points"));
        _stack.Add(FormRows.Number("Points (spend in the Athlete Shop)", _data.Points, Pokeathlon4.MaxPoints, v => _data.Points = (uint)v));

        _stack.Add(FormRows.Header("Data Cards"));
        var allCards = new Button { Text = "Get all Data Cards" };
        allCards.Clicked += (_, _) => { _data.FlagsDataCard = Pokeathlon4.DataCardAllObtained; Build(); };
        var noCards = new Button { Text = "Clear Data Cards" };
        noCards.Clicked += (_, _) => { _data.FlagsDataCard = 0; Build(); };
        _stack.Add(new Label { Text = $"{System.Numerics.BitOperations.PopCount(_data.FlagsDataCard)} of 27 owned" });
        _stack.Add(FormRows.Buttons(allCards, noCards));

        _stack.Add(FormRows.Header("Athlete Shop"));
        var restock = new Button { Text = "Reset today's purchases" };
        restock.Clicked += (_, _) => _data.FlagsDailyShop = 0;
        _stack.Add(FormRows.Buttons(restock));

        _stack.Add(FormRows.Header("Best event scores"));
        for (int i = 0; i < EventNames.Length; i++)
        {
            var ev = (PokeathlonEvent4)i;
            _stack.Add(FormRows.Number(EventNames[i], _data.GetBestScore(ev), ushort.MaxValue, v => _data.SetBestScore(ev, (ushort)v)));
        }
        _stack.Add(new Label { Text = $"Global score: {_data.CalculateGlobalScore()}", Margin = new Thickness(0, 8, 0, 0) });

        Theme.Style(_stack);
    }
}
