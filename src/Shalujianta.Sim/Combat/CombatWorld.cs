using Shalujianta.Sim.Codec;
using Shalujianta.Sim.Core;

namespace Shalujianta.Sim.Combat;

/// <summary>
/// Headless combat world: Step is deterministic and mutates in place (§0.3).
/// Turn machine (§4.11): BattleStart draws nothing; every PlayerTurnStart (including
/// the first) refills energy, clears block and draws 5; END_TURN resolves the whole
/// enemy turn inside the same Step. Clone() is SL-only (KD-16).
/// </summary>
public sealed class CombatWorld : ISimWorld
{
    private readonly SimConfig _config;
    private readonly CombatStart _start;
    private readonly MultiRng _rng;
    private readonly List<IMonsterAi> _ais = [];

    private CombatPhase _phase;
    private int _turn;
    private bool _terminal;
    private StepStatus _terminalStatus;

    private int _playerHp;
    private int _playerMaxHp;
    private int _playerBlock;
    private int _energy;
    private int _maxEnergy;
    private int _timesDamagedThisCombat;
    private int _cardsPlayedThisTurn;
    private int _zeroCostPlaysThisTurn;

    // Zones are public for test/golden-fixture wiring; the kernel mutates them
    // through its own paths. Struct instances: no per-card heap allocation.
    public List<CardInstance> Hand { get; } = [];
    public List<CardInstance> DrawPile { get; } = [];
    public List<CardInstance> DiscardPile { get; } = [];
    public List<CardInstance> ExhaustPile { get; } = [];
    public List<CardInstance> Limbo { get; } = [];
    public List<PowerStack> PlayerPowers { get; } = [];
    public List<MonsterInstance> Monsters { get; } = [];

    public SimConfig Config => _config;
    public InfoMode Mode => _config.Mode;
    public CombatPhase Phase => _phase;
    public int Turn => _turn;
    public int PlayerHp => _playerHp;
    public int PlayerMaxHp => _playerMaxHp;
    public int PlayerBlock => _playerBlock;
    public int Energy => _energy;
    public int MaxEnergy => _maxEnergy;
    public int CardsPlayedThisTurn => _cardsPlayedThisTurn;
    public int ZeroCostPlaysThisTurn => _zeroCostPlaysThisTurn;
    public bool ShufflePending => DrawPile.Count == 0 && DiscardPile.Count > 0;
    public bool IsTerminal => _terminal;

    /// <summary>Terminal status once IsTerminal; StepStatus.Ok before that.</summary>
    public StepStatus TerminalStatus => _terminal ? _terminalStatus : StepStatus.Ok;

    /// <summary>
    /// RNG handle for battle AI and relic hooks (streams: MonsterAi etc.). NoSL search
    /// worlds (PR-008) must never hand out the real world's rng.
    /// </summary>
    public MultiRng Rng => _rng;

    public CombatWorld(SimConfig config, CombatStart start, int runSeedI32)
    {
        _config = config;
        _start = start;
        _rng = new MultiRng(config.RngBackend);
        _rng.Seed(runSeedI32);

        _playerMaxHp = start.PlayerMaxHp;
        _playerHp = start.PlayerHp < 0 ? start.PlayerMaxHp : start.PlayerHp;
        _maxEnergy = start.MaxEnergy;
        _energy = start.MaxEnergy;

        foreach (var spawn in start.Monsters)
        {
            int hp = ResolveSpawnHp(spawn);
            Monsters.Add(new MonsterInstance
            {
                Slot = Monsters.Count,
                CatalogId = spawn.CatalogId,
                Hp = hp,
                MaxHp = spawn.MaxHp < 0 ? hp : spawn.MaxHp,
                Alive = true,
                IntentKnown = true,
                Powers = [],
            });
            _ais.Add(spawn.AiFactory());
        }
    }

    private int ResolveSpawnHp(MonsterSpawn spawn)
    {
        if (spawn.Hp > 0)
        {
            return spawn.Hp;
        }

        if (spawn.HpMin > 0 && spawn.HpMax >= spawn.HpMin)
        {
            return spawn.HpMin + _rng.NextInt(RngStream.MonsterAi, spawn.HpMax - spawn.HpMin + 1);
        }

        throw new InvalidOperationException($"monster spawn {spawn.CatalogId} needs Hp or a HpMin/HpMax range");
    }

    /// <summary>BattleStart + first PlayerTurnStart. Agents only ever see the post-turn-start state.</summary>
    public void Start()
    {
        RunBattleStart();
        RunPlayerTurnStart();
    }

