using System.Reflection;
using PKHeX.Core;

namespace PKHeXMobile;

/// <summary>Maps Pokemon fields to the named lists the PC PKHeX shows in its dropdowns, so values can be picked by name.</summary>
public static class FieldChoices
{
    private static List<ComboItem> FromNames(string[] names) =>
        names.Select((n, i) => new ComboItem($"{n} ({i})", i)).ToList();

    private static List<ComboItem> Genders { get; } = [new("Male (0)", 0), new("Female (1)", 1), new("Genderless (2)", 2)];

    /// <summary>Returns the named choices for a property, or null when it is a plain number or text field.</summary>
    public static IReadOnlyList<ComboItem>? For(PropertyInfo prop, PKM pk, SaveFile sav)
    {
        var name = prop.Name;
        if (name is nameof(PKM.Species))
            return FromNames(Names.Species(sav));
        if (name is nameof(PKM.HeldItem))
            return FromNames(Names.Items(sav));
        if (name is nameof(PKM.Ability))
            return FromNames(Names.Abilities(sav));
        if (name.StartsWith("Move", StringComparison.Ordinal) && name.Length == 5 && char.IsDigit(name[4]) || name.StartsWith("RelearnMove", StringComparison.Ordinal))
            return FromNames(Names.Moves(sav));
        if (name is nameof(PKM.Ball) or "BallDPPt" or "BallHGSS")
            return GameInfo.Sources.BallDataSource;
        if (name is nameof(PKM.Language))
            return GameInfo.Sources.LanguageDataSource(pk.Generation, pk.Context);
        if (name is nameof(PKM.Version))
            return GameInfo.Sources.VersionDataSource;
        if (name is nameof(PKM.Gender) or nameof(PKM.OriginalTrainerGender) or "HandlingTrainerGender")
            return name is nameof(PKM.Gender) ? Genders : Genders.Take(2).ToList();
        if (name.StartsWith("MetLocation", StringComparison.Ordinal))
            return GameInfo.Sources.Met.GetLocationList(pk.Version, pk.Context, egg: false);
        if (name.StartsWith("EggLocation", StringComparison.Ordinal))
            return GameInfo.Sources.Met.GetLocationList(pk.Version, pk.Context, egg: true);
        if (name is nameof(PKM.Form))
        {
            var forms = sav.Version == GameVersion.PY
                ? PsyduckYellowExpanded.GetFormNames(pk.Species)
                : FormConverter.GetFormList(pk.Species, GameInfo.Strings.Types, GameInfo.Strings.forms, pk.Context);
            return FromNames(forms);
        }
        if (prop.PropertyType.IsEnum)
        {
            var type = prop.PropertyType;
            return Enum.GetValues(type).Cast<object>()
                .Select(v => new ComboItem($"{v} ({Convert.ToInt64(v)})", (int)Convert.ToInt64(v)))
                .DistinctBy(c => c.Value).ToList();
        }
        return null;
    }
}
