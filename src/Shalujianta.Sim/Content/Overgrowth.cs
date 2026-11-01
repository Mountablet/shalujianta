using Shalujianta.Sim.Cards.Status;
using Shalujianta.Sim.Catalog;
using Shalujianta.Sim.Combat;
using Shalujianta.Sim.Monsters.Overgrowth;

namespace Shalujianta.Sim.Content;

/// <summary>
/// Overgrowth (Act 1 密林) hallway encounters, wiki values 2026-11-01 (see
/// catalog/patches/overgrowth_moves.json). Ruby Raiders (5 raider types, random
/// trio) is the one hallway encounter still missing; Dense Vegetation is event-only.
/// </summary>
public static class Overgrowth
{
    private static bool _registered;

    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;
        Slimed.Register();
        Wound.Register();
        Dazed.Register();
        Infection.Register();
        Beckon.Register();
    }

    public static MonsterSpawn Nibbit() => new()
    {
        CatalogId = MonsterIds.Nibbit,
        HpMin = NibbitAi.HpMin,
        HpMax = NibbitAi.HpMax,
        AiFactory = () => new NibbitAi(),
    };

    public static MonsterSpawn LeafSlime(bool medium) => new()
    {
        CatalogId = medium ? MonsterIds.LeafSlimeM : MonsterIds.LeafSlimeS,
        HpMin = medium ? LeafSlimeAi.HpMinM : LeafSlimeAi.HpMinS,
        HpMax = medium ? LeafSlimeAi.HpMaxM : LeafSlimeAi.HpMaxS,
        AiFactory = () => new LeafSlimeAi(medium),
    };

    public static MonsterSpawn TwigSlime(bool medium) => new()
    {
        CatalogId = medium ? MonsterIds.TwigSlimeM : MonsterIds.TwigSlimeS,
        HpMin = medium ? TwigSlimeAi.HpMinM : TwigSlimeAi.HpMinS,
        HpMax = medium ? TwigSlimeAi.HpMaxM : TwigSlimeAi.HpMaxS,
        AiFactory = () => new TwigSlimeAi(medium),
    };

    public static MonsterSpawn ShrinkerBeetle() => new()
    {
        CatalogId = MonsterIds.ShrinkerBeetle,
        HpMin = ShrinkerBeetleAi.HpMin,
        HpMax = ShrinkerBeetleAi.HpMax,
        AiFactory = () => new ShrinkerBeetleAi(),
    };

    public static MonsterSpawn FuzzyWurmCrawler() => new()
    {
        CatalogId = MonsterIds.FuzzyWurmCrawler,
        HpMin = FuzzyWurmCrawlerAi.HpMin,
        HpMax = FuzzyWurmCrawlerAi.HpMax,
        AiFactory = () => new FuzzyWurmCrawlerAi(),
    };

    public static MonsterSpawn Inklet() => new()
    {
        CatalogId = MonsterIds.Inklet,
        HpMin = InkletAi.HpMin,
        HpMax = InkletAi.HpMax,
        AiFactory = () => new InkletAi(),
        StartingPowers = [new PowerStack(PowerIds.Slippery, 1, JustApplied: false)],
    };

    public static MonsterSpawn Mawler() => new()
    {
        CatalogId = MonsterIds.Mawler,
        HpMin = MawlerAi.HpMin,
        HpMax = MawlerAi.HpMax,
        AiFactory = () => new MawlerAi(),
    };

    public static MonsterSpawn Flyconid() => new()
    {
        CatalogId = MonsterIds.Flyconid,
        HpMin = FlyconidAi.HpMin,
        HpMax = FlyconidAi.HpMax,
        AiFactory = () => new FlyconidAi(),
    };

    public static MonsterSpawn SnappingJaxfruit() => new()
    {
        CatalogId = MonsterIds.SnappingJaxfruit,
        HpMin = SnappingJaxfruitAi.HpMin,
        HpMax = SnappingJaxfruitAi.HpMax,
        AiFactory = () => new SnappingJaxfruitAi(),
    };

    public static MonsterSpawn VineShambler() => new()
    {
        CatalogId = MonsterIds.VineShambler,
        HpMin = VineShamblerAi.HpMin,
        HpMax = VineShamblerAi.HpMax,
        AiFactory = () => new VineShamblerAi(),
    };

    public static MonsterSpawn Fogmog() => new()
    {
        CatalogId = MonsterIds.Fogmog,
        HpMin = FogmogAi.HpMin,
        HpMax = FogmogAi.HpMax,
        AiFactory = () => new FogmogAi(),
    };

    public static MonsterSpawn CubexConstruct() => new()
    {
        CatalogId = MonsterIds.CubexConstruct,
        HpMin = CubexConstructAi.HpMin,
        HpMax = CubexConstructAi.HpMax,
        AiFactory = () => new CubexConstructAi(),
        StartingPowers = [new PowerStack(PowerIds.Artifact, 1, JustApplied: false)],
    };

    public static MonsterSpawn SlitheringStrangler() => new()
    {
        CatalogId = MonsterIds.SlitheringStrangler,
        HpMin = SlitheringStranglerAi.HpMin,
        HpMax = SlitheringStranglerAi.HpMax,
        AiFactory = () => new SlitheringStranglerAi(),
    };

    public static MonsterSpawn BygoneEffigy() => new()
    {
        CatalogId = MonsterIds.BygoneEffigy,
        HpMin = BygoneEffigyAi.HpMin,
        HpMax = BygoneEffigyAi.HpMax,
        AiFactory = () => new BygoneEffigyAi(),
        StartingPowers = [new PowerStack(PowerIds.Slow, 1, JustApplied: false)],
    };

    public static MonsterSpawn Byrdonis() => new()
    {
        CatalogId = MonsterIds.Byrdonis,
        HpMin = ByrdonisAi.HpMin,
        HpMax = ByrdonisAi.HpMax,
        AiFactory = () => new ByrdonisAi(),
        StartingPowers = [new PowerStack(PowerIds.Territorial, 1, JustApplied: false)],
    };

    public static MonsterSpawn PhrogParasite() => new()
    {
        CatalogId = MonsterIds.PhrogParasite,
        HpMin = PhrogParasiteAi.HpMin,
        HpMax = PhrogParasiteAi.HpMax,
        AiFactory = () => new PhrogParasiteAi(),
    };

    public static MonsterSpawn CeremonialBeast() => new()
    {
        CatalogId = MonsterIds.CeremonialBeast,
        HpMin = CeremonialBeastAi.HpMin,
        HpMax = CeremonialBeastAi.HpMax,
        AiFactory = () => new CeremonialBeastAi(),
    };

    public static MonsterSpawn KinPriest() => new()
    {
        CatalogId = MonsterIds.KinPriest,
        HpMin = KinPriestAi.HpMin,
        HpMax = KinPriestAi.HpMax,
        AiFactory = () => new KinPriestAi(),
    };

    public static MonsterSpawn KinFollower() => new()
    {
        CatalogId = MonsterIds.KinFollower,
        HpMin = KinFollowerAi.HpMin,
        HpMax = KinFollowerAi.HpMax,
        AiFactory = () => new KinFollowerAi(),
    };

    public static MonsterSpawn Vantom() => new()
    {
        CatalogId = MonsterIds.Vantom,
        HpMin = VantomAi.HpMin,
        HpMax = VantomAi.HpMax,
        AiFactory = () => new VantomAi(),
        StartingPowers = [new PowerStack(PowerIds.Slippery, 9, JustApplied: false)],
    };

    /// <summary>All implemented Overgrowth hallway encounters by bridge key.</summary>
    public static readonly IReadOnlyDictionary<string, (int EncounterId, Func<List<MonsterSpawn>> Build)> Hallway
        = new Dictionary<string, (int, Func<List<MonsterSpawn>>)>
        {
            // Easy pool (first 3 fights draw from here).
            ["Ov_A_Lone_Nibbit"] = (OvergrowthEncounterIds.ALoneNibbit, () => [Nibbit()]),
            ["Ov_Group_of_Slimes"] = (OvergrowthEncounterIds.GroupOfSlimes, () =>
            [
                LeafSlime(medium: false),
                LeafSlime(medium: true),
                TwigSlime(medium: false),
            ]),
            ["Ov_Shrinker_Beetle"] = (OvergrowthEncounterIds.ShrinkerBeetle, () => [ShrinkerBeetle()]),
            ["Ov_Fuzzy_Wurm_Crawler"] = (OvergrowthEncounterIds.FuzzyWurmCrawler, () => [FuzzyWurmCrawler()]),
            // Normal pool.
            ["Ov_Inklets"] = (OvergrowthEncounterIds.Inklets, () => [Inklet(), Inklet(), Inklet()]),
            ["Ov_Mawler"] = (OvergrowthEncounterIds.Mawler, () => [Mawler()]),
            ["Ov_A_Pair_of_Nibbits"] = (OvergrowthEncounterIds.APairOfNibbits, () => [Nibbit(), Nibbit()]),
            ["Ov_Swarm_of_Slimes"] = (OvergrowthEncounterIds.SwarmOfSlimes, () =>
            [
                LeafSlime(medium: true),
                TwigSlime(medium: true),
                LeafSlime(medium: false),
                TwigSlime(medium: false),
            ]),
            ["Ov_Shrink_Slime"] = (OvergrowthEncounterIds.ShroomAndSlime, () =>
            [
                // One random medium slime: v0 fixes Leaf M (documented simplification).
                LeafSlime(medium: true),
                Flyconid(),
            ]),
            ["Ov_Overgrowth_Flora"] = (OvergrowthEncounterIds.OvergrowthFlora, () =>
            [
                SnappingJaxfruit(),
                Flyconid(),
            ]),
            ["Ov_Fogmog"] = (OvergrowthEncounterIds.Fogmog, () => [Fogmog()]),
            ["Ov_Cubex_Construct"] = (OvergrowthEncounterIds.CubexConstruct, () => [CubexConstruct()]),
            ["Ov_Vine_Shambler"] = (OvergrowthEncounterIds.VineShambler, () => [VineShambler()]),
            ["Ov_Overgrowth_Crawlers"] = (OvergrowthEncounterIds.OvergrowthCrawlers, () =>
            [
                ShrinkerBeetle(),
                FuzzyWurmCrawler(),
            ]),
            ["Ov_Strangler_and_Friend"] = (OvergrowthEncounterIds.StranglerAndFriend, () =>
            [
                // One of {Jaxfruit, medium slime, S slime + S twig}: v0 fixes Jaxfruit.
                SlitheringStrangler(),
                SnappingJaxfruit(),
            ]),
            // ---- Underdocks hallway pool (Act 1b) ----
            ["Ud_Corpse_Slugs"] = (UnderdocksEncounterIds.CorpseSlugs, () => [Underdocks.CorpseSlug(), Underdocks.CorpseSlug()]),
            ["Ud_Seapunk"] = (UnderdocksEncounterIds.Seapunk, () => [Underdocks.Seapunk()]),
            ["Ud_Sludge_Spinner"] = (UnderdocksEncounterIds.SludgeSpinner, () => [Underdocks.SludgeSpinner()]),
            ["Ud_Toadpoles"] = (UnderdocksEncounterIds.Toadpoles, () => [Underdocks.Toadpole(), Underdocks.Toadpole()]),
            ["Ud_Many_Corpse_Slugs"] = (UnderdocksEncounterIds.ManyCorpseSlugs, () =>
                [Underdocks.CorpseSlug(), Underdocks.CorpseSlug(), Underdocks.CorpseSlug()]),
            ["Ud_Cultists"] = (UnderdocksEncounterIds.Cultists, () => [Underdocks.CalcifiedCultist(), Underdocks.DampCultist()]),
            ["Ud_Living_Fog"] = (UnderdocksEncounterIds.LivingFog, () => [Underdocks.LivingFog()]),
            ["Ud_Fossil_Stalker"] = (UnderdocksEncounterIds.FossilStalker, () => [Underdocks.FossilStalker()]),
            ["Ud_Two_Gremlins_in_a_Trenchcoat"] = (UnderdocksEncounterIds.TwoGremlins, () => [Underdocks.GremlinMerc()]),
            ["Ud_Haunted_Ship"] = (UnderdocksEncounterIds.HauntedShip, () => [Underdocks.HauntedShip()]),
            ["Ud_Punch_Construct"] = (UnderdocksEncounterIds.PunchConstruct, () => [Underdocks.PunchConstruct()]),
            ["Ud_Sewer_Clam"] = (UnderdocksEncounterIds.SewerClam, () => [Underdocks.SewerClam()]),
            ["Ud_Underdocks_Wildlife"] = (UnderdocksEncounterIds.Wildlife, () => [Underdocks.CalcifiedCultist(), Underdocks.Seapunk()]),
            ["Ud_Two_Tailed_Rats"] = (UnderdocksEncounterIds.TwoTailedRats, () => UnderdocksAct1.TwoTailedRatTrio()),
            // ---- Underdocks elites + bosses ----
            ["UdE_Skulking_Colony"] = (UnderdocksEncounterIds.ESkulkingColony, () => [Underdocks.SkulkingColony()]),
            ["UdE_Phantasmal_Gardener"] = (UnderdocksEncounterIds.EPhantasmalGardener, () =>
                [Underdocks.PhantasmalGardener(), Underdocks.PhantasmalGardener(), Underdocks.PhantasmalGardener(), Underdocks.PhantasmalGardener()]),
            ["UdE_Terror_Eel"] = (UnderdocksEncounterIds.ETerrorEel, () => [Underdocks.TerrorEel()]),
            ["UdB_Waterfall_Giant"] = (UnderdocksEncounterIds.BWaterfallGiant, () => [Underdocks.WaterfallGiant()]),
            ["UdB_Soul_Fysh"] = (UnderdocksEncounterIds.BSoulFysh, () => [Underdocks.SoulFysh()]),
            ["UdB_Lagavulin_Matriarch"] = (UnderdocksEncounterIds.BLagavulinMatriarch, () => [Underdocks.LagavulinMatriarch()]),
            // ---- Overgrowth elites + bosses ----
            ["OvE_Bygone_Effigy"] = (OvergrowthEncounterIds.EBygoneEffigy, () => [BygoneEffigy()]),
            ["OvE_Byrdonis"] = (OvergrowthEncounterIds.EByrdonis, () => [Byrdonis()]),
            ["OvE_Phrog_Parasite"] = (OvergrowthEncounterIds.EPhrogParasite, () => [PhrogParasite()]),
            ["OvB_Ceremonial_Beast"] = (OvergrowthEncounterIds.BCeremonialBeast, () => [CeremonialBeast()]),
            ["OvB_The_Kin"] = (OvergrowthEncounterIds.BTheKin, () => [KinPriest(), KinFollower(), KinFollower()]),
            ["OvB_Vantom"] = (OvergrowthEncounterIds.BVantom, () => [Vantom()]),
        };

    /// <summary>Every implemented hallway encounter key (Underdocks + Overgrowth), sorted.</summary>
    public static readonly IReadOnlyList<string> AllEncounterKeys = BuildKeys();

    private static string[] BuildKeys()
    {
        var keys = new List<string> { "Act1_TwoTailedRats" };
        keys.AddRange(Hallway.Keys);
        keys.Sort(StringComparer.Ordinal);
        return [.. keys];
    }
}
