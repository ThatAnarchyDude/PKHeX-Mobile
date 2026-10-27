using PKHeX.Core;

namespace PKHeXMobile;

/// <summary>Loads PKHeX's own sprite art (bundled in the app) for Pokemon, balls and overlays.</summary>
public static class Sprites
{
    private static readonly Dictionary<string, byte[]?> Cache = [];
    private static Dictionary<string, int>? _national;

    private static async Task<byte[]?> LoadAsync(string path)
    {
        if (Cache.TryGetValue(path, out var hit))
            return hit;
        byte[]? bytes = null;
        try
        {
            await using var s = await FileSystem.OpenAppPackageFileAsync(path);
            using var ms = new MemoryStream();
            await s.CopyToAsync(ms);
            bytes = ms.ToArray();
        }
        catch (Exception)
        {
            // missing sprite: caller falls back
        }
        Cache[path] = bytes;
        return bytes;
    }

    private static ImageSource? ToSource(byte[]? bytes) => bytes is null ? null : ImageSource.FromStream(() => new MemoryStream(bytes));

    public static async Task<ImageSource?> FileAsync(string name) => ToSource(await LoadAsync($"sprites/{name}"));

    /// <summary>Item icon: the game's own ROM icon for Psyduck Yellow, then PKHeX's item art, otherwise nothing.</summary>
    public static async Task<ImageSource?> ItemAsync(SaveFile sav, int item)
    {
        if (item <= 0)
            return null;
        if (sav.Version == GameVersion.PY && await LoadAsync($"sprites/items/py_{item}.png") is { } rom)
            return ToSource(rom);
        return ToSource(await LoadAsync($"sprites/items/bitem_{item}.png"));
    }

    /// <summary>Bag pouch icon, as on the PC editor's pouch tabs.</summary>
    public static Task<ImageSource?> PouchAsync(InventoryType type)
    {
        var name = type switch
        {
            InventoryType.KeyItems => "key",
            InventoryType.TMHMs => "tech",
            InventoryType.MailItems => "mail",
            InventoryType.Medicine => "medicine",
            InventoryType.Berries => "berries",
            InventoryType.Balls => "balls",
            InventoryType.BattleItems => "battle",
            InventoryType.PCItems => "pcitems",
            InventoryType.FreeSpace => "free",
            InventoryType.ZCrystals => "z",
            InventoryType.Candy => "candy",
            InventoryType.Treasure => "treasure",
            InventoryType.MegaStones => "mega",
            _ => "items",
        };
        return FileAsync($"bag/bag_{name}.png");
    }

    public static async Task<ImageSource?> BallAsync(int ball) => ball <= 0 ? null : ToSource(await LoadAsync($"sprites/ball/_ball{ball}.png"));

    /// <summary>National Dex number whose sprite stands in for the save's species (Psyduck Yellow uses its own internal numbering).</summary>
    private static int DisplaySpecies(PKM pk, SaveFile sav)
    {
        if (pk is not PK4 { IsExpandedEngine: true })
            return pk.Species;
        var shown = PsyduckYellowExpanded.GetDisplaySpecies(pk.Species);
        if (shown != 0)
            return shown;

        // "(Starter)" and "(Mega)" copies use their base Pokemon's art.
        var name = Names.At(PsyduckYellowExpanded.SpeciesNames, pk.Species);
        foreach (var suffix in new[] { " (Starter)", " (Mega)" })
            if (name.EndsWith(suffix, StringComparison.Ordinal))
                name = name[..^suffix.Length];
        if (name.Equals("Novareon", StringComparison.OrdinalIgnoreCase))
            return (int)Species.Umbreon; // its stats are an Umbreon placeholder, so borrow that sprite until it has its own art
        _national ??= GameInfo.GetStrings("en").specieslist
            .Select((n, i) => (n, i)).Where(t => t.n.Length > 0).GroupBy(t => t.n, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().i, StringComparer.OrdinalIgnoreCase);
        return _national.TryGetValue(name, out var id) ? id : 0;
    }

    public static async Task<ImageSource?> PokemonAsync(PKM pk, SaveFile sav)
    {
        if (pk.Species == 0)
            return null;
        if (pk.IsEgg)
            return await FileAsync("b_egg.png");

        var species = DisplaySpecies(pk, sav);
        var form = pk is PK4 { IsExpandedEngine: true } ? 0 : pk.Form;
        var s = pk.IsShiny ? "s" : string.Empty;
        var f = form > 0 ? $"-{form}" : string.Empty;
        foreach (var name in new[]
        {
            $"b_{species}{f}{s}.png", $"a_{species}{f}{s}.png", // exact form, shiny if possible
            $"b_{species}{s}.png", $"a_{species}{s}.png",       // base form
            $"b_{species}.png", $"a_{species}.png",             // no shiny art: use the normal colours
            "b_0.png",
        })
        {
            if (await LoadAsync($"sprites/{name}") is { } bytes)
                return ToSource(bytes);
        }
        return null;
    }
}
