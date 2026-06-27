using System;
using System.Collections.Generic;
using System.Linq;
using Echolos.Domain.Battle.Aura;
using Echolos.Domain.Battle.Synergy;
using Echolos.Domain.Battle.Terrain;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle.Replay
{
    // 戦闘サブシステムの生成と結線をカプセル化する。
    // BattleRunner.Run が薄いまま保たれるよう、オブジェクト生成と結線責務をここに集約。
    //
    // 利用順序：
    //   1. new BattleAssembly(allies, enemies, maxTurns, terrain, strength, isAttacking, rng)
    //   2. Recorder の Attach*／Subscribe*（ログヘッダを実行より先に並べるため）
    //   3. WireBattleLogic()
    //   4. Manager.InitializeBattle(Context) → ProcessTurn ループ
    //
    // WireBattleLogic で結線する経路：
    //   Manager.OnBattleStart   → SynergyApplier.ApplyAll → AuraApplier.ApplyAll
    //   Manager.OnActionStart   → StatusProcessor.HandleActionStart
    //   Manager.OnActionSkipped → StatusProcessor.HandleActionSkipped
    //   Manager.OnEndPhase      → StatusProcessor.HandleEndPhase
    //                          → AuraTracker.FlushPendingDeaths（Burn/呪い即死経路のオーラ剥奪を Died Event 後に）
    //   Executor.OnUnitDied     → StatusProcessor.HandleUnitDied
    //                          → AuraTracker.HandleUnitDied（pending キュー追加のみ）
    //   Executor.OnActionResolved → AuraTracker.FlushPendingDeaths（攻撃由来の死亡を ActionResolved Event 後に剥奪）
    //   StatusProcessor.OnStatusEffectKill → AuraTracker.HandleUnitDied（Burn/呪い即死もキュー追加）
    public sealed class BattleAssembly
    {
        public BattleContext Context { get; }
        public BattleManager Manager { get; }
        public ActionExecutor Executor { get; }
        public StatusEffectProcessor StatusProcessor { get; }
        public AuraTracker AuraTracker { get; }

        private readonly IReadOnlyList<SynergyDefinition> _synergyDefinitions;
        private readonly IReadOnlyList<AuraDefinition> _auraDefinitions;

        public BattleAssembly(
            IEnumerable<RuntimeUnit> allies,
            IEnumerable<RuntimeUnit> enemies,
            int maxTurns,
            TerrainKind terrain = TerrainKind.Neutral,
            TerrainStrength terrainStrength = TerrainStrength.Light,
            bool isAttackingSide = false,
            Func<int> random0to99 = null,
            IReadOnlyList<SynergyDefinition> synergyDefinitions = null,
            IReadOnlyList<AuraDefinition> auraDefinitions = null)
        {
            if (allies == null) throw new ArgumentNullException(nameof(allies));
            if (enemies == null) throw new ArgumentNullException(nameof(enemies));

            Context = new BattleContext(maxTurns)
            {
                Terrain = terrain,
                TerrainStrength = terrainStrength,
                IsAttackingSide = isAttackingSide,
            };
            Context.AllyUnits.AddRange(allies);
            Context.EnemyUnits.AddRange(enemies);

            Executor = new ActionExecutor(random0to99);
            StatusProcessor = new StatusEffectProcessor();
            Manager = new BattleManager(Executor);

            _synergyDefinitions = synergyDefinitions ?? SynergyDefinitions.All;
            _auraDefinitions = auraDefinitions ?? AuraDefinitions.All;
            AuraTracker = new AuraTracker(Context, _auraDefinitions);
        }

        public void WireBattleLogic()
        {
            Manager.OnBattleStart += ctx => SynergyApplier.ApplyAll(ctx, _synergyDefinitions);
            Manager.OnBattleStart += ctx => AuraApplier.ApplyAll(ctx, _auraDefinitions);
            Manager.OnActionStart += StatusProcessor.HandleActionStart;
            Manager.OnActionSkipped += StatusProcessor.HandleActionSkipped;
            Manager.OnEndPhase += StatusProcessor.HandleEndPhase;
            // OnEndPhase 内で StatusProcessor が Burn 即死等を処理し OnStatusEffectKill 経由で
            // Died Event を積む。その後（C# event は購読順）AuraTracker.FlushPendingDeaths が走る
            // ことで Aura 解除 Event は構造的に Died Event の後に積まれる。
            Manager.OnEndPhase += AuraTracker.FlushPendingDeaths;

            Executor.OnUnitDied += StatusProcessor.HandleUnitDied;
            Executor.OnUnitDied += AuraTracker.HandleUnitDied;
            // ActionResolved Event が積まれた後に flush ＝攻撃由来死亡の Aura 解除 Event は
            // ActionResolved Event の後に積まれる。
            Executor.OnActionResolved += (ctx, _decl, _outcomes) => AuraTracker.FlushPendingDeaths(ctx);

            // Burn / 呪い即死経路：StatusProcessor.OnStatusEffectKill でキュー追加。
            // 実際の flush は OnEndPhase 終端（上記）で起きる。
            StatusProcessor.OnStatusEffectKill += AuraTracker.HandleUnitDied;
        }

        public IReadOnlyList<RuntimeUnit> Allies => Context.AllyUnits;
        public IReadOnlyList<RuntimeUnit> Enemies => Context.EnemyUnits;
        public IEnumerable<RuntimeUnit> AllUnits => Allies.Concat(Enemies);
    }
}
