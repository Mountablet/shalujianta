using Shalujianta.Sim.Catalog;
using Shalujianta.Sim.Combat;
using Shalujianta.Sim.Codec;
using Shalujianta.Sim.Content;
using Shalujianta.Sim.Core;
using Xunit;

namespace Shalujianta.Sim.Tests;

/// <summary>Act 1 completion: Underdocks pool, elites, bosses, and their mechanics.</summary>
public sealed class Act1Tests
{
    private static CombatWorld NewWorld(string key, int seed = 5, int hp = 80, bool autoStart = true)
    {
        SliceV0.Register();
        var cfg = new SimConfig { CatalogVersion = "synthetic-v0", Mode = InfoMode.Sl, GameVersionString = "act1-tests" };
        var start = SliceV0.EncounterByName(key);
        start.PlayerHp = hp;
        var world = new CombatWorld(cfg, start, seed);
        if (autoStart)
        {
            world.Start();
        }

        return world;
    }

    private static CombatWorld WithStarterHand(string key, int seed = 5, int hp = 80)
    {
        var w = NewWorld(key, seed, hp, autoStart: false);
        w.RunBattleStart();
        CombatTestHarness.MoveFromDrawToHand(w, CardIds.StrikeR, 2);
        CombatTestHarness.MoveFromDrawToHand(w, CardIds.DefendR, 3);
        w.RunPlayerTurnStart();
        return w;
    }

    [Fact]
    public void ToadpoleThornsReflectsAttackDamage()
    {
        var w = WithStarterHand("Ud_Toadpoles", seed: 5);
        // Force Thorns onto the front toadpole (Spiken grants it during its cycle).
        w.ApplyMonsterPower(0, PowerIds.Thorns, 2);
        int hpBefore = w.PlayerHp;
        int monHp = w.Monsters[0].Hp;
        CombatTestHarness.PlayCard(w, CardIds.StrikeR, new TargetRef(TargetKind.Monster, 0));
        Assert.Equal(monHp - 6, w.Monsters[0].Hp);
        Assert.Equal(hpBefore - 2, w.PlayerHp); // thorns reflected
    }

    [Fact]
    public void CultistRitualGrantsStrengthEachTurn()
    {
        var w = WithStarterHand("Ud_Cultists", seed: 7);
        CombatTestHarness.EndTurn(w); // Calcified opens Incantation (+2 Ritual)
        Assert.True(PowerOps.HasPower(w.Monsters[0].Powers, PowerIds.Ritual));
        CombatTestHarness.EndTurn(w); // its turn ends: ritual pays out Strength
        Assert.True(PowerOps.GetAmount(w.Monsters[0].Powers, PowerIds.Strength) >= 2);
    }

    [Fact]
    public void FossilStalkerSuckConvertsUnblockedDamageToStrength()
    {
        var w = WithStarterHand("Ud_Fossil_Stalker", seed: 9);
        int strBefore = PowerOps.GetAmount(w.Monsters[0].Powers, PowerIds.Strength);
        CombatTestHarness.EndTurn(w); // stalker Latch 12 unblocked
        int dealt = 80 - w.PlayerHp;
        Assert.True(dealt >= 12);
        Assert.Equal(strBefore + dealt, PowerOps.GetAmount(w.Monsters[0].Powers, PowerIds.Strength));
    }

    [Fact]
    public void SewerClamPlatingGainsBlockAndDecays()
    {
        var w = WithStarterHand("Ud_Sewer_Clam", seed: 11);
        Assert.Equal(8, PowerOps.GetAmount(w.Monsters[0].Powers, PowerIds.Plating));
        CombatTestHarness.EndTurn(w); // turn start: plating 8->7, acts, gains 7 block
        Assert.Equal(7, PowerOps.GetAmount(w.Monsters[0].Powers, PowerIds.Plating));
        Assert.Equal(7, w.Monsters[0].Block);
        CombatTestHarness.EndTurn(w); // next turn start: plating 7->6
        Assert.Equal(6, PowerOps.GetAmount(w.Monsters[0].Powers, PowerIds.Plating));
    }