    /// <summary>Shuffle deck (innate floats to top), relic OnCombatStart, roll opening intents. Draws nothing.</summary>
    public void RunBattleStart()
    {
        _phase = CombatPhase.Exec;
        _turn = 1; // the upcoming player turn is turn 1; it starts via RunPlayerTurnStart
        DrawPile.AddRange(_start.Deck);
        for (int i = DrawPile.Count - 1; i > 0; i--)
        {
            int j = _rng.NextInt(RngStream.Shuffle, i + 1);
            (DrawPile[i], DrawPile[j]) = (DrawPile[j], DrawPile[i]);
        }

        if (HasInnate())
        {
            List<CardInstance> innate = [];
            List<CardInstance> rest = [];
            foreach (var c in DrawPile)
            {
                if (c.HasFlag(CardFlags.Innate))
                {
                    innate.Add(c);
                }
                else
                {
                    rest.Add(c);
                }
            }

            DrawPile.Clear();
            DrawPile.AddRange(rest);
            DrawPile.AddRange(innate);
        }

        foreach (int relicId in _start.RelicIds)
        {
            if (RelicRegistry.TryGet(relicId, out var hooks))
            {
                hooks.OnCombatStart(this);
            }
            else
            {
                HookLog.NotImplemented(_config, $"relic:{relicId}:OnCombatStart");
            }
        }

        for (int slot = 0; slot < Monsters.Count; slot++)
        {
            RollIntent(slot);
        }
    }

    /// <summary>Every player turn, including turn 1 (§4.11): energy refill, block clear, draw 5.</summary>
    public void RunPlayerTurnStart()
    {
        _phase = CombatPhase.Player;
        _energy = _maxEnergy;
        _playerBlock = 0; // v0 has no block-persist power; Barricade-like lands with its power
        _cardsPlayedThisTurn = 0;
        _zeroCostPlaysThisTurn = 0;
        DrawCards(CombatRules.DrawPerTurn);

        foreach (int relicId in _start.RelicIds)
        {
            if (RelicRegistry.TryGet(relicId, out var hooks))
            {
                hooks.OnTurnStart(this);
            }
            else
            {
                HookLog.NotImplemented(_config, $"relic:{relicId}:OnTurnStart");
            }
        }
    }

    public StepResult Step(in Action action)
    {
        if (_terminal)
        {
            return new StepResult(StepStatus.Illegal, 0f, true);
        }

        return action.Verb switch
        {
            Verb.PlayCard => StepPlayCard(action),
            Verb.EndTurn => StepEndTurn(),
            Verb.Noop or Verb.Wait or Verb.State => new StepResult(StepStatus.Ok, 0f, false),
            _ => LogAndReject(action.Verb),
        };
    }

    private StepResult LogAndReject(Verb verb)
    {
        Log($"verb_not_supported_v0: {verb}");
        return new StepResult(StepStatus.Illegal, 0f, false);
    }

    private StepResult StepPlayCard(in Action action)
    {
        if (_phase != CombatPhase.Player)
        {
            return Illegal();
        }

        int idx = action.CardIndex;
        if (idx < 0 || idx >= Hand.Count)
        {
            return Illegal();
        }

        var card = Hand[idx];
        if (action.CardUuid != -1 && action.CardUuid != card.Uuid)
        {
            return Illegal();
        }

        if (!CardRegistry.TryGet(card.CatalogId, out var def))
        {
            return Illegal();
        }

        if (def.Apply is null)
        {
            HookLog.NotImplemented(_config, $"card:{card.CatalogId}:Apply");
            return Illegal();
        }

        if (card.HasFlag(CardFlags.Unplayable))
        {
            return Illegal();
        }

        if (card.HasFlag(CardFlags.XCost))
        {
            Log("x_cost_disabled_v0");
            return Illegal();
        }

        int cost = card.CurrentCost;
        if (cost > _energy)
        {
            return Illegal();
        }

        if (!TargetIsValid(def.Target, action.Target))
        {
            return Illegal();
        }

        // From here on the action commits.
        _energy -= cost;
        Hand.RemoveAt(idx);
        Limbo.Add(card);
        _cardsPlayedThisTurn++;

        if (cost == 0)
        {
            _zeroCostPlaysThisTurn++;
            if (_zeroCostPlaysThisTurn == CombatRules.ZeroCostWarn)
            {
                Log($"zero_cost_warn: {_zeroCostPlaysThisTurn} zero-cost plays without END_TURN");
            }

            if (_zeroCostPlaysThisTurn >= CombatRules.MaxZeroCostHardCut)
            {
                FinishCard(card);
                SetTerminal(StepStatus.StallCut);
                return new StepResult(StepStatus.StallCut, 0f, true);
            }
        }

        def.Apply(this, card, action.Target);
        FinishCard(card);

        if (_terminal)
        {
            return new StepResult(_terminalStatus, 0f, true);
        }

        if (AllMonstersDead())
        {
            SetTerminal(StepStatus.TerminalWin);
            return new StepResult(StepStatus.TerminalWin, 0f, true);
        }

        return new StepResult(StepStatus.Ok, 0f, false);
    }

