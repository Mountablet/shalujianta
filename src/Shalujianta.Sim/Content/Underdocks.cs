using Shalujianta.Sim.Catalog;
using Shalujianta.Sim.Combat;
using Shalujianta.Sim.Monsters;
using Shalujianta.Sim.Monsters.Underdocks;

namespace Shalujianta.Sim.Content;

/// <summary>
/// Underdocks (Act 1 码头) monster spawn factories, wiki values 2026-11-01 (see
/// catalog/patches/underdocks_moves.json).
/// </summary>
public static class Underdocks
{
    public static MonsterSpawn CorpseSlug() => new()
    {
        CatalogId = MonsterIds.CorpseSlug,
        HpMin = CorpseSlugAi.HpMin,
        HpMax = CorpseSlugAi.HpMax,
        AiFactory = () => new CorpseSlugAi(),
    };

    public static MonsterSpawn Seapunk() => new()
    {
        CatalogId = MonsterIds.Seapunk,
        HpMin = SeapunkAi.HpMin,
        HpMax = SeapunkAi.HpMax,
        AiFactory = () => new SeapunkAi(),
    };

    public static MonsterSpawn SludgeSpinner() => new()
    {
        CatalogId = MonsterIds.SludgeSpinner,
        HpMin = SludgeSpinnerAi.HpMin,
        HpMax = SludgeSpinnerAi.HpMax,
        AiFactory = () => new SludgeSpinnerAi(),
    };

    public static MonsterSpawn Toadpole() => new()
    {
        CatalogId = MonsterIds.Toadpole,
        HpMin = ToadpoleAi.HpMin,
        HpMax = ToadpoleAi.HpMax,
        AiFactory = () => new ToadpoleAi(),
    };

    public static MonsterSpawn CalcifiedCultist() => new()
    {
        CatalogId = MonsterIds.CalcifiedCultist,
        HpMin = CalcifiedCultistAi.HpMin,
        HpMax = CalcifiedCultistAi.HpMax,
        AiFactory = () => new CalcifiedCultistAi(),
    };

    public static MonsterSpawn DampCultist() => new()
    {
        CatalogId = MonsterIds.DampCultist,
        HpMin = DampCultistAi.HpMin,
        HpMax = DampCultistAi.HpMax,
        AiFactory = () => new DampCultistAi(),
    };

    public static MonsterSpawn LivingFog() => new()
    {
        CatalogId = MonsterIds.LivingFog,
        HpMin = LivingFogAi.HpMin,
        HpMax = LivingFogAi.HpMax,
        AiFactory = () => new LivingFogAi(),
    };

    public static MonsterSpawn FossilStalker() => new()
    {
        CatalogId = MonsterIds.FossilStalker,
        HpMin = FossilStalkerAi.HpMin,
        HpMax = FossilStalkerAi.HpMax,
        AiFactory = () => new FossilStalkerAi(),
        StartingPowers = [new PowerStack(PowerIds.Suck, 3, JustApplied: false)],
    };

    public static MonsterSpawn GremlinMerc() => new()
    {
        CatalogId = MonsterIds.GremlinMerc,
        HpMin = GremlinMercAi.HpMin,
        HpMax = GremlinMercAi.HpMax,
        AiFactory = () => new GremlinMercAi(),
    };

    public static MonsterSpawn HauntedShip() => new()
    {
        CatalogId = MonsterIds.HauntedShip,
        HpMin = HauntedShipAi.HpMin,
        HpMax = HauntedShipAi.HpMax,
        AiFactory = () => new HauntedShipAi(),
    };

    public static MonsterSpawn PunchConstruct() => new()
    {
        CatalogId = MonsterIds.PunchConstruct,
        HpMin = PunchConstructAi.HpMin,
        HpMax = PunchConstructAi.HpMax,
        AiFactory = () => new PunchConstructAi(),
        StartingPowers = [new PowerStack(PowerIds.Artifact, 1, JustApplied: false)],
    };

    public static MonsterSpawn SewerClam() => new()
    {
        CatalogId = MonsterIds.SewerClam,
        HpMin = SewerClamAi.HpMin,
        HpMax = SewerClamAi.HpMax,
        AiFactory = () => new SewerClamAi(),
        StartingPowers = [new PowerStack(PowerIds.Plating, 8, JustApplied: false)],
    };

