namespace Echolos.Domain.Models
{
    /// <summary>ユニットの状態（ランを通じた永続状態）</summary>
    public enum UnitState
    {
        Active,  // 出撃中（前衛または後衛スロットに配置済み）
        Reserve, // 控え（ベンチ）
        Dead     // 死亡（完全ロスト）
    }

    /// <summary>バトルのフェーズ状態</summary>
    public enum PhaseState
    {
        Start,               // ターン開始フェーズ（開幕スキル等）
        Main,                // メインフェーズ（各ユニットのSPD順行動）
        InterventionStandby, // プレイヤー介入待機（2〜3秒ウィンドウ）
        End                  // ターン終了フェーズ（状態異常処理・シールド消滅等）
    }

    /// <summary>
    /// 属性。シナジー軸（火・水・補助）と地形による DEF 補正にのみ影響する。
    /// 状態異常の属性連動解除・特効相性は持たない。
    /// </summary>
    public enum Element
    {
        None,      // 無属性（補助属性等）
        Fire,      // 火
        Water,     // 水
        Ice,       // 氷
        Lightning, // 雷
        Wind,      // 風
        Earth,     // 地
        Light,     // 光
        Dark       // 闇
    }

    /// <summary>戦闘評価結果（4段階：完勝／辛勝／惜敗／完敗）</summary>
    public enum BattleResult
    {
        None,                // 未決定（戦闘継続中）
        PerfectVictory,      // 完勝：ターン制限内に敵を全滅
        AdvantageousVictory, // 辛勝：ターン制限到達時、損耗差が自軍有利
        MarginalDefeat,      // 惜敗：ターン制限到達時、損耗差が敵軍有利(同数含む)
        CrushingDefeat       // 完敗：味方を全滅（または撤退選択）
    }

    /// <summary>
    /// 攻撃種別。反撃可否と配置 ATK 補正カーブを決める軸。
    /// ターゲット選定方向は <see cref="TargetingDirection"/> で独立に決める。
    /// </summary>
    public enum AttackKind
    {
        Melee,  // 近接：反撃を受ける／反撃の発動者になれる。配置 ATK 補正は内部スロット 0〜5 のカーブ
        Ranged, // 遠隔：反撃を受けない／反撃発動なし。配置 ATK 補正は最後尾からの距離のカーブ
        None    // 攻撃しない：補助・回復専門ユニット用。攻撃 Waza を発動しないので反撃の授受・配置 ATK 補正の対象外
    }

    /// <summary>
    /// 通常攻撃のターゲット選定方向。AttackKind とは独立した軸。
    /// 4 通り（Melee/Ranged × FromFront/FromBack）でユニットの戦術的位置付けが決まる。
    /// </summary>
    public enum TargetingDirection
    {
        FromFront, // 前から狙う：最前の敵を狙う（通常の前衛アタッカー・前列焼き）
        FromBack   // 後ろから狙う：最後尾の敵を狙う（弓兵・暗殺者）
    }

    /// <summary>技のターゲット指定タイプ</summary>
    public enum TargetingType
    {
        SingleEnemy,         // 敵単体（TargetingDirection に従って 1 体）
        SingleAlly,          // 味方単体
        AllEnemies,          // 敵全体
        AllAllies,           // 味方全体
        Self,                // 自身のみ
        DirectionalEnemies   // 敵の前/後から TargetCount 体（範囲攻撃・TargetingDirection と Waza.TargetCount を参照）
    }

    /// <summary>
    /// 単体ターゲットの選定戦略。SingleEnemy / SingleAlly で TargetingType と組み合わせて使う。
    /// Default は TargetingType ごとの既定挙動（SingleEnemy=TargetingDirection／SingleAlly=最低 HP 割合）。
    /// それ以外は全生存対象から戦略に従って 1 体選ぶ（TargetingDirection は無視）。
    /// </summary>
    public enum TargetSelection
    {
        Default,         // 既定（SingleEnemy=TargetingDirection／SingleAlly=最低 HP 割合）
        LowestHpRatio,   // 最低 HP 割合（回復・DEF バフ等）
        HighestAtk,      // 最高 ATK（ATK バフ＝主力味方／ATK デバフ＝最大脅威の敵）
        HighestDef       // 最高 DEF（DEF デバフ＝硬い敵）
    }

    /// <summary>パッシブスキルの発動タイミング</summary>
    public enum TriggerType
    {
        Always,           // 常時発動（ステータス補正等）
        OnActionStart,    // 自身の行動開始時
        OnActionEnd,      // 自身の行動終了時
        OnDamageReceived, // 被ダメージ時（1ヒット毎）
        OnPerHit,         // 攻撃側が攻撃を1ヒット与えた毎（トゲ反射等）
        OnDeath,          // 自身が死亡した時（自爆等）
        OnTurnStart,      // ターン開始フェーズ時
        OnTurnEnd         // ターン終了フェーズ時
    }

    /// <summary>技の行動カテゴリ。ActionExecutorの処理分岐とAIの行動選択に使用する。</summary>
    public enum WazaCategory
    {
        Attack,  // 攻撃：ダメージを与える（既定）
        Heal,    // 回復：味方のHPを回復する（ダメージ・防御計算なし）
        Buff,    // 強化：対象（味方）にバフを付与する（ダメージなし）
        Debuff,  // 弱体：対象（敵）にデバフを付与する（ダメージなし）
        Counter  // 反撃：近接攻撃を受けた際に同時解決で発動する（通常選択肢には現れない）
    }

    /// <summary>
    /// 推奨配置（プレイヤー向け UI ヒント）。
    /// システム制約ではない（システム的に後衛にしか配置できないユニットは存在しない）。
    /// </summary>
    public enum PlacementHint
    {
        Any,    // 任意（特に推奨なし・固有キャラ等）
        Front,  // 前衛推奨
        Back    // 後衛推奨
    }

    /// <summary>
    /// 戦術的役割（ユニットの主たる働き）。
    /// List で複数指定可能（例：騎士は Tank+Attacker）。
    /// </summary>
    public enum UnitRole
    {
        Tank,      // タンク（攻撃を引き受ける・耐久重視）
        Attacker,  // アタッカー（火力役）
        Support,   // 補助（バフ／デバフ／状態異常）
        Healer     // 回復役
    }
}