    /// <summary>Move a resolved card out of limbo into exhaust/discard unless Apply consumed it.</summary>
    private void FinishCard(in CardInstance card)
    {
        int li = IndexOfUuid(Limbo, card.Uuid);
        if (li < 0)
        {
            return;
        }

        Limbo.RemoveAt(li);
        if (card.HasFlag(CardFlags.Exhausts))
        {
            ExhaustPile.Add(card);
        }
        else
        {
            DiscardPile.Add(card);
        }
    }

    private StepResult StepEndTurn()
    {
        if (_phase != CombatPhase.Player)
        {
            return Illegal();
        }

        _phase = CombatPhase.Exec;

        // PlayerTurnEnd: ethereal exhausts, retain stays, everything else discards.
        for (int i = Hand.Count - 1; i >= 0; i--)
        {
            var c = Hand[i];
            if (c.HasFlag(CardFlags.Ethereal))
            {
                Hand.RemoveAt(i);
                ExhaustPile.Add(c);
            }
            else if (!c.HasFlag(CardFlags.Retain))
            {
                Hand.RemoveAt(i);
                DiscardPile.Add(c);
            }
        }

        PowerOps.TickDecay(PlayerPowers);
        RunEnemyTurn();
        if (_terminal)
        {
            return new StepResult(_terminalStatus, 0f, true);
        }

        _turn++;
        if (_turn > _config.StallTurnLimit)
        {
            SetTerminal(StepStatus.StallCut);
            return new StepResult(StepStatus.StallCut, 0f, true);
        }

        RunPlayerTurnStart();
        if (AllMonstersDead())
        {
            SetTerminal(StepStatus.TerminalWin);
            return new StepResult(StepStatus.TerminalWin, 0f, true);
        }

        return new StepResult(StepStatus.Ok, 0f, false);
    }

    private void RunEnemyTurn()
    {
        for (int slot = 0; slot < Monsters.Count; slot++)
        {
            var m = Monsters[slot];
            if (!m.Alive)
            {
                continue;
            }

            m.Block = 0; // monster block clears when its own turn begins
            switch (m.Intent)
            {
                case IntentId.Attack:
                    int hits = Math.Max(1, m.MoveHits);
                    for (int h = 0; h < hits; h++)
                    {
                        DamagePlayer(m.MoveBaseDamage, m.Powers);
                        if (_terminal)
                        {
                            break;
                        }
                    }

                    break;
                case IntentId.Defend:
                    m.Block += DamageMath.BlockGain(m.MoveBaseDamage, m.Powers);
                    break;
                case IntentId.Buff or IntentId.Debuff:
                    var move = _ais[slot].MoveFor(this, slot, m.MoveId);
                    if (move.Effect is not null)
                    {
                        move.Effect(this, slot);
                    }
                    else
                    {
                        HookLog.NotImplemented(_config, $"monster:{m.CatalogId}:move:{m.MoveId}");
                    }

                    break;
                case IntentId.Unknown:
                    HookLog.NotImplemented(_config, $"monster:{m.CatalogId}:unknown_intent");
                    break;
            }

            PowerOps.TickDecay(m.Powers);
            Monsters[slot] = m;
            if (_terminal)
            {
                return;
            }

            RollIntent(slot);
        }
    }

    private void RollIntent(int slot)
    {
        var move = _ais[slot].ChooseNextMove(this, slot);
        var m = Monsters[slot];
        m.Intent = move.Intent;
        m.IntentKnown = true;
        m.MoveId = move.MoveId;
        m.MoveBaseDamage = move.BaseDamage;
        m.MoveHits = move.Hits;
        m.Misc++;
        Monsters[slot] = m;
    }

    // ---- damage / block entry points (used by card Apply and monster moves) ----