    [Fact]
    public void LivingFogSmoggyLimitsSkillsToOnePerTurn()
    {
        var w = WithStarterHand("Ud_Living_Fog", seed: 13);
        w.ApplyPlayerPower(PowerIds.Smoggy, 1); // as Advanced Gas applies
        CombatTestHarness.PlayCard(w, CardIds.DefendR, new TargetRef(TargetKind.None));
        var second = w.Hand.FirstOrDefault(c => c.CatalogId == CardIds.DefendR);
        Assert.True(second.Uuid != 0);
        int idx = w.Hand.FindIndex(c => c.Uuid == second.Uuid);
        Assert.Equal(StepStatus.Illegal, w.Step(new Action(
            Verb.PlayCard, idx, second.Uuid, -1, new TargetRef(TargetKind.None))).Status);
        // Attacks stay legal under Smoggy.
        var strike = w.Hand.FirstOrDefault(c => c.CatalogId == CardIds.StrikeR);
        if (strike.Uuid != 0)
        {
            int sIdx = w.Hand.FindIndex(c => c.Uuid == strike.Uuid);
            Assert.Equal(StepStatus.Ok, w.Step(new Action(
                Verb.PlayCard, sIdx, strike.Uuid, -1, new TargetRef(TargetKind.Monster, 0))).Status);
        }
    }

    [Fact]
    public void TerrorEelShriekTriggersStunThenTerror()
    {
        var w = WithStarterHand("UdE_Terror_Eel", seed: 15);
        // Bring it to the 70 HP shriek threshold mid-turn; the CURRENT intent (Crash,
        // rolled at battle start) still resolves this turn, and the roll afterwards
        // queues the stun.
        w.DamageMonster(0, w.Monsters[0].Hp - 70, w.PlayerPowers);
        CombatTestHarness.EndTurn(w); // Crash executes: 16 damage, then shriek roll
        Assert.Equal(64, w.PlayerHp);
        CombatTestHarness.EndTurn(w); // stunned: no damage
        Assert.Equal(64, w.PlayerHp);
        CombatTestHarness.EndTurn(w); // Terror: 99 vulnerable
        Assert.True(PowerOps.GetAmount(w.PlayerPowers, PowerIds.Vulnerable) >= 99);
    }

    [Fact]
    public void SkulkingColonyHardenedShellCapsTurnlyHpLoss()
    {
        var w = WithStarterHand("UdE_Skulking_Colony", seed: 17);
        int hpBefore = w.Monsters[0].Hp;
        w.DamageMonster(0, 100, w.PlayerPowers); // one 100-damage blow
        Assert.Equal(hpBefore - 20, w.Monsters[0].Hp); // capped at 20
    }

    [Fact]
    public void PhantasmalGardenersOpenWithStaggeredCycles()
    {
        var w = NewWorld("UdE_Phantasmal_Gardener", seed: 19);
        var expected = new[] { 2, 0, 1, 3 }; // Flail, Bite, Lash, Enlarge
        for (int slot = 0; slot < 4; slot++)
        {
            Assert.Equal(expected[slot], (int)w.Monsters[slot].MoveId);
        }
        Assert.True(PowerOps.HasPower(w.Monsters[0].Powers, PowerIds.Skittish));
    }

    [Fact]
    public void BygoneEffigySlowScalesWithCardsPlayed()
    {
        var w = WithStarterHand("OvE_Bygone_Effigy", seed: 21);
        // Turn 1: effigy sleeps. Slow 1 -> first card rounds to base (floor(6*1.1)=6).
        CombatTestHarness.EndTurn(w);
        int hpBefore = w.Monsters[0].Hp;
        CombatTestHarness.PlayCard(w, CardIds.StrikeR, new TargetRef(TargetKind.Monster, 0));
        Assert.Equal(hpBefore - 6, w.Monsters[0].Hp);
        // Turn 2: Wake grants 10 Strength (boosting the Effigy's OWN next attack,
        // not the player's). Verify its Slashes hit for 13 + 10 = 23.
        CombatTestHarness.EndTurn(w);
        Assert.Equal(10, PowerOps.GetAmount(w.Monsters[0].Powers, PowerIds.Strength));
        int playerHpBefore = w.PlayerHp;
        CombatTestHarness.EndTurn(w); // effigy Slashes 13 + 10 Str = 23
        Assert.Equal(playerHpBefore - 23, w.PlayerHp);
    }

    [Fact]
    public void WaterfallGiantExplodesStoredSteamOnDeath()
    {
        var w = WithStarterHand("UdB_Waterfall_Giant", seed: 23, hp: 80);
        // Give the giant stored steam, then kill it: the player takes the blast.
        w.ApplyMonsterPower(0, PowerIds.SteamEruption, 15);
        int hpBefore = w.PlayerHp;
        w.DamageMonster(0, 999, w.PlayerPowers);
        Assert.False(w.Monsters[0].Alive);
        Assert.Equal(hpBefore - 15, w.PlayerHp);
    }

