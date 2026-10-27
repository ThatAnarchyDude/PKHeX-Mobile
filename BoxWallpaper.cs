using PKHeX.Core;
using static PKHeX.Core.GameVersion;

namespace PKHeXMobile;

/// <summary>Chooses the box wallpaper art the same way the PC editor does (per game, with the shared/default fallbacks).</summary>
public static class BoxWallpaper
{
    private const string Default = "box_wp16xy";

    /// <summary>Asset name (without extension) of the wallpaper for a box.</summary>
    public static string Name(SaveFile sav, int box)
    {
        if (sav is not IBoxDetailWallpaper wp)
            return Default;
        var index = wp.GetBoxWallpaper(box) + 1; // wallpapers are numbered from 1 in the art
        var suffix = Suffix(sav.Version, sav.Context, index);
        return suffix is null ? Default : $"box_wp{index:00}{suffix}";
    }

    /// <summary>Asset name for a given wallpaper number (0-based, as stored in the save).</summary>
    public static string NameFor(SaveFile sav, int wallpaper)
    {
        var index = wallpaper + 1;
        var suffix = Suffix(sav.Version, sav.Context, index);
        return suffix is null ? Default : $"box_wp{index:00}{suffix}";
    }

    private static string? Suffix(GameVersion version, EntityContext context, int index) => context switch
    {
        EntityContext.Gen3 when version == E => "e",
        EntityContext.Gen3 when (version is FR or LG) && index > 12 => "frlg",
        EntityContext.Gen3 => "rs",
        EntityContext.Gen4 when (version is D or P) || index <= 16 => "dp",
        EntityContext.Gen4 when version == Pt => "pt",
        EntityContext.Gen4 => "hgss", // HeartGold, SoulSilver and Psyduck Yellow
        EntityContext.Gen5 => (version is B2 or W2) && index > 16 ? "b2w2" : "bw",
        EntityContext.Gen6 => (version is OR or AS) && index > 16 ? "ao" : "xy",
        EntityContext.Gen7 => "xy",
        _ => null,
    };
}
