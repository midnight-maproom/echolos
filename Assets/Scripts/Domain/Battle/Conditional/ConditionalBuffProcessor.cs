using System.Collections.Generic;
using Echolos.Domain.Models;

namespace Echolos.Domain.Battle.Conditional
{
    /// <summary>
    /// Conditional バフ（条件型バフ）の抽象基底。
    /// 派生クラスは購読フックを <see cref="Hooks"/> で宣言し、<see cref="Refresh"/> で再評価ロジックを実装する。
    /// 死亡ユニット情報が必要な場合は <see cref="OnUnitDied"/> を override する（既定は Refresh を呼ぶ）。
    ///
    /// BattleManager 側は登録された全 Processor のうち、対応する Hook を持つものだけを発火時に
    /// <see cref="DispatchRefresh"/> 経由で起動する。<see cref="DispatchRefresh"/> は再帰ガード
    /// （自身の AddEffect/RemoveEffect が BuffApplied/BuffRemoved を介して同 Processor を
    /// 再帰呼び出しするのを構造的に防ぐ）を備えるため、派生クラスは冪等性（差分判定）だけ
    /// 気にすれば良い。
    ///
    /// 新規 Conditional バフ追加手順：
    /// 1. 本基底を継承した派生クラスを Domain/Battle/Conditional/ に作成
    /// 2. Hooks プロパティで購読フックを宣言
    /// 3. Refresh / OnUnitDied を実装
    /// 4. Bootstrap で BattleManager コンストラクタに渡すリストに追加
    /// </summary>
    public abstract class ConditionalBuffProcessor
    {
        private bool _isRefreshing;

        /// <summary>このプロセッサが購読するフック。BattleManager が dispatch 時に参照する。</summary>
        public abstract IReadOnlyList<ConditionalBuffHook> Hooks { get; }

        /// <summary>
        /// BattleManager から呼ばれる再評価エントリポイント。
        /// 自身の AddEffect/RemoveEffect が BuffApplied/BuffRemoved 経由で同 Processor を
        /// 再帰呼び出しするのを構造的に防ぐ。<see cref="Refresh"/> 実行中は再入を弾く。
        /// </summary>
        public void DispatchRefresh(BattleContext context)
        {
            if (_isRefreshing) return;
            _isRefreshing = true;
            try { Refresh(context); }
            finally { _isRefreshing = false; }
        }

        /// <summary>
        /// 共通の再評価（BattleStart / TurnStart / BuffApplied / BuffRemoved で呼ばれる）。
        /// 派生クラスで実装する。直接呼ばずに <see cref="DispatchRefresh"/> 経由で起動するため、
        /// 派生クラスは「自 AuraSourceId 由来の集計除外＋差分判定（冪等）」だけ満たせば良い。
        /// </summary>
        public abstract void Refresh(BattleContext context);

        /// <summary>
        /// UnitDied フック専用（死亡ユニット情報が必要なケース）。
        /// 既定は <see cref="DispatchRefresh"/> を呼ぶ（再帰ガード経由・陣営非依存の Processor 向け）。
        /// 死亡陣営だけ再評価したい等の最適化が必要な場合は override する。
        /// override する場合は自身の Refresh も DispatchRefresh 経由で呼ぶか、
        /// 自身が引き起こす BuffRemoved の再帰に注意すること。
        /// </summary>
        public virtual void OnUnitDied(BattleContext context, RuntimeUnit deadUnit)
            => DispatchRefresh(context);
    }
}
