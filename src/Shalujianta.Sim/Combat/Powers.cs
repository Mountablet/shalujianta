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
                powers[i] = powers[i] with
                {
                    Amount = powers[i].Amount + amount,
                    JustApplied = powers[i].JustApplied | justApplied,
                };
                return;
            }
        }

        powers.Add(new PowerStack(catalogId, amount, justApplied));
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
    /// debuffs (Vulnerable/Weak/Frail) lose one stack and drop at zero. Matches the
    /// STS convention that a debuff applied this turn still covers the next turn.
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
        catalogId is PowerIds.Vulnerable or PowerIds.Weak or PowerIds.Frail;
}

/// <summary>
/// Damage/block pipeline (§4.11 hook order; final authority is the dll, PR-014).
/// Damage: (base + Strength) -> x1.5 if defender Vulnerable -> x0.75 if attacker Weak -> floor.
/// Block: (base + Dexterity) -> x0.75 if Frail -> floor.
/// </summary>
public static class DamageMath
{
    public static int Outgoing(int baseDamage, List<PowerStack> attacker, List<PowerStack> defender)
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
        catalogId is PowerIds.Vulnerable or PowerIds.Weak or PowerIds.Frail or PowerIds.Poison;
}
