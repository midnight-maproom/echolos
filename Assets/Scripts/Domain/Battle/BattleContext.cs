using System;
using System.Collections.Generic;
using Echolos.Domain.Battle;
using Echolos.Domain.Battle.Terrain;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle
{
    /// <summary>
    /// バトル中の全状態を管理するコンテキスト。
    /// バトルロジック（ターゲット評価・ダメージ計算・フェーズ進行）は
    /// すべてこのクラスを通じて状態を参照・変更する。
    /// MonoBehaviourを継承しない純粋なPOCO。
    /// </summary>
    public class BattleContext
    {
        // ユニット管理

        /// <summary>味方のRuntimeUnitリスト（SlotIndex順。最大6体）</summary>
        public List<RuntimeUnit> AllyUnits { get; set; } = new List<RuntimeUnit>();

        /// <summary>敵のRuntimeUnitリスト（SlotIndex順。最大6体）</summary>
        public List<RuntimeUnit> EnemyUnits { get; set; } = new List<RuntimeUnit>();

        // ターン管理

        /// <summary>現在のターン数（1から始まる）</summary>
        public int CurrentTurn { get; set; } = 1;

        /// <summary>ターン制限。この値に達した時点で優勢勝利/敗北の判定を行う</summary>
        public int MaxTurnLimit { get; set; }

        // フェーズ管理

        /// <summary>現在のバトルフェーズ</summary>
        public PhaseState CurrentPhase { get; set; } = PhaseState.Start;

        // 地形（環境項）

        /// <summary>戦闘マスの地形種別。DamageFormula の環境項算出に使用する（仕様 320 §5）</summary>
        public TerrainKind Terrain { get; set; } = TerrainKind.Neutral;

        /// <summary>地形強度（自領 Light / 敵領 Medium / 敵拠点 Heavy）。α 値の決定に使用する</summary>
        public TerrainStrength TerrainStrength { get; set; } = TerrainStrength.Light;

        // 攻め守り

        /// <summary>
        /// この戦闘で味方陣営が攻め側か。
        /// VSPrototypeRoundManager が MapNode 種別から判定してセットする。
        /// 引き分け非対称評価（IsAdvantageousVictoryCondition）と SPD タイブレーク
        /// の陣営間優先で共用される。既定 false（守り）。
        /// </summary>
        public bool IsAttackingSide { get; set; } = false;

        // 戦闘結果

        /// <summary>戦闘評価結果。None = 未決定（戦闘継続中）</summary>
        public BattleResult Result { get; set; } = BattleResult.None;

        // 割り込み行動キュー（ReactionStack）

        /// <summary>
        /// 反撃・自爆などの割り込み行動を積むキュー。
        /// メインフェーズの1ユニットの行動（ヒットループ含む）が完全に終了した直後に
        /// 順次処理される。処理中に新たなリアクションが発生した場合も積み続ける。
        /// </summary>
        public Queue<Action> ReactionStack { get; set; } = new Queue<Action>();

        // 統計（勝敗判定用）

        /// <summary>戦闘開始時の味方ユニット数（InitializeBattleでセット）</summary>
        public int InitialAllyCount { get; set; }

        /// <summary>戦闘開始時の敵ユニット数（InitializeBattleでセット）</summary>
        public int InitialEnemyCount { get; set; }

        /// <summary>
        /// この戦闘で味方が撃破した敵ユニット数。
        /// 「戦闘開始時の敵数 - 現在の生存敵数」で算出し、マイナスにはならない。
        /// </summary>
        public int AllyKillCount => Math.Max(0, InitialEnemyCount - GetAliveEnemies().Count);

        /// <summary>
        /// この戦闘で撃破された味方ユニット数。
        /// 「戦闘開始時の味方数 - 現在の生存味方数」で算出し、マイナスにはならない。
        /// </summary>
        public int AllyDeathCount => Math.Max(0, InitialAllyCount - GetAliveAllies().Count);

        public BattleContext(int maxTurnLimit = 10)
        {
            MaxTurnLimit = maxTurnLimit;
        }

        // ヘルパーメソッド

        /// <summary>生存している味方ユニットの一覧を返す</summary>
        public List<RuntimeUnit> GetAliveAllies()
        {
            var result = new List<RuntimeUnit>();
            foreach (var unit in AllyUnits)
                if (unit.IsAlive) result.Add(unit);
            return result;
        }

        /// <summary>生存している敵ユニットの一覧を返す</summary>
        public List<RuntimeUnit> GetAliveEnemies()
        {
            var result = new List<RuntimeUnit>();
            foreach (var unit in EnemyUnits)
                if (unit.IsAlive) result.Add(unit);
            return result;
        }

        /// <summary>味方が全滅しているかどうか</summary>
        public bool IsAllyWiped => GetAliveAllies().Count == 0;

        /// <summary>敵が全滅しているかどうか</summary>
        public bool IsEnemyWiped => GetAliveEnemies().Count == 0;

        /// <summary>
        /// ターン制限到達時の優勢勝利条件を満たしているか。
        /// 攻め側：1 体以上撃破していれば勝利／0 撃破は時間切れ負け。
        /// 守り側：1 体も被撃破されていなければ勝利。
        /// </summary>
        public bool IsAdvantageousVictoryCondition =>
            VictoryEvaluator.IsAdvantageousVictory(AllyKillCount, AllyDeathCount, IsAttackingSide);
    }
}