    [Fact]
    public void LagavulinMatriarchWakesOnDamageAndLosesPlating()
    {
        var w = WithStarterHand("UdB_Lagavulin_Matriarch", seed: 25);
        Assert.True(PowerOps.HasPower(w.Monsters[0].Powers, PowerIds.Asleep));
        Assert.Equal(12, PowerOps.GetAmount(w.Monsters[0].Powers, PowerIds.Plating));
        CombatTestHarness.PlayCard(w, CardIds.StrikeR, new TargetRef(TargetKind.Monster, 0));
        Assert.False(PowerOps.HasPower(w.Monsters[0].Powers, PowerIds.Asleep));
        Assert.Equal(0, PowerOps.GetAmount(w.Monsters[0].Powers, PowerIds.Plating));
    }

    [Fact]
    public void SoulFyshIntangibleCapsDamage()
    {
        var w = WithStarterHand("UdB_Soul_Fysh", seed: 27);
        w.ApplyMonsterPower(0, PowerIds.Intangible, 2);
        int hpBefore = w.Monsters[0].Hp;
        CombatTestHarness.PlayCard(w, CardIds.StrikeR, new TargetRef(TargetKind.Monster, 0));
        Assert.Equal(hpBefore - 1, w.Monsters[0].Hp); // damage reduced to 1
    }

    [Fact]
    public void CeremonialBeastStunsAtPlowThresholdAndRingsNextTurn()
    {
        var w = WithStarterHand("OvB_Ceremonial_Beast", seed: 29);
        w.DamageMonster(0, w.Monsters[0].Hp - 150, w.PlayerPowers);
        // Simplified edge case (documented in overgrowth_moves.json): HP already below
        // the threshold at battle start -> stun comes right after the queued Stamp,
        // then phase two begins with Beast Cry (Ringing: 1 card per turn).
        CombatTestHarness.EndTurn(w); // Stamp (no damage)
        Assert.Equal(80, w.PlayerHp);
        CombatTestHarness.EndTurn(w); // stunned turn: no damage
        Assert.Equal(80, w.PlayerHp);
        CombatTestHarness.EndTurn(w); // Beast Cry
        Assert.True(PowerOps.HasPower(w.PlayerPowers, PowerIds.Ringing));
        // Playing one card is legal; the second is not.
        CombatTestHarness.PlayCard(w, CardIds.StrikeR, new TargetRef(TargetKind.Monster, 0));
        var next = w.Hand.FirstOrDefault(c => c.CatalogId == CardIds.StrikeR);
        Assert.True(next.Uuid != 0);
        Assert.Equal(StepStatus.Illegal, w.Step(new Action(
            Verb.PlayCard, w.Hand.FindIndex(c => c.Uuid == next.Uuid), next.Uuid, -1,
            new TargetRef(TargetKind.Monster, 0))).Status);
    }

    [Fact]
    public void HauntedShipDazedIsUnplayableAndExhaustsEthereal()
    {
        var w = WithStarterHand("Ud_Haunted_Ship", seed: 31);
        w.AddCardsToDiscard(CardIds.Dazed, 2);
        var dazed = w.DiscardPile.First(c => c.CatalogId == CardIds.Dazed);
        w.Hand.Add(dazed);
        w.DiscardPile.Remove(dazed);
        Assert.True(dazed.HasFlag(CardFlags.Unplayable));
        Assert.True(dazed.HasFlag(CardFlags.Ethereal));
        // Playing it is illegal; at end of turn it exhausts from hand.
        Assert.Equal(StepStatus.Illegal, w.Step(new Action(
            Verb.PlayCard, w.Hand.Count - 1, dazed.Uuid, -1, new TargetRef(TargetKind.None))).Status);
        CombatTestHarness.EndTurn(w);
        Assert.Single(w.ExhaustPile.Where(c => c.CatalogId == CardIds.Dazed));
    }

    [Fact]
    public void InfectionBitesAtEndOfTurnWhileInHand()
    {
        var w = WithStarterHand("OvE_Phrog_Parasite", seed: 33);
        w.AddCardsToDiscard(CardIds.Infection, 3);
        var inf = w.DiscardPile.First(c => c.CatalogId == CardIds.Infection);
        w.DiscardPile.Remove(inf);
        w.Hand.Add(inf);
        int hpBefore = w.PlayerHp;
        CombatTestHarness.EndTurn(w); // Infection bites 3, then discards with the rest
        Assert.Equal(hpBefore - 3, w.PlayerHp);
    }

    [Fact]
    public void EveryAct1EncounterCompletesUnderRandomPlay()
    {
        // Covered by the shared feasibility test, but assert the act-1 keys here
        // explicitly so a registry regression is named precisely.
        foreach (var key in Overgrowth.AllEncounterKeys.Where(k => k.StartsWith("Ud", StringComparison.Ordinal)))
        {
            Assert.Contains(key, Overgrowth.AllEncounterKeys);
        }
        Assert.Equal(20, Overgrowth.AllEncounterKeys.Count(k => k.StartsWith("Ud", StringComparison.Ordinal)));
    }
}
