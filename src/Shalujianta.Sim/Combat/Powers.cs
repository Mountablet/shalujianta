namespace Shalujianta.Sim.Combat;

/// <summary>
/// POWER entity ids, synthetic-v0 catalog. Cross-checked against
/// catalog/generated/entity_ids.json by CombatKernelTests.PowerIdsMatchCatalog.
/// </summary>
public static class PowerIds
{
    public const int Artifact = 0x04000001;
    public const int Buffer = 0x04000002;
    public const int Dexterity = 0x04000003;
    public const int Frail = 0x04000004;
    public const int Poison = 0x04000005;
    public const int Strength = 0x04000006;
    public const int Vulnerable = 0x04000007;
    public const int Weak = 0x04000008;
    public const int Constrict = 0x04000009;
    public const int Shrink = 0x0400000A;
    public const int Slippery = 0x0400000B;
    public const int Tangled = 0x0400000C;
    public const int HardenedShell = 0x0400000D;
    public const int Intangible = 0x0400000E;
    public const int Plating = 0x0400000F;
    public const int Ritual = 0x04000010;
    public const int Skittish = 0x04000011;
    public const int Slow = 0x04000012;
    public const int SteamEruption = 0x04000013;
    public const int Suck = 0x04000014;
    public const int Territorial = 0x04000015;
    public const int Thorns = 0x04000016;
    public const int Vigor = 0x04000017;
    public const int Asleep = 0x04000018;
    public const int Smoggy = 0x04000019;
    public const int Ringing = 0x0400001A;
}

public readonly record struct PowerStack(int CatalogId, int Amount, bool JustApplied);

/// <summary>Power list ops shared by player and monsters (struct lists, no LINQ).</summary>
public static class PowerOps
{
    public static int GetAmount(List<PowerStack> powers, int catalogId)
    {
        for (int i = 0; i < powers.Count; i++)
        {
            if (powers[i].CatalogId == catalogId)
            {
                return powers[i].Amount;
            }
        }

        return 0;
    }

    public static bool HasPower(List<PowerStack> powers, int catalogId) => GetAmount(powers, catalogId) > 0;

    /// <summary>Stacks onto an existing row; justApplied survives stacking.</summary>
    public static void Apply(List<PowerStack> powers, int catalogId, int amount, bool justApplied = true)
    {
        for (int i = 0; i < powers.Count; i++)
        {
            if (powers[i].CatalogId == catalogId)
            {
                int total = powers[i].Amount + amount;
                if (total <= 0)
                {
                    powers.RemoveAt(i); // Soul Siphon-style negative stacks remove the row
                }
                else
                {
                    powers[i] = powers[i] with
                    {
                        Amount = total,
                        JustApplied = powers[i].JustApplied | justApplied,
                    };
                }
                return;
            }
        }

        if (amount > 0)
        {
            powers.Add(new PowerStack(catalogId, amount, justApplied));
        }
    }

    public static bool Remove(List<PowerStack> powers, int catalogId)
    {
        for (int i = 0; i < powers.Count; i++)
        {
            if (powers[i].CatalogId == catalogId)
            {
                powers.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    /// <summary>Lowers a stack by one, removing the row at zero. For Artifact consumption etc.</summary>
    public static bool Decrement(List<PowerStack> powers, int catalogId)
    {
        for (int i = 0; i < powers.Count; i++)
        {
            if (powers[i].CatalogId == catalogId && powers[i].Amount > 0)
            {
                if (powers[i].Amount - 1 <= 0)
                {
                    powers.RemoveAt(i);
                }
                else
                {
                    powers[i] = powers[i] with { Amount = powers[i].Amount - 1 };
                }

                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// End-of-owner-turn tick: just_applied powers only clear their flag; decayable
    /// debuffs (Vulnerable/Weak/Frail/Tangled) lose one stack and drop at zero. Matches
    /// the STS convention that a debuff applied this turn still covers the next turn.
    /// Slippery is consumed on trigger; Constrict is removed when its applier dies.
    /// </summary>
    public static void TickDecay(List<PowerStack> powers)
    {
        for (int i = powers.Count - 1; i >= 0; i--)
        {
            var p = powers[i];
            if (p.JustApplied)
            {
                powers[i] = p with { JustApplied = false };
                continue;
            }

            if (!IsDecayable(p.CatalogId))
            {
                continue;
            }

            if (p.Amount - 1 <= 0)
            {
                powers.RemoveAt(i);
            }
            else
            {
                powers[i] = p with { Amount = p.Amount - 1 };
            }
        }
    }

    private static bool IsDecayable(int catalogId) =>
        catalogId is PowerIds.Vulnerable or PowerIds.Weak or PowerIds.Frail or PowerIds.Tangled or PowerIds.Intangible;
}

/// <summary>
/// Damage/block pipeline (§4.11 hook order; final authority is the dll, PR-014).
/// Damage: (base + Strength) -> x1.5 if defender Vulnerable -> x0.75 if attacker Weak -> floor.
/// Block: (base + Dexterity) -> x0.75 if Frail -> floor.
/// </summary>
public static class DamageMath
{
    public static int Outgoing(int baseDamage, List<PowerStack> attacker, List<PowerStack> defender, int defenderSlowStacks = 0)
    {
        float d = baseDamage + PowerOps.GetAmount(attacker, PowerIds.Strength);
        if (PowerOps.HasPower(defender, PowerIds.Vulnerable))
        {
            d *= CombatRules.VulnerableMultiplier;
        }

        if (PowerOps.HasPower(attacker, PowerIds.Weak))
        {
            d *= CombatRules.WeakMultiplier;
        }

        if (PowerOps.HasPower(attacker, PowerIds.Shrink))
        {
            d *= CombatRules.ShrinkMultiplier;
        }

        if (defenderSlowStacks > 0)
        {
            d *= 1f + 0.1f * defenderSlowStacks; // Bygone Effigy: +10% per card played
        }

        return (int)MathF.Floor(d);
    }

    public static int BlockGain(int baseBlock, List<PowerStack> unit)
    {
        float b = baseBlock + PowerOps.GetAmount(unit, PowerIds.Dexterity);
        if (PowerOps.HasPower(unit, PowerIds.Frail))
        {
            b *= CombatRules.FrailMultiplier;
        }

        return (int)MathF.Floor(b);
    }

    /// <summary>Debuffs an Artifact holder can absorb (Poison stored but not ticked in v0).</summary>
    public static bool IsDebuff(int catalogId) =>
        catalogId is PowerIds.Vulnerable or PowerIds.Weak or PowerIds.Frail or PowerIds.Poison
            or PowerIds.Shrink or PowerIds.Tangled or PowerIds.Constrict or PowerIds.Slippery
            or PowerIds.Slow;
}