    public void DamageMonster(int slot, int baseDamage, List<PowerStack>? attackerPowers = null)
    {
        if (slot < 0 || slot >= Monsters.Count)
        {
            return;
        }

        var m = Monsters[slot];
        if (!m.Alive)
        {
            return;
        }

        int dmg = DamageMath.Outgoing(baseDamage, attackerPowers ?? PlayerPowers, m.Powers);
        int absorbed = Math.Min(m.Block, dmg);
        m.Block -= absorbed;
        dmg -= absorbed;
        if (dmg > 0)
        {
            m.Hp -= dmg;
        }

        if (m.Hp <= 0)
        {
            m.Hp = 0;
            m.Alive = false;
            Log($"monster_death: slot={m.Slot} id={m.CatalogId}");
        }

        Monsters[slot] = m;
    }

    public void DamagePlayer(int baseDamage, List<PowerStack>? attackerPowers = null)
    {
        if (_terminal)
        {
            return;
        }

        int dmg = DamageMath.Outgoing(baseDamage, attackerPowers ?? [], PlayerPowers);
        int absorbed = Math.Min(_playerBlock, dmg);
        _playerBlock -= absorbed;
        dmg -= absorbed;
        if (dmg > 0)
        {
            _playerHp -= dmg;
            _timesDamagedThisCombat++;
        }

        if (_playerHp <= 0)
        {
            _playerHp = 0;
            SetTerminal(StepStatus.TerminalLoss);
        }
    }

    public void GainPlayerBlock(int baseBlock) => _playerBlock += DamageMath.BlockGain(baseBlock, PlayerPowers);

    public void HealPlayer(int amount)
    {
        if (amount > 0)
        {
            _playerHp = Math.Min(_playerMaxHp, _playerHp + amount);
        }
    }

    /// <summary>
    /// Returns the card currently being resolved from limbo back to the hand
    /// (self-recasting loop cards). FinishCard then leaves the card alone.
    /// </summary>
    public void ReturnCardToHand(int cardUuid)
    {
        int li = IndexOfUuid(Limbo, cardUuid);
        if (li >= 0)
        {
            var c = Limbo[li];
            Limbo.RemoveAt(li);
            Hand.Add(c);
        }
    }

    /// <summary>Shuffle-stream call counter; battle-start shuffle counts as the first call.</summary>
    public int ShuffleCounter => _rng.Counter(RngStream.Shuffle);

    public void ApplyPlayerPower(int catalogId, int amount)
    {
        if (amount > 0 && DamageMath.IsDebuff(catalogId) && TryConsumeArtifact(PlayerPowers))
        {
            return;
        }

        PowerOps.Apply(PlayerPowers, catalogId, amount);
        if (catalogId == PowerIds.Poison)
        {
            Log("poison_stored_without_tick_v0");
        }
    }

    public void ApplyMonsterPower(int slot, int catalogId, int amount)
    {
        if (slot < 0 || slot >= Monsters.Count)
        {
            return;
        }

        var m = Monsters[slot];
        if (!m.Alive)
        {
            return;
        }

        if (amount > 0 && DamageMath.IsDebuff(catalogId) && TryConsumeArtifact(m.Powers))
        {
            Monsters[slot] = m;
            return;
        }

        PowerOps.Apply(m.Powers, catalogId, amount);
        if (catalogId == PowerIds.Poison)
        {
            Log("poison_stored_without_tick_v0");
        }

        Monsters[slot] = m;
    }

    private static bool TryConsumeArtifact(List<PowerStack> powers) =>
        PowerOps.Decrement(powers, PowerIds.Artifact);

    // ---- draw / shuffle ----

    private void DrawCards(int n)
    {
        for (int i = 0; i < n; i++)
        {
            if (Hand.Count >= CombatRules.HandCap)
            {
                Log("hand_overflow: draw skipped, card stays on draw pile");
                return;
            }

            if (DrawPile.Count == 0)
            {
                if (DiscardPile.Count == 0)
                {
                    return;
                }

                Reshuffle();
            }

            var top = DrawPile[^1];
            DrawPile.RemoveAt(DrawPile.Count - 1);
            Hand.Add(top);
        }
    }

    private void Reshuffle()
    {
        DrawPile.AddRange(DiscardPile);
        DiscardPile.Clear();
        for (int i = DrawPile.Count - 1; i > 0; i--)
        {
            int j = _rng.NextInt(RngStream.Shuffle, i + 1);
            (DrawPile[i], DrawPile[j]) = (DrawPile[j], DrawPile[i]);
        }

        Log("reshuffle");
    }

