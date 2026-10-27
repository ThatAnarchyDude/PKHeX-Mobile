using PKHeX.Core;

namespace PKHeXMobile;

/// <summary>Name lookups that use the Psyduck Yellow tables for PY saves and stock strings otherwise.</summary>
public static class Names
{
    public static string[] Species(SaveFile sav) =>
        sav.Version == GameVersion.PY ? PsyduckYellowExpanded.SpeciesNames : GameInfo.Strings.specieslist;

    public static string[] Moves(SaveFile sav) =>
        sav.Version == GameVersion.PY ? PsyduckYellowExpanded.MoveNames : GameInfo.Strings.movelist;

    public static string[] Items(SaveFile sav) =>
        sav.Version == GameVersion.PY ? PsyduckYellowExpanded.ItemNames : GameInfo.Strings.itemlist;

    public static string[] Abilities(SaveFile sav) =>
        sav.Version == GameVersion.PY ? PsyduckYellowExpanded.AbilityNames : GameInfo.Strings.abilitylist;

    public static string At(string[] list, int index) =>
        (uint)index < (uint)list.Length ? list[index] : $"#{index}";
}
