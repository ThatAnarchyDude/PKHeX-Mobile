using PKHeX.Core;

namespace PKHeXMobile;

/// <summary>Builds a fully filled-in starting Pokemon (met data, ability, nickname, IVs, PP) for a save.</summary>
public static class NewPokemon
{
    public static PKM Create(SaveFile sav, int species, byte level = 5)
    {
        // Borrow origin details (version, language, met location) from an existing party member so they match what the game writes.
        var template = sav.PartyData.FirstOrDefault(p => p.Species != 0);

        var pk = sav.BlankPKM.Clone();
        pk.Species = (ushort)species;
        pk.CurrentLevel = level;
        pk.PID = (uint)Random.Shared.NextInt64(1, uint.MaxValue);
        pk.OriginalTrainerName = sav.OT;
        pk.TID16 = sav.TID16;
        pk.SID16 = sav.SID16;
        pk.OriginalTrainerGender = (byte)(sav.Gender & 1);
        pk.Ball = 4; // Poke Ball
        pk.MetLevel = level;
        pk.MetDate = DateOnly.FromDateTime(DateTime.Today);
        pk.CurrentFriendship = 70;
        if (template is not null)
        {
            pk.Version = template.Version;
            pk.Language = template.Language;
            pk.MetLocation = template.MetLocation;
            if (template is G4PKM t && pk is G4PKM g)
            {
                // Copy the exact origin fields the game wrote so the Pokemon looks like an in-game catch.
                g.MetLocationExtended = t.MetLocationExtended;
                g.BallDPPt = t.BallDPPt;
                g.BallHGSS = t.BallHGSS;
                g.GroundTile = t.GroundTile;
            }
        }

        pk.SetRandomIVs();
        pk.RefreshAbility(0);
        pk.Nickname = Names.At(Names.Species(sav), species);
        pk.IsNicknamed = false;
        SetStartingMoves(sav, pk, level);
        pk.HealPP();
        pk.ResetPartyStats();
        pk.RefreshChecksum();

        // Gen 4 ties gender, ability slot and nature to the PID; reroll until the legality checker accepts the combination.
        for (int i = 0; i < 300; i++)
        {
            pk.PID = (uint)Random.Shared.NextInt64(1, uint.MaxValue);
            pk.Gender = EntityGender.GetFromPID(pk.Species, pk.PID);
            pk.RefreshAbility(0);
            pk.ResetPartyStats();
            pk.RefreshChecksum();
            if (new LegalityAnalysis(pk, sav.Personal).Valid)
                break;
        }
        return pk;
    }

    /// <summary>Gives the most recent level-up moves for the species (Psyduck Yellow reads the game's own learnsets).</summary>
    private static void SetStartingMoves(SaveFile sav, PKM pk, byte level)
    {
        var moves = new ushort[4];
        if (sav.Version == GameVersion.PY && pk.Species < PsyduckYellowExpanded.Learnsets.Length)
        {
            var learned = PsyduckYellowExpanded.Learnsets[pk.Species].GetMoveRange(level);
            var tail = learned.Length > 4 ? learned[^4..] : learned;
            tail.CopyTo(moves);
        }
        if (moves[0] == 0)
            moves[0] = 1;
        pk.SetMoves(moves);
    }
}