    // ---- legality ----

    public ActionMask LegalActions()
    {
        var mask = new ActionMask();
        if (_terminal || _phase != CombatPhase.Player)
        {
            return mask;
        }

        bool anyPlay = false;
        for (int i = 0; i < Hand.Count && i < ActionFlatten.MaxHand; i++)
        {
            var card = Hand[i];
            if (!CardRegistry.TryGet(card.CatalogId, out var def) || def.Apply is null)
            {
                continue;
            }

            if (card.HasFlag(CardFlags.Unplayable) || card.HasFlag(CardFlags.XCost))
            {
                continue;
            }

            if (card.CurrentCost > _energy)
            {
                continue;
            }

            mask.PlayableHand[i] = true;
            anyPlay = true;
            switch (def.Target)
            {
                case CardTarget.None:
                    mask.PlayTargets[i][0] = true;
                    break;
                case CardTarget.Enemy:
                    for (int s = 0; s < Monsters.Count && s < CombatRules.MaxMonsters; s++)
                    {
                        if (Monsters[s].Alive)
                        {
                            mask.PlayTargets[i][1 + s] = true;
                        }
                    }

                    break;
                case CardTarget.Player:
                    mask.PlayTargets[i][7] = true;
                    break;
                case CardTarget.AllEnemies:
                    mask.PlayTargets[i][8] = true;
                    break;
            }
        }

        mask.VerbsAllowed[(int)Verb.PlayCard] = anyPlay;
        mask.VerbsAllowed[(int)Verb.EndTurn] = true;
        mask.PopulateFlat();
        return mask;
    }

    private bool TargetIsValid(CardTarget cardTarget, TargetRef target)
    {
        return cardTarget switch
        {
            CardTarget.None => target.Kind == TargetKind.None,
            CardTarget.Enemy => target.Kind == TargetKind.Monster
                                && target.Index >= 0
                                && target.Index < Monsters.Count
                                && Monsters[target.Index].Alive,
            CardTarget.Player => target.Kind == TargetKind.Player,
            CardTarget.AllEnemies => target.Kind == TargetKind.AllEnemies,
            _ => false,
        };
    }

    // ---- observation ----

    public CombatObservation Observe()
    {
        var player = new PlayerObservation
        {
            Hp = _playerHp,
            MaxHp = _playerMaxHp,
            Block = _playerBlock,
            Energy = _energy,
            MaxEnergy = _maxEnergy,
            Powers = [.. PlayerPowers],
            CardsPlayedThisTurn = _cardsPlayedThisTurn,
            ZeroCostPlaysThisTurn = _zeroCostPlaysThisTurn,
            TimesDamagedThisCombat = _timesDamagedThisCombat,
        };

        var monsters = new List<MonsterObservation>(Monsters.Count);
        foreach (var m in Monsters)
        {
            monsters.Add(new MonsterObservation
            {
                Slot = m.Slot,
                CatalogId = m.CatalogId,
                Hp = m.Hp,
                MaxHp = m.MaxHp,
                Block = m.Block,
                Alive = m.Alive,
                IntentId = (int)m.Intent,
                IntentKnown = m.IntentKnown,
                MoveBaseDamage = m.MoveBaseDamage,
                MoveHits = m.MoveHits,
                Powers = [.. m.Powers],
            });
        }

        var hand = new List<CardObservation>(Hand.Count);
        foreach (var c in Hand)
        {
            hand.Add(new CardObservation
            {
                CatalogId = c.CatalogId,
                Uuid = c.Uuid,
                Upgrade = c.Upgrade,
                CurrentCost = c.CurrentCost,
                Flags = (byte)c.Flags,
            });
        }

        var bag = new SortedDictionary<int, int>();
        foreach (var c in DrawPile)
        {
            bag[c.CatalogId] = bag.GetValueOrDefault(c.CatalogId) + 1;
        }

        var bagList = new List<BagCount>(bag.Count);
        foreach (var (id, count) in bag)
        {
            bagList.Add(new BagCount(id, count));
        }

        return new CombatObservation
        {
            SchemaVersion = _config.SchemaVersion,
            CatalogVersion = _config.CatalogVersion,
            Mode = _config.Mode,
            Screen = _terminal
                ? (_terminalStatus == StepStatus.TerminalWin ? ScreenType.Victory : ScreenType.GameOver)
                : ScreenType.Combat,
            Turn = _turn,
            Phase = _phase,
            Player = player,
            Monsters = monsters,
            Hand = hand,
            DrawCount = DrawPile.Count,
            DiscardCount = DiscardPile.Count,
            ExhaustCount = ExhaustPile.Count,
            LimboCount = Limbo.Count,
            DrawBagCounts = bagList,
            ShufflePending = ShufflePending,
        };
    }

