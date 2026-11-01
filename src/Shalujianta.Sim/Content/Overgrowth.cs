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