    public static MonsterSpawn AxeRaider() => new()
    {
        CatalogId = MonsterIds.AxeRaider,
        HpMin = AxeRaiderAi.HpMin,
        HpMax = AxeRaiderAi.HpMax,
        AiFactory = () => new AxeRaiderAi(),
    };

    public static MonsterSpawn AssassinRaider() => new()
    {
        CatalogId = MonsterIds.AssassinRaider,
        HpMin = AssassinRaiderAi.HpMin,
        HpMax = AssassinRaiderAi.HpMax,
        AiFactory = () => new AssassinRaiderAi(),
    };

    public static MonsterSpawn BruteRaider() => new()
    {
        CatalogId = MonsterIds.BruteRaider,
        HpMin = BruteRaiderAi.HpMin,
        HpMax = BruteRaiderAi.HpMax,
        AiFactory = () => new BruteRaiderAi(),
    };

    public static MonsterSpawn CrossbowRaider() => new()
    {
        CatalogId = MonsterIds.CrossbowRaider,
        HpMin = CrossbowRaiderAi.HpMin,
        HpMax = CrossbowRaiderAi.HpMax,
        AiFactory = () => new CrossbowRaiderAi(),
    };

    public static MonsterSpawn TrackerRaider() => new()
    {
        CatalogId = MonsterIds.TrackerRaider,
        HpMin = TrackerRaiderAi.HpMin,
        HpMax = TrackerRaiderAi.HpMax,
        AiFactory = () => new TrackerRaiderAi(),
    };

    public static MonsterSpawn SkulkingColony() => new()
    {
        CatalogId = MonsterIds.SkulkingColony,
        HpMin = SkulkingColonyAi.HpMin,
        HpMax = SkulkingColonyAi.HpMax,
        AiFactory = () => new SkulkingColonyAi(),
        StartingPowers = [new PowerStack(PowerIds.HardenedShell, 20, JustApplied: false)],
    };

    public static MonsterSpawn PhantasmalGardener() => new()
    {
        CatalogId = MonsterIds.PhantasmalGardener,
        HpMin = PhantasmalGardenerAi.HpMin,
        HpMax = PhantasmalGardenerAi.HpMax,
        AiFactory = () => new PhantasmalGardenerAi(),
        StartingPowers = [new PowerStack(PowerIds.Skittish, 6, JustApplied: false)],
    };

    public static MonsterSpawn TerrorEel() => new()
    {
        CatalogId = MonsterIds.TerrorEel,
        HpMin = TerrorEelAi.HpMin,
        HpMax = TerrorEelAi.HpMax,
        AiFactory = () => new TerrorEelAi(),
    };

    public static MonsterSpawn WaterfallGiant() => new()
    {
        CatalogId = MonsterIds.WaterfallGiant,
        HpMin = WaterfallGiantAi.HpMin,
        HpMax = WaterfallGiantAi.HpMax,
        AiFactory = () => new WaterfallGiantAi(),
    };

    public static MonsterSpawn SoulFysh() => new()
    {
        CatalogId = MonsterIds.SoulFysh,
        HpMin = SoulFyshAi.HpMin,
        HpMax = SoulFyshAi.HpMax,
        AiFactory = () => new SoulFyshAi(),
    };

    public static MonsterSpawn LagavulinMatriarch() => new()
    {
        CatalogId = MonsterIds.LagavulinMatriarch,
        HpMin = LagavulinMatriarchAi.HpMin,
        HpMax = LagavulinMatriarchAi.HpMax,
        AiFactory = () => new LagavulinMatriarchAi(),
        StartingPowers =
        [
            new PowerStack(PowerIds.Plating, 12, JustApplied: false),
            new PowerStack(PowerIds.Asleep, 3, JustApplied: false),
        ],
    };
}

/// <summary>Extra Underdocks compositions that live outside the frozen SliceV0 pair.</summary>
public static class UnderdocksAct1
{
    /// <summary>Live encounter: three Two-Tailed Rats (the SliceV0 eval pair stays x2).</summary>
    public static List<MonsterSpawn> TwoTailedRatTrio() =>
        [Rat(), Rat(), Rat()];

    public static MonsterSpawn Rat() => new()
    {
        CatalogId = MonsterIds.TwoTailedRat,
        HpMin = TwoTailedRatAi.HpMin,
        HpMax = TwoTailedRatAi.HpMax,
        AiFactory = () => new TwoTailedRatAi(),
    };
}