    // ---- checkpoint / clone (SL) ----

    public SimCheckpoint Checkpoint()
    {
        var ck = SimCheckpoint.Capture(_rng, _config.CatalogVersion, _config.GameVersionString);
        ck.CharacterId = _config.CharacterId;
        ck.Ascension = _config.Ascension;
        ck.Mode = _config.Mode;
        ck.Turn = _turn;
        ck.EncounterId = _start.EncounterId;
        ck.Floor = _start.Floor;
        ck.Combat = SnapshotState();
        return ck;
    }

    public void Restore(in SimCheckpoint ckpt)
    {
        if (ckpt.Combat is null)
        {
            throw new InvalidOperationException("checkpoint carries no combat state");
        }

        ckpt.RestoreInto(_rng);
        ApplySnapshot(ckpt.Combat);
        _terminal = false;
        _terminalStatus = StepStatus.Ok;
    }

    public ISimWorld Clone()
    {
        if (_config.Mode != InfoMode.Sl)
        {
            throw new InvalidOperationException(
                "Clone() is SL-only (KD-16). NoSL search must go through CloneForSearch (PR-008).");
        }

        var copy = new CombatWorld(_config, _start, 0);
        copy._rng.ImportState(_rng.ExportState());
        copy.ApplySnapshot(SnapshotState());
        return copy;
    }

    private CombatSnapshot SnapshotState()
    {
        var snap = new CombatSnapshot
        {
            Turn = _turn,
            Phase = (byte)_phase,
            PlayerHp = _playerHp,
            PlayerMaxHp = _playerMaxHp,
            PlayerBlock = _playerBlock,
            PlayerEnergy = _energy,
            PlayerMaxEnergy = _maxEnergy,
            TimesDamagedThisCombat = _timesDamagedThisCombat,
            CardsPlayedThisTurn = _cardsPlayedThisTurn,
            ZeroCostPlaysThisTurn = _zeroCostPlaysThisTurn,
            PlayerPowers = SnapshotPowers(PlayerPowers),
            Hand = SnapshotCards(Hand),
            Draw = SnapshotCards(DrawPile),
            Discard = SnapshotCards(DiscardPile),
            Exhaust = SnapshotCards(ExhaustPile),
            Limbo = SnapshotCards(Limbo),
            Monsters = [],
        };

        foreach (var m in Monsters)
        {
            snap.Monsters.Add(new MonsterSnapshot
            {
                Slot = m.Slot,
                CatalogId = m.CatalogId,
                Hp = m.Hp,
                MaxHp = m.MaxHp,
                Block = m.Block,
                Alive = m.Alive,
                HalfDead = m.HalfDead,
                Intent = (byte)m.Intent,
                IntentKnown = m.IntentKnown,
                MoveId = m.MoveId,
                MoveBaseDamage = m.MoveBaseDamage,
                MoveHits = m.MoveHits,
                Misc = m.Misc,
                Powers = SnapshotPowers(m.Powers),
            });
        }

        return snap;
    }

    private static List<CardSnapshot> SnapshotCards(List<CardInstance> zone)
    {
        var list = new List<CardSnapshot>(zone.Count);
        foreach (var c in zone)
        {
            list.Add(new CardSnapshot
            {
                CatalogId = c.CatalogId,
                Uuid = c.Uuid,
                Upgrade = c.Upgrade,
                Flags = (byte)c.Flags,
                CurrentCost = c.CurrentCost,
                Misc = c.Misc,
            });
        }

        return list;
    }

    private static List<PowerSnapshot> SnapshotPowers(List<PowerStack> powers)
    {
        var list = new List<PowerSnapshot>(powers.Count);
        foreach (var p in powers)
        {
            list.Add(new PowerSnapshot { CatalogId = p.CatalogId, Amount = p.Amount, JustApplied = p.JustApplied });
        }

        return list;
    }

