using PKHeX.Core;

namespace PKHeXMobile;

/// <summary>Copies how/where/when a Pokemon was obtained from another Pokemon, so an edited or evolved one matches a real encounter.</summary>
public static class EncounterCopy
{
    public static void Apply(PKM target, PKM source)
    {
        // Origin: game, language and trainer identity.
        target.Version = source.Version;
        target.Language = source.Language;
        target.OriginalTrainerName = source.OriginalTrainerName;
        target.OriginalTrainerGender = source.OriginalTrainerGender;
        target.TID16 = source.TID16;
        target.SID16 = source.SID16;
        target.OriginalTrainerFriendship = source.OriginalTrainerFriendship;

        // Met data.
        target.MetLocation = source.MetLocation;
        target.MetLevel = source.MetLevel;
        target.MetYear = source.MetYear;
        target.MetMonth = source.MetMonth;
        target.MetDay = source.MetDay;
        target.FatefulEncounter = source.FatefulEncounter;

        // Egg data.
        target.IsEgg = source.IsEgg;
        target.EggLocation = source.EggLocation;
        target.EggYear = source.EggYear;
        target.EggMonth = source.EggMonth;
        target.EggDay = source.EggDay;

        if (target is G4PKM t && source is G4PKM s)
        {
            t.MetLocationExtended = s.MetLocationExtended;
            t.EggLocationExtended = s.EggLocationExtended;
            t.GroundTile = s.GroundTile;
            t.BallDPPt = s.BallDPPt;
            t.BallHGSS = s.BallHGSS;
        }
        else
            target.Ball = source.Ball;

        target.RefreshChecksum();
    }
}
