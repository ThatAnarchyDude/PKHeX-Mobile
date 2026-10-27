using PKHeX.Core;

namespace PKHeXMobile;

/// <summary>Builds a Generation 4 PID that agrees with the chosen gender, nature, ability slot and shininess (what the game derives from them).</summary>
public static class PidTools
{
    /// <summary>Sets <see cref="PKM.PID"/>, gender and ability slot on the Pokemon. Returns false if no PID was found.</summary>
    public static bool Recalculate(PKM pk, byte gender, Nature nature, int abilitySlot, bool shiny)
    {
        var ratio = pk.PersonalInfo.Gender;
        var tidsid = (uint)(pk.TID16 ^ pk.SID16);
        var rng = Random.Shared;

        for (int i = 0; i < 3_000_000; i++)
        {
            var lo = (uint)rng.Next(0x10000);
            var hi = shiny ? ((tidsid ^ lo) & ~7u) | (uint)rng.Next(8) : (uint)rng.Next(0x10000);
            var pid = (hi << 16) | lo;

            if (pid % 25 != (uint)nature)
                continue;
            if (shiny != ((tidsid ^ hi ^ lo) < 8))
                continue;
            if (EntityGender.GetFromPIDAndRatio(pid, ratio) != gender)
                continue;

            pk.PID = pid;
            pk.Gender = gender;
            pk.RefreshAbility(abilitySlot);
            pk.RefreshChecksum();
            return true;
        }
        return false;
    }
}