    private void ApplySnapshot(CombatSnapshot s)
    {
        _turn = s.Turn;
        _phase = (CombatPhase)s.Phase;
        _playerHp = s.PlayerHp;
        _playerMaxHp = s.PlayerMaxHp;
        _playerBlock = s.PlayerBlock;
        _energy = s.PlayerEnergy;
        _maxEnergy = s.PlayerMaxEnergy;
        _timesDamagedThisCombat = s.TimesDamagedThisCombat;
        _cardsPlayedThisTurn = s.CardsPlayedThisTurn;
        _zeroCostPlaysThisTurn = s.ZeroCostPlaysThisTurn;

        RestoreCards(Hand, s.Hand);
        RestoreCards(DrawPile, s.Draw);
        RestoreCards(DiscardPile, s.Discard);
        RestoreCards(ExhaustPile, s.Exhaust);
        RestoreCards(Limbo, s.Limbo);

        PlayerPowers.Clear();
        foreach (var p in s.PlayerPowers)
        {
            PlayerPowers.Add(new PowerStack(p.CatalogId, p.Amount, p.JustApplied));
        }

        if (s.Monsters.Count != Monsters.Count)
        {
            throw new InvalidOperationException(
                $"monster count mismatch: checkpoint {s.Monsters.Count} vs world {Monsters.Count}");
        }

        for (int i = 0; i < s.Monsters.Count; i++)
        {
            var ms = s.Monsters[i];
            var m = Monsters[i];
            m.Slot = ms.Slot;
            m.CatalogId = ms.CatalogId;
            m.Hp = ms.Hp;
            m.MaxHp = ms.MaxHp;
            m.Block = ms.Block;
            m.Alive = ms.Alive;
            m.HalfDead = ms.HalfDead;
            m.Intent = (IntentId)ms.Intent;
            m.IntentKnown = ms.IntentKnown;
            m.MoveId = ms.MoveId;
            m.MoveBaseDamage = ms.MoveBaseDamage;
            m.MoveHits = ms.MoveHits;
            m.Misc = ms.Misc;
            m.Powers.Clear();
            foreach (var p in ms.Powers)
            {
                m.Powers.Add(new PowerStack(p.CatalogId, p.Amount, p.JustApplied));
            }

            Monsters[i] = m;
        }
    }

    private static void RestoreCards(List<CardInstance> zone, List<CardSnapshot> snaps)
    {
        zone.Clear();
        foreach (var cs in snaps)
        {
            zone.Add(new CardInstance
            {
                CatalogId = cs.CatalogId,
                Uuid = cs.Uuid,
                Upgrade = cs.Upgrade,
                Flags = (CardFlags)cs.Flags,
                CurrentCost = cs.CurrentCost,
                Misc = cs.Misc,
            });
        }
    }

    // ---- hashes ----

    public ulong Hash64()
    {
        ulong h = 14695981039346656037UL;
        h = Mix(h, _config.SchemaVersion);
        h = MixStr(h, _config.CatalogVersion);
        h = Mix(h, _turn);
        h = Mix(h, (int)_phase);
        h = Mix(h, _playerHp);
        h = Mix(h, _playerMaxHp);
        h = Mix(h, _playerBlock);
        h = Mix(h, _energy);
        h = Mix(h, _maxEnergy);
        h = Mix(h, _timesDamagedThisCombat);
        h = MixZone(h, Hand, ordered: true);
        h = MixZone(h, DrawPile, ordered: true);
        h = MixZone(h, DiscardPile, ordered: true);
        h = MixZone(h, ExhaustPile, ordered: true);
        h = MixZone(h, Limbo, ordered: true);
        foreach (var p in PlayerPowers)
        {
            h = Mix(h, p.CatalogId);
            h = Mix(h, p.Amount);
            h = Mix(h, p.JustApplied ? 1 : 0);
        }

        for (int i = 0; i < Monsters.Count; i++)
        {
            var m = Monsters[i];
            h = Mix(h, m.Slot);
            h = Mix(h, m.CatalogId);
            h = Mix(h, m.Hp);
            h = Mix(h, m.MaxHp);
            h = Mix(h, m.Block);
            h = Mix(h, m.Alive ? 1 : 0);
            h = Mix(h, m.MoveId);
            h = Mix(h, (int)m.Intent);
            h = Mix(h, m.Misc);
            foreach (var p in m.Powers)
            {
                h = Mix(h, p.CatalogId);
                h = Mix(h, p.Amount);
                h = Mix(h, p.JustApplied ? 1 : 0);
            }
        }

        byte[] rngState = _rng.ExportState();
        h = Mix(h, rngState.Length);
        foreach (byte b in rngState)
        {
            h = Mix(h, b);
        }

        return h;
    }

    /// <summary>Public-information hash (§4.10.2): no draw order, no rng counters, no seed.</summary>
    public ulong InfoSetHash64()
    {
        ulong h = 14695981039346656037UL;
        h = Mix(h, _config.SchemaVersion);
        h = MixStr(h, _config.CatalogVersion);
        h = Mix(h, _turn);
        h = Mix(h, (int)_phase);
        h = Mix(h, _playerHp);
        h = Mix(h, _playerMaxHp);
        h = Mix(h, _playerBlock);
        h = Mix(h, _energy);
        h = Mix(h, _maxEnergy);
        h = MixZone(h, Hand, ordered: false);
        h = MixZone(h, DiscardPile, ordered: false);
        h = MixZone(h, ExhaustPile, ordered: false);
        h = MixZone(h, Limbo, ordered: false);

        // Draw pile only as bag counts + revealed uuids (§4.10.2).
        var bag = new SortedDictionary<int, int>();
        List<int> knownUuids = [];
        foreach (var c in DrawPile)
        {
            bag[c.CatalogId] = bag.GetValueOrDefault(c.CatalogId) + 1;
            if ((c.Flags & CardFlags.Innate) != 0)
            {
                knownUuids.Add(c.Uuid);
            }
        }

        foreach (var (id, count) in bag)
        {
            h = Mix(h, id);
            h = Mix(h, count);
        }

        knownUuids.Sort();
        foreach (int u in knownUuids)
        {
            h = Mix(h, u);
        }

        foreach (var p in PlayerPowers)
        {
            h = Mix(h, p.CatalogId);
            h = Mix(h, p.Amount);
        }

        for (int i = 0; i < Monsters.Count; i++)
        {
            var m = Monsters[i];
            h = Mix(h, m.Slot);
            h = Mix(h, m.CatalogId);
            h = Mix(h, m.Hp);
            h = Mix(h, m.MaxHp);
            h = Mix(h, m.Block);
            h = Mix(h, m.Alive ? 1 : 0);
            h = Mix(h, m.IntentKnown ? (int)m.Intent : -1);
            foreach (var p in m.Powers)
            {
                h = Mix(h, p.CatalogId);
                h = Mix(h, p.Amount);
            }
        }

        return h;
    }

    private static ulong MixZone(ulong h, List<CardInstance> zone, bool ordered)
    {
        if (!ordered)
        {
            var sorted = new List<CardInstance>(zone);
            sorted.Sort((a, b) => a.Uuid.CompareTo(b.Uuid));
            zone = sorted;
        }

        foreach (var c in zone)
        {
            h = Mix(h, c.CatalogId);
            h = Mix(h, c.Uuid);
            h = Mix(h, c.Upgrade);
            h = Mix(h, c.CurrentCost);
            h = Mix(h, (int)c.Flags);
        }

        return h;
    }

    private static ulong Mix(ulong h, long v)
    {
        h ^= (ulong)v;
        h *= 1099511628211UL;
        return h;
    }

    private static ulong MixStr(ulong h, string s)
    {
        foreach (char c in s)
        {
            h ^= c;
            h *= 1099511628211UL;
        }

        return h;
    }

    // ---- internals ----

    private void SetTerminal(StepStatus status)
    {
        _terminal = true;
        _terminalStatus = status;
        _phase = CombatPhase.Exec;
        foreach (int relicId in _start.RelicIds)
        {
            if (RelicRegistry.TryGet(relicId, out var hooks))
            {
                hooks.OnCombatEnd(this);
            }
            else
            {
                HookLog.NotImplemented(_config, $"relic:{relicId}:OnCombatEnd");
            }
        }

        Log($"combat_end: {status}");
    }

    private bool AllMonstersDead()
    {
        foreach (var m in Monsters)
        {
            if (m.Alive)
            {
                return false;
            }
        }

        return Monsters.Count > 0;
    }

    private bool HasInnate()
    {
        foreach (var c in DrawPile)
        {
            if (c.HasFlag(CardFlags.Innate))
            {
                return true;
            }
        }

        return false;
    }

    private static int IndexOfUuid(List<CardInstance> zone, int uuid)
    {
        for (int i = 0; i < zone.Count; i++)
        {
            if (zone[i].Uuid == uuid)
            {
                return i;
            }
        }

        return -1;
    }

    private void Log(string message) => _config.HookLog?.Invoke(message);

    private static StepResult Illegal() => new(StepStatus.Illegal, 0f, false);
}
