# Echolos アーキテクチャ設計書

**本書のスコープ**：VSプロト範囲（[310](310_vsprototype_spec.md) ラン進行 / [320](320_vsprototype_combat_spec.md) 戦闘 / [330](330_vsprototype_storyplot.md) ストーリー）の実装設計。フル版（コンテスト後の本実装）の設計はプロト完了後に新エンジン基盤で書き起こす想定で、本書には含めない。

**読み方**：
- §1〜§2 で基本方針と asmdef 構成を理解する
- §3 で 310/320/330 の仕様要素と本書で定義する実装観点の対応関係を表で押さえる
- §4〜§7 で各層の責務を確認する

---

## 1. 基本方針（Architecture Rules）

### 1.1 4 層アーキテクチャ

責務で 4 層に分離する。依存方向は一方向で循環なし。

```
Presentation ──→ UseCase ──→ Domain ←── Data
      │                        ↑          │
      └────────────────────────┴──────────┘
   （Presentation は DI 配線のため全層参照可）
```

| 層 | 役割 | Unity 依存 | 例 |
|---|---|---|---|
| **Domain** | ドメインロジック・抽象定義 | × | `Unit` / `RuntimeUnit` / `BattleManager` / `Waza` / `IUnitCatalog` / `ISaveStore` 等の**抽象** |
| **Data** | 永続データ・データロード機構・抽象の実装 | ○ | `UnitDefinitionSO` / `UnitCatalog : IUnitCatalog`（Resources.LoadAll 実装） / `JsonSaveStore : ISaveStore` |
| **UseCase** | ゲーム進行ロジック・シナリオ層 | × | `RoundManager` / `InteriorService` / `EncounterScheduler` / `RewardEngine` |
| **Presentation** | UI・Composition Root | ○ | `Bootstrap`（DI 配線）/ 各種 `*GUI` / 演出層 |

**核**：「Domain が抽象を定義し、Data が実装する。UseCase は抽象に依存し、Presentation が DI で配線する。」これにより Domain と UseCase は Unity 非依存を保ち、Editor テストで完結する。

### 1.2 依存方向の規律

- **Domain は何にも依存しない**（asmdef `references: []`・`noEngineReferences: true`）
- **Data は Domain にのみ依存**（`references: [Domain]`・`noEngineReferences: false`・Unity 依存可）
- **UseCase は Domain にのみ依存**（`references: [Domain]`・`noEngineReferences: true`）
- **Presentation は Domain / Data / UseCase 全てに依存可**（`noEngineReferences: false`・Unity 依存可）

「UseCase は Data に依存しない」が肝。UseCase は Domain の抽象（`IUnitCatalog` 等）を通してデータを取得する。実装の DI 配線は Presentation の責務。

### 1.3 状態変化通知 API — public フィールド直接公開を禁止

ロジック層が状態を変える際は `public` フィールド直接公開を禁止する。

- フィールドは `private`
- 読み取りは `IReadOnlyList<T>` 等の immutable view
- 変更は `AddX` / `RemoveX` 等のメソッド経由のみ
- メソッド内で `OnXAdded` / `OnXRemoved` 等のイベント発火

本ルールは UI 連動忘れを**コンパイラ強制レベルで防ぐ**柱。

### 1.4 データ駆動原則 — 触りたいデータはコードに書かない

**コード変更なしで触れる必要があるデータ**はコードに直書きせず、SO + POCO の二段構成で外部データとして持つ。コードは「構造」と「振る舞いの実装」のみを担う。

判定基準：以下のいずれかに該当するなら SO 化対象。

- バランス調整で値を動かす（ステータス・技構成・報酬式パラメタ・施設コスト等）
- シナリオライターやデザイナーが文言を触る（ナレ文・UI 文言・スチルパス）
- 多言語化で文字列を差し替える（全テキスト）
- プール構成やルール表でゲーム性を切り替える（ドラフトプール・施設アンロック条件）

| 区分 | 例 | 扱い |
|---|---|---|
| 定義データ | ユニット・技・状態異常のステータス／構成パラメタ | SO 化 |
| 演出データ | ストーリーペッジ（ナレ文・画像パス・フェード秒数） | SO 化 |
| ゲーム調整データ | ドラフトプール構成・報酬計算式＋パラメタ・メタ強化定義・施設アンロック条件 | SO 化 |
| ロジック実装 | ダメージ計算式・特殊発動条件・戦闘進行 | コードに残す |
| 識別子定数 | `MetaUpgradeIds` / `MetaUnitIds` 等の SO 主キー | コードに残す（const）|

**接合方式**：データ側は「式 ID + パラメタ」を持ち、コード側のレジストリで実装を引く（`DamageFormulaRegistry` パターン）。

詳細と将来 SO 化対象は §5（特に §5.6）を参照。

### 1.5 SO 内ネスト POCO は必ず public field 形式

ScriptableObject 内にネストするデータクラス（`EffectDefinition` / `UnitDefinition` / `WazaDefinition` / `FormulaParam` 等）のフィールドは **auto-property（`{ get; set; }`）を使わない**。Unity のシリアライザは auto-property を認識せず SO に保存できない（`[System.Serializable]` を付けても無効）。

### 1.6 Composition Root は Presentation の Bootstrap が唯一の DI 配線地点

依存の組み立て（`new UnitCatalog()` → UseCase に注入）は Presentation の Bootstrap MonoBehaviour でのみ行う。Domain / Data / UseCase の中で `new` するインスタンスは扱うデータ自身（`Unit` インスタンス・状態オブジェクト等）に限定し、サービス（Catalog・Service・Engine・Scheduler 等）は**必ず注入**される。

サービス数が拡大するフェーズでは DI コンテナ（VContainer / Zenject 等）に移行する。

---

## 2. 層構成と asmdef

### 2.1 asmdef 構成

**現役 asmdef（6 個）**：

| asmdef | noEngineRefs | references | includePlatforms | 役割 |
|---|---|---|---|---|
| `Echolos.Domain` | true | [] | (all) | ドメイン・抽象定義 |
| `Echolos.Data` | false | [Domain] | (all) | SO・データロード機構・抽象実装 |
| `Echolos.UseCase` | true | [Domain] | (all) | ゲーム進行ロジック |
| `Echolos.Presentation` | false | [Domain, Data, UseCase] | (all) | UI・演出層・Composition Root |
| `Echolos.Tests.Domain` | false | [Domain, UseCase, Data, nunit] | [Editor] | Domain + UseCase テスト |
| `Echolos.Data.Editor` | false | [Domain, Data] | [Editor] | Editor 専用ツール |

**`_old/` 退避用 asmdef（7 個）**：`defineConstraints`（`ECHOLOS_OLD_PROTO` / `ECHOLOS_OLD_BATTLE` 等）＋ `autoReferenced: false` でコンパイル対象外化。詳細は §9.1。

| asmdef | 役割 |
|---|---|
| `Echolos.Domain.OldPrototype` | 段階1-2 古資産＋戦闘プロト固有 Domain（旧 Roster 系含む） |
| `Echolos.Domain.OldBattle` | 旧戦闘システム本体（BattleManager / TargetEvaluator / ActionExecutor / StatusEffectProcessor / BattleRunner / Conditional 派生 4 種） |
| `Echolos.Presentation.Old` | 戦闘プロト固有 GUI ＋ Debug シーン用 Spectator/Sandbox |
| `Echolos.Presentation.DevTools.Old` | 旧戦闘前提の Runtime Bootstrap（DebugBattleSandbox 等） |
| `Echolos.Presentation.DevTools.OldEditor` | 旧戦闘前提のバランス検証ツール群（BalanceReportTool 系・Editor 限定） |
| `Echolos.Data.OldEditor` | 旧戦闘前提の SO アセット生成ツール（SoAssetGenerator・Editor 限定） |
| `Echolos.Tests.Domain.Old` | 退避対象に依存していたテスト |

### 2.2 ディレクトリ構造

VSプロト範囲で実装済の構造を反映。Phase 1 以降でサブ namespace（UseCase の Campaign / Strategy / Meta 等、Data の Save / Scene / Event / Time 等）を順次拡張する想定。

本表は「何をどこに置くか」の方針を示す。各サブ namespace の **具体クラス列挙やアセット件数は記載しない**（情報は腐りやすく、コード／git を見れば一次情報が手に入るため）。役割の責務記述だけで方針を伝える。

```
Assets/
├── Resources/                   ← Resources.Load* の対象
│   ├── Data/                    SO アセット群
│   │   ├── Units/               UnitDefinitionSO
│   │   ├── Wazas/               WazaDefinitionSO
│   │   ├── MetaReward/          MetaRewardFormulaSO
│   │   ├── MetaUpgrades/        MetaUpgradeDefinitionSO
│   │   ├── DraftPools/          DraftPoolDefinitionSO
│   │   └── StoryScenes/         StorySceneDefinitionSO
│   ├── Icons/                   ちびキャラアイコン PNG（IconRegistry 経由・Unit.Id がファイル名）
│   │   └── Battlers/            陣営別階層 Allies/{id}.png / Enemies/{id}.png / Bosses/{id}.png
│   ├── Images/                  スチル PNG（key-visual / ending_* / intro_* 等）
│   │   └── VSPrototype/         戦闘背景・マップ背景・拠点アイコン（BackgroundRegistry 経由）
│   └── Fonts/                   NotoSansJP-Regular.ttf（GuiTheme.JapaneseFont 経由）
│
├── Scenes/                      EcholosProto_VS.unity / _old/ 退避済
│
└── Scripts/
    ├── Domain/                  Echolos.Domain.asmdef（noEngineRefs=true）
    │   ├── Models/              永続データ・戦闘中実体・状態異常・各種 Enums・UniqueUnitIds（固有 Id 定数の SSoT）
    │   ├── Battle/              戦闘ロジック（評価・実行・状態異常・パッシブ）
    │   │   ├── （直下フラット配置）  BattleManager / ActionExecutor / TargetEvaluator / ActionDeclaration / 純関数群（DamageFormula / DamageModifier / CounterAttackResolver / PositionAtkCorrection / InternalSlotResolver / ShieldConsumer / VictoryEvaluator / SpdOrderResolver / StatusEffectProcessor）
    │   │   ├── Skills/          IActionEffect / AttackEffect / 各 Effect クラス / Waza / RuntimeWaza / StatusEffectStacker
    │   │   ├── Replay/          BattleAssembly / BattleEventRecorder / BattleRunner / BattleReport / BattleResolver delegate / BattleLogFormatter
    │   │   ├── Synergy/         属性シナジー（データ駆動 + 静的 Applier・SynergyDefinitions / SynergyApplier）
    │   │   ├── Aura/            固有ユニットのオーラ機構（AuraDefinitions / AuraApplier・SourceUnit 在席時に陣営全体へ Permanent 付与・メタ強化 AuraBoost で量を加算）
    │   │   ├── Terrain/         地形補正純関数（TerrainKind / TerrainStrength / TerrainBonusCalculator）
    │   │   ├── Conditional/     条件型バフ基底（Hook 種別＋再帰ガード・将来動的バフ用に残置）
    │   │   └── _old/            Echolos.Domain.OldBattle（旧 Skills / 旧 Formula / 旧 Battle 本体・コンパイル対象外）
    │   ├── Items/               装備・アイテム
    │   ├── GameCycle/           経験値・控え回復ロジック
    │   ├── Catalog/             ID → Domain 完成品変換の抽象（Data 層で実装）
    │   ├── Formula/             メタ通貨報酬式レジストリ（旧戦闘式は Battle/_old/Formula へ退避済）
    │   ├── Meta/                メタ進行ドメイン
    │   ├── Draft/               ドラフトプール
    │   ├── Story/               ストーリーシーン・ページ・進捗
    │   ├── Save/                セーブ抽象（SaveSchema は Phase 1 で追加）
    │   ├── Prototype/           共通キャラクター名簿（本実装移行時に Models 配下に再配置検討）
    │   │   └── _old/            Echolos.Domain.OldPrototype（コンパイル対象外）
    │   ├── Scene/               （未実装・Phase 1 で ISceneFlow / SceneId 追加）
    │   ├── Event/               （未実装・Phase 1 で IEventBus / DomainEvent 追加）
    │   ├── Time/                （未実装・Phase 1 で IClock / IRandom 追加）
    │   └── Logging/             （未実装・Phase 1 で ILogger 追加）
    │
    ├── Data/                    Echolos.Data.asmdef（noEngineRefs=false）
    │   ├── Definitions/         POCO 集約（SO シリアライズ可能型・WazaPattern enum 含む）
    │   ├── Roster/              UnitDefinition / WazaDefinition のファクトリ集（数値 SSoT・SO 生成元）
    │   ├── （直下フラット配置）  SO ラッパー＋ Catalog 実装＋ SaveStore 実装
    │   ├── _old/                Echolos.Data.Old（旧 SO ローダ・旧 POCO・コンパイル対象外）
    │   └── Editor/              Echolos.Data.Editor.asmdef（SO アセット生成等の Editor ツール）
    │       └── _old/            Echolos.Data.OldEditor（コンパイル対象外）
    │
    ├── UseCase/                 Echolos.UseCase.asmdef（noEngineRefs=true）
    │   └── VSPrototype/         VSプロト範囲。本実装移行で Campaign / Strategy / Meta / Story / Unit 等のサブ namespace に再編
    │
    └── Presentation/            Echolos.Presentation.asmdef（noEngineRefs=false）
        ├── Common/              共通 UI 基盤（GUI スタイル・アイコン／背景／ガイドの遅延ロード）
        ├── Story/               ストーリー演出層
        ├── Battle/              戦闘可視化基盤（バフバッジ・状態異常オーバーレイ）
        ├── VSPrototype/         VSプロトの Composition Root＋各 *GUI
        ├── DevTools/            Debug 用検証スクリプト（Debug_*.unity シーンとセットで利用）
        │   ├── Editor/          Echolos.Presentation.DevTools.Editor.asmdef（Editor メニュー経由の運用ツール）
        │   │   └── _old/        Echolos.Presentation.DevTools.OldEditor（コンパイル対象外）
        │   └── _old/            Echolos.Presentation.DevTools.Old（コンパイル対象外・Runtime 用 Bootstrap）
        └── _old/                Echolos.Presentation.Old（コンパイル対象外）

Assets/Tests/Editor/Domain/       Echolos.Tests.Domain.asmdef（includePlatforms=[Editor]）
                  ├── Battle/          戦闘ロジック系テスト（DamageFormula / Synergy / Terrain / Effect / Action 等）
                  ├── GameCycle/       GameCycleTests（成長・装備・撤退・ロスト・控え回復の統合）
                  ├── VSPrototype/     VSプロト UseCase 系テスト（Resolver / State / Catalog / Save）
                  ├── _Helpers/        テスト共通ヘルパ（TestStubs 等）
                  └── _old/            Echolos.Tests.Domain.Old（コンパイル対象外）
```

### 2.3 namespace 規約

ディレクトリと namespace を 1:1 で対応させる。例：

- `Assets/Scripts/Domain/Battle/Replay/` → `Echolos.Domain.Battle.Replay`
- `Assets/Scripts/UseCase/Campaign/Round/` → `Echolos.UseCase.Campaign.Round`
- `Assets/Scripts/Presentation/Story/Cinematic/` → `Echolos.Presentation.Story.Cinematic`

---

## 3. 仕様要素 → 実装観点マッピング

VSプロト仕様（[310](310_vsprototype_spec.md) ラン進行 / [320](320_vsprototype_combat_spec.md) 戦闘 / [330](330_vsprototype_storyplot.md) ストーリー）で定義された仕様要素を、本書で定義する実装観点と対応付ける。**この章が本書の核**。

### 3.1 戦闘システム（最大 6 体オートバトル）を実現するために

> [320_vsprototype_combat_spec.md](320_vsprototype_combat_spec.md) 全章

シナジー中心・推測容易性原則の戦闘システム。仕様要素 → 実装観点マッピング。

#### 行動順・配置・ターゲティング

| 仕様要素 | 必要な実装観点 | 該当抽象 / 層 | 着手 Phase |
|---|---|---|---|
| SPD 順 + タイブレーク完全決定論（[320 §3.1](320_vsprototype_combat_spec.md)） | SPD 降順 → 陣営（IsAttackingSide 優先）→ 同陣営内 slot 昇順 | `Domain.Battle.SpdOrderResolver`（純関数） | Phase 0 |
| 配置 ATK 補正（近接 slot 0-5 / 遠隔 最後尾距離）（[320 §2.3-§2.4](320_vsprototype_combat_spec.md)） | 動的スロット再計算（戦闘中の死亡で詰め直し） | `Domain.Battle.PlacementBonusCalculator`（AttackEffect 内で参照） | Phase 0 |
| ターゲティング 4 通り（AttackKind × TargetingDirection）（[320 §2.2](320_vsprototype_combat_spec.md)） | 内部スロット詰め + FromFront/FromBack 方向選定 + TargetSelection 戦略 | `Domain.Battle.TargetEvaluator`（旧 D/H スコア廃止） | Phase 0 |

#### ダメージ・回復・バフ

| 仕様要素 | 必要な実装観点 | 該当抽象 / 層 | 着手 Phase |
|---|---|---|---|
| ダメージ式（√HP / DEF 分母 / 環境項）（[320 §1.2](320_vsprototype_combat_spec.md)） | DamageFormula 純関数 + 分母 1 クランプ（除算事故防止） | `Domain.Battle.DamageFormula` | Phase 0 |
| 回復式（√回復役現在 HP × 威力）（[320 §1.3](320_vsprototype_combat_spec.md)） | 最大 HP クランプ + 対象=最低 HP 割合 | `Domain.Battle.HealEffect` | Phase 0 |
| バフ・デバフ 3 系統（基礎加算 / 最終出力割合 / 確率パラメタ）（[320 §1.4](320_vsprototype_combat_spec.md)） | 作用箇所別の独立計算経路。乗算は最終出力のみ | `RuntimeUnit.GetEffectiveAtk/Def` + `DamageModifier` 純関数 | Phase 0 |
| 最終ダメージ計算順序（与ダメ%→被ダメ%キャップ 80%→クリ）（[320 §1.4.1](320_vsprototype_combat_spec.md)） | 加算スタック乗算 + キャップ + 最終乗算 | `Domain.Battle.DamageModifier` | Phase 0 |
| 回避（遠隔限定・50% キャップ）（[320 §1.4.2](320_vsprototype_combat_spec.md)） | EvasionUp 加算 + 命中ロール | `AttackEffect.ResolveHit` 内 | Phase 0 |

#### 反撃・防御・行動評価

| 仕様要素 | 必要な実装観点 | 該当抽象 / 層 | 着手 Phase |
|---|---|---|---|
| 反撃（近接 vs 近接限定・同時解決）（[320 §3.2-§3.3](320_vsprototype_combat_spec.md)） | CounterWaza 評価 + 共通フォールバック反撃 + 反撃の反撃なし | `Domain.Battle.CounterAttackResolver` | Phase 0 |
| 防御フォールバック（攻撃 Waza 不所持ユニット）（[320 §3.5](320_vsprototype_combat_spec.md)） | `def_guard` Waza 自動発動 + ActionGuard カテゴリ | `Domain.Battle.ActionExecutor` 内 + `StatusEffectProcessor.HandleActionStart` | Phase 0 |
| 行動評価方針（推測容易性原則）（[320 §3.6](320_vsprototype_combat_spec.md)） | AI スコア廃止 + Waza リスト順 + IsForcedWhenReady + Cooldown のみ | `Domain.Battle.DeclareAction`（旧 SupportActionSelector 6 段階優先度廃止） | Phase 0 |

#### Waza / Effect Strategy

| 仕様要素 | 必要な実装観点 | 該当抽象 / 層 | 着手 Phase |
|---|---|---|---|
| IActionEffect Strategy（[320 §3.7](320_vsprototype_combat_spec.md)） | Waza = `IList<IActionEffect>` 一本化。WazaCategory switch 廃止 | `Domain.Battle.IActionEffect` + 派生（§4.5） | Phase 0 |
| AttackEffect 凝集（命中→補正→ダメ→クリ→Shield→Rider→反撃）（[320 §3.7.4](320_vsprototype_combat_spec.md)） | 攻撃チェーン全体を 1 つの大型 Effect に凝集（暗黙結合・責務漏れ防止） | `Domain.Battle.AttackEffect` + `onHitRiders` | Phase 0 |
| 多段攻撃 × ヒットループ（[320 §3.2 + §3.7.4](320_vsprototype_combat_spec.md)） | HitCount 毎の独立命中判定 + 反撃 + シールド消費 | `AttackEffect` 内 | Phase 0 |
| 範囲攻撃（[320 §3.7.1](320_vsprototype_combat_spec.md)） | TargetingType（DirectionalEnemies / AllEnemies / AllAllies）+ 対象毎独立処理 | `Domain.Battle.TargetEvaluator` + `IActionEffect` | Phase 0 |
| 解除経路 3 系統（CleansesStatusAilments / DispelsBuffs / DispelsDebuffs）（[320 §4.7](320_vsprototype_combat_spec.md)） | Waza フラグ別の対象判定 + IsUndispellable で守護 | `Domain.Battle.DispelBuffsEffect` / `DispelDebuffsEffect` / `CleanseStatusAilmentsEffect` | Phase 0 |

#### 属性シナジー・地形

| 仕様要素 | 必要な実装観点 | 該当抽象 / 層 | 着手 Phase |
|---|---|---|---|
| 属性シナジー（火 / 水 / 光・戦闘開始時静的付与）（[320 §4.1-§4.2](320_vsprototype_combat_spec.md)） | データ駆動 SynergyDefinition + SynergyApplier 純関数（戦闘中の動的再評価なし） | `Domain.Battle.Synergy.SynergyApplier`（§4.5.5） | Phase 0 |
| 固有ユニットのオーラ（戦闘開始時静的付与・SourceUnit 在席時に陣営全体へ Permanent）（[320 §4.9](320_vsprototype_combat_spec.md)） | AuraDefinition + AuraApplier 純関数。SourceUnit の AppliedUpgrades から `UpgradeKind.AuraBoost` 合計を読み取り BaseMagnitude に加算（メタ強化 1 段で +2） | `Domain.Battle.Aura.AuraApplier` | Phase 0 |
| 光属性 HP% 全体回復（[320 §4.2](320_vsprototype_combat_spec.md)） | Persistent + ターン終了時発動（Burn 適用後）+ 最大 HP クランプ | `Domain.Battle.SubSynergyProcessor` | Phase 0 |
| 地形（列ごと火/水/中立 × 層別強度）（[320 §5](320_vsprototype_combat_spec.md)） | 属性 × 地形 × 強度から環境項算出（自属性 +α / 逆属性 -α / 補助 0） | `Domain.Battle.TerrainBonusCalculator` 純関数 + `BattleContext.Terrain` | Phase 0 |
| 敵編成地形追従（[320 §5.4](320_vsprototype_combat_spec.md)） | 列地形に合う属性タンクを編成に含める | `UseCase.VSPrototype.EnemyCompositionResolver`（プール × 地形 × 抽選） | Phase 1 |

#### 効果型階層

| 仕様要素 | 必要な実装観点 | 該当抽象 / 層 | 着手 Phase |
|---|---|---|---|
| 効果の振る舞いを派生クラスで表現（[320 §4.7](320_vsprototype_combat_spec.md)） | 16 派生クラス（`AbilityModifier` / `EvasionModifier` / `OutgoingDamageModifier` / `IncomingDamageModifier` / `CriticalRateModifier` / `CounterDamageModifier` / `HealReceivedModifier` / `ContinuousDot` / `ContinuousHot` / `FreezeEffect` / `ParalysisEffect` / `CurseEffect` / `ShieldEffect` / `SilencedCounterFlag` / `ReviveInvalidFlag` / `SelfGuard`）。新系統追加は派生クラス追加だけで既存処理は触らない（OCP） | `Domain.Effects.*` + `IEffect` / `EffectBase` + `EffectKind` enum | Phase 0 |
| 持続・解除の交差軸（[320 §4.7.2](320_vsprototype_combat_spec.md)） | `Lifetime`（Triggered / Permanent 2 値）／`IsUndispellable`（Dispel 拒否）／`IsCleansable`（Cleanse 対象）／`AuraSourceId`（オーラ起因）を独立フラグで持ち、派生クラスと直交させる | `Domain.Effects.EffectBase` 共通フィールド | Phase 0 |
| 付与拒否（[320 §4.7.5](320_vsprototype_combat_spec.md)） | `Unit.ImmunityKinds: HashSet<EffectKind>` で個別 Kind の集合を持つ。「全状態異常無効」は対象 Kind を全部入れる形で表現（総称概念は廃止） | `Domain.Models.Unit.ImmunityKinds` + `StatusEffectStacker.ApplyWithStacking` | Phase 0 |
| 解除経路 3 種（[320 §4.7.3](320_vsprototype_combat_spec.md)） | `DispelDebuffsEffect` / `DispelBuffsEffect` はモディファイア系派生クラスの `IsBuff` フラグで対象判定／`CleanseStatusAilmentsEffect` は `IsCleansable=true` を一括対象。ハードコード分類リストは廃止 | `Domain.Battle.Skills.Dispel*` / `CleanseStatusAilmentsEffect` | Phase 0 |
| Burn 蓄積モデル（[320 §1.4.4](320_vsprototype_combat_spec.md)） | `ContinuousDot`（`Lifetime=Permanent + IsCleansable=true`）。スタック上限なし + Shield 貫通 + 自然治癒なし + Cleanse 経路のみ解除 | `Domain.Battle.StatusEffectProcessor.HandleEndPhase` | Phase 0 |
| Shield 残数管理（[320 §4.1 水属性](320_vsprototype_combat_spec.md)） | `ShieldEffect`（`Lifetime=Permanent + IsUndispellable`）+ Stacks に残数 + 攻撃ダメージのみ吸収（DOT 貫通） | `Domain.Battle.ShieldConsumer` 純関数（AttackEffect 内） | Phase 0 |
| SearingWound（熱傷・[320 §4.8.3](320_vsprototype_combat_spec.md)） | `HealReceivedModifier`（`Lifetime=Permanent + IsCleansable=true` + `MaxStacks=9`）。HealEffect 内で `最終回復 = 素回復 × max(0, 1 - Magnitude/100 × Stacks)` を適用 | `Domain.Effects.HealReceivedModifier` + `Domain.Battle.Skills.HealEffect` | Phase 0 |

#### 勝敗判定・リプレイ・イベント

| 仕様要素 | 必要な実装観点 | 該当抽象 / 層 | 着手 Phase |
|---|---|---|---|
| 引き分け非対称評価（攻め 1 撃破 / 守り 0 被撃破）（[320 §6](320_vsprototype_combat_spec.md)） | BattleContext.IsAttackingSide で勝敗判定 | `Domain.Battle.VictoryEvaluator` 純関数 | Phase 0 |
| 戦闘リプレイ（録画 → 再生）（[320 §3.7](320_vsprototype_combat_spec.md) 派生） | イベント列で戦闘を録画・観戦ビューが時系列再生 | `Domain.Battle.Replay.BattleRunner` + `BattleEvent` | Phase 0 |
| 戦闘イベントのアクション単位スコープ集約 | 1 アクション = 1 `ActionResolved` イベント。`HitOutcome` を Outcomes に束ねて発火。観戦ビューは 1 件で範囲攻撃の HP 同時更新・集約ログ生成・合計ダメージ表示が可能 | `Domain.Battle.ActionExecutor.OnActionResolved` + `Domain.Battle.HitOutcome` + `Domain.Battle.Replay.BattleEvent.Outcomes` | Phase 0 |
| 戦闘ログと Events の構造的 1:1 同期 | `BattleEvent.LogLine` プロパティに 1 行ログを格納し、`BattleReport.Log` は Events.LogLine 非空から動的生成（read-only view）。Recorder の唯一の追加経路 `AddEvent(ev, logLine)` が Event と LogLine を同時セットするため、観戦ビューの cursor 進行とログ表示が構造的に同期する（規約依存ではなく型で強制）| `Domain.Battle.Replay.BattleEvent.LogLine` + `BattleEventRecorder.AddEvent` + `BattleReport.Log` (派生 view) | Phase 0 |

### 3.2 ラン進行・領地マップ・内政・メタ通貨を実現するために

> [310_vsprototype_spec.md](310_vsprototype_spec.md) 全章

7 ラウンド構成のラン進行（3 列 × 3 層＋本拠地）・領地マップ・内政（ドラフト／自動加入／兵種強化）・メタ通貨「王国の記憶」とメタ拠点（亡国の遺跡）の VSプロト仕様 → 実装観点マッピング。

| 仕様要素 | 必要な実装観点 | 該当抽象 / 層 |
|---|---|---|
| マップ構造・領地データモデル（[310 §1.2-§1.3](310_vsprototype_spec.md)） | 3 列 × 3 層＋本拠地のノード／自領・敵領・敵拠点の種別／バルドゥイン拠点フラグ | `UseCase.VSPrototype.MapNode` + `VSPrototypeMapState` |
| ラウンド進行（[310 §1.4](310_vsprototype_spec.md)） | R1-R7 状態機械・ラウンド開始イベント発火・配置確定・戦闘解決順序 | `UseCase.VSPrototype.VSPrototypeRoundManager` |
| 敵編成プール・抽選（[310 §1.4.1](310_vsprototype_spec.md)） | 強度 × 役割 × 地形追従の決定論的抽選（ドラフト枠別独立抽選＋ Pass1.5 補完）／皇太子戦の必敗版／通常版分岐 | `UseCase.VSPrototype.VSPrototypeEnemyPool` + `VSPrototypeEnemyPatterns` |
| 1 周目初期固定編成（[310 §1.11.4](310_vsprototype_spec.md)） | 1 周目ラン開始時に確定編成を返すスタブ | `UseCase.VSPrototype.VSPrototypeFirstRunFixedRoster` |
| 戦線概念・自領陥落・取り戻し戦（[310 §1.4.2 / §1.5](310_vsprototype_spec.md)） | 隣接性に基づく戦線判定・陥落列の戦線外化・取り戻し戦遷移 | `VSPrototypeMapState` 内 |
| ラウンド開始演出判定（[330 §5](330_vsprototype_storyplot.md)） | フラグ × ラウンド条件でシーン ID 選定（B-a/b1/b2/c/e・A-c1/c2） | `UseCase.VSPrototype.VSPrototypeRoundStartEventResolver` |
| ラン勝敗判定（[310 §1.7](310_vsprototype_spec.md) / [330 §6](330_vsprototype_storyplot.md)） | Defeat / True の 2 種類判定・3 分割 Defeat 演出振り分け | `UseCase.VSPrototype.VSPrototypeEndingResolver` + `VSPrototypeDefeatSceneResolver` + `VSPrototypeRunResult` |
| 内政フェーズ（[310 §1.11](310_vsprototype_spec.md)） | ドラフト・自動加入・兵種強化・前後列配置の 4 アクション | `UseCase.VSPrototype.VSPrototypeInteriorService` + `VSPrototypeInteriorState` + `VSPrototypeInteriorAction` + `VSPrototypeDraftService` |
| メタ通貨「王国の記憶」（[310 §3](310_vsprototype_spec.md)） | 獲得式（撃破数）・消費先（メタ強化購入）・ラン跨ぎ永続化 | `UseCase.VSPrototype.MetaProgressState` + `MetaProgressStore` + `MetaProgressSerializer` ＋ `ISaveStore` |
| 試遊モード（[310 §1.13](310_vsprototype_spec.md)） | R4 救出戦 1 セーブ構成・通常進行・メタ進行非保存・救出ピーク／ラン終了でタイトル戻り | `UseCase.Demo.DemoSaveCatalog` ＋ `IDemoFlowController` 抽象 |

### 3.3 ストーリー演出を実現するために

> [330_vsprototype_storyplot.md](330_vsprototype_storyplot.md) 全章

シーン ID とフラグ管理に基づく演出発火条件・分岐ロジックの実装観点マッピング。

| 仕様要素 | 必要な実装観点 | 該当抽象 / 層 |
|---|---|---|
| シーン ID とテキスト草案（[330 §2-§3](330_vsprototype_storyplot.md)） | SO 化されたシーン定義（ページ列・ナレ文・画像パス・フェード秒数）・既見短縮（`RepeatNarration` 自動切替） | `Data.StorySceneDefinitionSO` + `Domain.Story.StoryScene` + `IStorySceneCatalog` |
| 永続フラグ管理（[330 §4.1](330_vsprototype_storyplot.md)） | `HasReachedTrueEnd` / `HasFirstReachedBoss` / `HasRescuedBalduin` / `HasNotedPendantPower` / `UnlockedUnits[Bridget]` / `RunCount` / `SeenStorySceneIds` 等の永続化 | `UseCase.VSPrototype.MetaProgressState` ＋ `ISaveStore` |
| ラン中フラグ管理（[330 §4.2](330_vsprototype_storyplot.md)） | `IsBridgetRescued` / `IsBalduinRescuePlayed` / `IsBalduinSurrendered` 等のラン内状態 | `UseCase.VSPrototype.VSPrototypeMapState` |
| 演出発火条件判定（[330 §5](330_vsprototype_storyplot.md)） | ラウンド開始時／解放直後／ラン終了時の各タイミングでフラグ × 条件評価しシーン ID 選定 | `VSPrototypeRoundStartEventResolver` + `VSPrototypeDefeatSceneResolver` |
| ストーリーオーバーレイ描画（[330 §2-§3](330_vsprototype_storyplot.md)） | ページ送り・フェード・スチル切替・既見短縮ナレ表示 | `Domain.Story.StoryProgress` + `Presentation.Story.StoryOverlay` |
| マップ初期状態動的化（[330 §7](330_vsprototype_storyplot.md)） | `HasRescuedBalduin == true` のランで左列敵拠点をバルドゥイン拠点扱いから解除 | `VSPrototypeMapState` 初期化 |

### 3.4 セーブ要件を実現するために

| 仕様要素 | 必要な実装観点 | 該当抽象 / 層 |
|---|---|---|
| メタ進行（永続フラグ・解放ユニット・メタ通貨残量）の単一スロット永続化 | KVS インタフェースに JSON シリアライズ済み文字列を Load/Save／ラン終了時に Save | `Domain.Save.ISaveStore` + `Data.Save.PlayerPrefsSaveStore` + `UseCase.VSPrototype.MetaProgressStore` + `MetaProgressSerializer` |
| 試遊モードの固定セーブ（[310 §1.13](310_vsprototype_spec.md)） | 試遊版シーンから R4 開始セーブを直接ロード（ラン進行途中状態を再現） | `UseCase.Demo.DemoSaveCatalog` ＋ `DemoSaveDefinition` |

複数スロット・オートセーブ・手動セーブ・マイグレーション・破損検出・Steam Cloud 連携・クリア後セーブ管理は VSプロト範囲外（フル版設計時に追加）。

### 3.5 戦闘演出を実現するために

VSプロトでは戦闘画面は OnGUI の単体クラス（[VSPrototypeBattleGUI](../Assets/Scripts/Presentation/VSPrototype/VSPrototypeBattleGUI.cs)）に集約。倍速・スキップ（×4 段階）・全スキップ・戦闘ログ表示・複数戦闘連鎖・HP バー再生・死亡グレーアウト・属性シナジー集約ログ等を全てここで処理する。

| 仕様要素 | 必要な実装観点 | 該当抽象 / 層 |
|---|---|---|
| 戦闘ロジックは速度非認識・ビュー側で再生制御 | `BattleEvent` のイベント列を時系列再生・倍速はビュー側の更新間隔で制御 | `Domain.Battle.Replay`（速度非依存）+ `VSPrototypeBattleGUI` 内再生制御 |
| 戦場背景の取り込み（VSプロト 3 列地形ごと） | `Resources/Images/VSPrototype/{key}.png` のフォールバック付き遅延ロード | `Presentation.Common.BackgroundRegistry` |
| 透過ちびキャラの取り込み（unitId 別個別画像） | `Resources/Icons/Battlers/{Allies/Enemies/Bosses}/{unitId}.png` 遅延ロード | `Presentation.Common.IconRegistry` |
| 戦闘可視化バッジ（バフ・状態異常） | 効果バッジオーバーレイの描画 | `Presentation.Battle.UnitBadgeOverlay` + `StatusEffectOverlay` |

エフェクト・モーション・装飾枠・サウンド・拠点ビジュアル進化等のリッチ化はフル版で再生制御・エフェクト制御・サウンド統合・拠点シーン演出に責務分割する想定。詳細な GUI 設計方針は §7.4 を参照。

---

## 4. Domain 層

純 C#・Unity 非依存。テスト容易性と本実装移植性を最優先する層。

### 4.1 Models / Skills / Battle / Story の責務

- **`Domain.Models`**：`Unit`（永続データ）・`RuntimeUnit`（戦闘中の実体）・各種 Enums。状態変化は §1.3 API に従う
- **`Domain.Effects`**：効果型階層（`IEffect` / `EffectBase` / 16 派生クラス / `EffectKind` / `Lifetime` / `EffectFactory` / `EffectDefinition`）。詳細は §4.4 と §3.6
- **`Domain.Skills`**：`Waza`（不変テンプレ・定義データ・Formula ID + Params）・`RuntimeWaza`（戦闘中状態・CD / 使用回数）。`Unit` / `RuntimeUnit` 分離と同じ原則を Waza にも適用
- **`Domain.Battle`**：戦闘進行ロジック群。録画・再生機構は `Replay/` サブ namespace に隔離
- **`Domain.Story`**：ストーリーオーバーレイのロジック層（フェード・遷移・Skip）。描画は Presentation 側

本書 §3 で各 Domain クラスの責務を VSプロト仕様要素（[310](310_vsprototype_spec.md) / [320](320_vsprototype_combat_spec.md) / [330](330_vsprototype_storyplot.md)）にマップしている。

### 4.2 抽象インタフェース群（`Echolos.Domain.*`）

UseCase 層がここに依存し、Data 層が実装を提供する。

| サブ namespace | 抽象 | 役割 | 実装状況 |
|---|---|---|---|
| `Catalog` | `IUnitCatalog` / `IWazaCatalog` / `IMetaRewardFormulaCatalog` / `IMetaUpgradeCatalog` / `IDraftPoolCatalog` / `IStorySceneCatalog` | ID → Domain インスタンス変換 | ✅ VSプロトで 6 抽象とも実装済 |
| `Save` | `ISaveStore`（最小 KVS：Load/Save/Has/Delete）／ `SaveSchema`（Phase 1） | 文字列 KVS 永続化（Phase 1 で複数スロット・マイグレーション・チェックサム追加） | `ISaveStore` のみ実装済 |
| `Scene` | `ISceneFlow` + `SceneId` enum | シーン遷移の命令的 API | Phase 1 で追加 |
| `Event` | `IEventBus` + `IDomainEvent` | 層を跨ぐ Pub/Sub | Phase 1 で追加 |
| `Time` | `IClock` / `IRandom` | 時間・乱数のテスト可能化（シード制御） | Phase 1 で追加 |
| `Logging` | `ILogger` | 統一ログ窓口 | Phase 1 で追加 |

各抽象の interface 定義はコード（`Assets/Scripts/Domain/...`）を Single Source of Truth とする。

### 4.3 Formula Registry（`Echolos.Domain.Formula`）

クロージャ（Func）を ID + Params に置換するためのレジストリ。

- `DamageFormulaRegistry` — 標準セット 5 種（`standard_physical` / `standard_magical` / `multi_hit_physical` / `heal_flat` / `buff_only`）
- `TriggerConditionRegistry` — 標準セット 2 種（`self_hp_below_ratio` / `always_true`）

本番版で追加見込み：割合ダメージ・最大 HP 参照・距離減衰・属性相性込み等。

### 4.4 効果型階層（Domain.Effects）

効果は振る舞い軸（派生クラス）と持続／解除／識別子の独立軸を直交させる構造。総称分類（「能力デバフ」「状態異常」等）は持たず、派生クラス＋フラグで一意に決まる。

仕様要素 → 実装観点の対応表は **§3.1「効果型階層」を SSoT** とする。本節では設計判断（Why）と Conditional 基底フレームワーク・ログ表示ポリシーだけを書く。

#### 4.4.1 派生クラス＋独立軸

| 軸 | 表現 |
|---|---|
| 振る舞い | 派生クラス（16 種・`AbilityModifier` / `EvasionModifier` / 4 ダメージ系 / `HealReceivedModifier` / `ContinuousDot` / `ContinuousHot` / `FreezeEffect` / `ParalysisEffect` / `CurseEffect` / `ShieldEffect` / `SilencedCounterFlag` / `ReviveInvalidFlag` / `SelfGuard`） |
| 識別子 | `EffectKind` enum（型と独立軸・1 派生クラスが複数 Kind を持つことがある／例：`AbilityModifier` は AttackUp/AttackDown/DefenseUp/DefenseDown） |
| 自然消滅 | `Lifetime` enum（Triggered / Permanent 2 値） |
| Dispel 拒否 | `IsUndispellable` フラグ |
| Cleanse 対象 | `IsCleansable` フラグ |
| 付与拒否 | `Unit.ImmunityKinds: HashSet<EffectKind>`（Unit 側で Kind 集合を持つ） |
| 動的再評価 | `ConditionalBuffProcessor` 派生（§4.4.3） |

**型 → 整合性の保証**：派生クラスのコンストラクタが Kind と Magnitude を受け、`AbilityModifier(EffectKind.AttackUp, ...)` のような呼び方で `Stat` / `IsBuff` を内部派生する。整合チェック CI テストは派生クラス分離により不要。`EffectFactory.CreateByKind(kind, magnitude)` が全派生クラスの標準ファクトリ。

#### 4.4.2 SO シリアライズ用 POCO — `EffectDefinition`

`IEffect` は interface のため SO シリアライズ不可（Domain 層 `noEngineReferences=true` で `[SerializeReference]` も使えない）。Roster / SO アセットでは `EffectDefinition`（純 POCO・`[System.Serializable]`）で記述し、`ToEffect()` で派生クラスに変換する 2 段構成。

ファクトリ：
- `EffectDefinition.CreatePersistent(kind, magnitude, sourceAbilityName, maxStacks)` — Permanent + IsUndispellable=true（パッシブ／オーラ）
- `EffectDefinition.CreateTriggered(kind, magnitude, remainingTurns, maxStacks, stacks)` — Triggered + 有限ターン（スキル発動バフ・デバフ）
- `EffectDefinition.CreateConditional(kind, magnitude, auraSourceId, sourceAbilityName, maxStacks)` — Permanent + IsUndispellable + AuraSourceId（動的再評価オーラ）
- `EffectDefinition.CreateCleansable(kind, magnitude, stacks, maxStacks)` — Permanent + IsCleansable（Burn / Freeze / Paralysis / Curse / SearingWound 等）

書き手は仕様意図に合うファクトリを選べば整合した状態が作れる。例外的ケースは Definition 直接 new + フラグ明示で「これは例外」のシグナルになる。

#### 4.4.3 Conditional バフの抽象化（ConditionalBuffProcessor 基底）

Conditional バフは「**再評価の契機（フック）**」と「**評価関数**」が個別に必要（傭兵は陣営生存数で再評価／皇太子（闇）はターン経過で再評価）。これを集約するため抽象基底 `ConditionalBuffProcessor` を `Domain/Battle/Conditional/` に置く。

- 各 Processor が `Hooks` プロパティで購読フックを宣言
- BattleManager が登録された全 Processor に対し、フック発火時に該当する Processor だけ `Refresh` を呼ぶ
- フック種別は `ConditionalBuffHook` enum で集約（現状 5 種：BattleStart / TurnStart / UnitDied / BuffApplied / BuffRemoved）

**新規 Conditional バフ追加手順**：
1. `ConditionalBuffProcessor` 派生クラスを `Domain/Battle/Conditional/` に作成
2. `Hooks` プロパティで必要なフックを宣言（複数可）
3. `Refresh` を実装（必要なら `OnUnitDied` を override）
4. Bootstrap で `BattleManager` コンストラクタに渡す Processor リストに追加

**新しいフック種別が必要になった時**：
1. `ConditionalBuffHook` enum に値追加
2. BattleManager 側で対応する発火位置を追加（BattleRunner / ActionExecutor のイベント購読 → DispatchConditional 呼び出し）

**現状（プロト範囲）**：

Conditional 名前空間には派生クラスは存在しない。**基底フレームワーク（`ConditionalBuffProcessor` / `ConditionalBuffHook` / `EffectDefinition.CreateConditional` ファクトリ / `IEffect.AuraSourceId` プロパティ）のみ残置**して、将来「戦闘中に動的再評価が必要な動的バフ」が必要になったユニットが登場した時に派生クラスを足せる状態にしてある。

属性シナジー（火 / 水 / 光）は「戦闘開始時に確定して戦闘終了まで永続・解除されない」仕様のため、ConditionalBuffProcessor は使わずデータ駆動 + 静的 Applier 構成で実装している（§4.5.5 参照）。

**Conditional 同士の連鎖と無限ループ防止**：
バフ連動型 Processor は `BuffApplied` / `BuffRemoved` フックで他 Processor の付与・剥奪に追従する。連鎖は `RuntimeUnit.OnEffectAdded` / `OnEffectRemoved` → BattleRunner の購読 → `DispatchConditional(BuffApplied/Removed)` のチェーンで自然に発生する。

自身の `AddEffect` / `RemoveEffect` が `OnEffectAdded` / `OnEffectRemoved` を発火し、それが BuffApplied/Removed dispatch を経由して **同 Processor の Refresh を再帰呼び出し** するリスクがある。この再帰は基底クラス `ConditionalBuffProcessor.DispatchRefresh` が `_isRefreshing` フラグで **構造的に防止** する（BattleManager は Refresh を直接呼ばず必ず `DispatchRefresh` 経由で起動）。

派生クラスの書き手の責務は：
- **自 AuraSourceId 由来のエフェクトは集計から除外**（型違いで自然に分離できる場合は不要）
- **冪等性（差分判定）**：`Magnitude` が変わらないなら剥奪＋再付与しない early return

書き手が「再帰しないか」を心配する必要はない（基底で保証）。

`OnUnitDied` を override する場合は、自身の `Refresh` を `DispatchRefresh` 経由で呼ぶか、BuffApplied/Removed を購読していないことを前提に直接 `Refresh` 相当処理を呼ぶか、どちらかを選ぶ。

#### 4.4.4 ログ表示ポリシー（Lifetime / IsCleansable / SelfGuard 別の付与・剥奪ログ）

戦闘ログのテキスト出力は効果の種別ごとに「付与」「剥奪」の表示方針を分ける。構造化イベント（観戦ビューのバフバッジ更新用）は種別に関わらず全件記録する。

| 種別（判定軸） | 付与ログ | 剥奪ログ | 構造化イベント | 理由 |
|---|---|---|---|---|
| `Lifetime=Permanent` + `IsUndispellable=true`（パッシブ／オーラ） | 出す | **出さない** | 出す | 源が消えたら自動消滅が仕様。源側のログ（戦闘不能等）で意味は伝わる |
| `Lifetime=Permanent` + `AuraSourceId` 付き（Conditional） | 出す | **出さない** | 出す | 動的再評価の中間 remove や条件変化での連鎖剥奪がノイズになる |
| `Lifetime=Triggered`（有限ターンバフ・デバフ） | 出す | 出す | 出す | ターン切れタイミングがプレイ判断に直結 |
| `IsCleansable=true`（Burn / Freeze / Paralysis / Curse / SearingWound） | 出す | 出す | 出す | Cleanse や解除タイミングがプレイ判断に直結 |
| **`SelfGuard` 派生クラス** | **出さない** | **出さない** | **出さない** | 技名「防御」と効果が 1:1 対応で自明。バッジ表示も冗長で出さない |

実装は `BattleEventRecorder.SubscribeUnitEffects` の `OnEffectAdded` / `OnEffectRemoved` で派生クラス型 + 共通フラグを判定し、`AddEvent(ev, logLine)` の `logLine` 引数に表示文字列 or null を渡す。LogLine=null の Event は cursor 進行・HP 更新等の Apply は通常通り走るが、観戦ビューのログ表示はスキップされる（Persistent 剥奪のような「Event 必要・ログ冗長」を構造として表現）。

#### 4.4.5 拡張点

新しい効果系統が必要になったら、**派生クラス 1 個追加＋ `EffectKind` 値追加＋ `EffectFactory.CreateByKind` の switch case 1 行**で完結する（OCP）。既存の `StatusEffectProcessor` / Dispel・Cleanse 系・ログ整形などは pattern match で振る舞いが分かれるため、新派生クラスを認識しないだけで既存ロジックには手を入れない。

「将来動的再評価が必要な状態異常（HP 連動で呪いスタック自動増加等）」が出てきた場合は、§4.4.3 の `ConditionalBuffProcessor` 派生として実装可能。Processor の必要性（Static / Dynamic 軸）は派生クラスの振る舞い軸とは直交しているため、`IsCleansable=true` な効果に動的 Processor を組み合わせるのも自由。

### 4.5 戦闘ロジックの Strategy パターン

戦闘エンジン（`Domain.Battle`）は Waza の効果を `IList<IActionEffect>` として一本化し、`ActionExecutor` が順次 Apply する Strategy 構造。「攻撃 Waza」「バフ Waza」「回復 Waza」のような分類 enum は持たない。仕様詳細は [320 §3.7](320_vsprototype_combat_spec.md)。

#### 4.5.1 IActionEffect インタフェース

```csharp
public interface IActionEffect
{
    void Apply(IActionContext context);
}
```

`IActionContext` は `Battle` / `Actor` / `Targets` / `Random0To99` / `Outcomes` を提供する。各 Effect は `context.Targets` リストに対して 1 段の処理を行い、結果を `HitOutcome` として `context.Outcomes` に追加する。

設計意図：
- `WazaCategory` enum の switch 分岐を持たないことで、効果の組み合わせで Waza の振る舞いを表現する
- 新規 Waza 追加時に `ActionExecutor` に分岐を追加する必要がない
- 各 Effect は独立クラスで単体テスト可能

#### 4.5.2 主要な Effect

| Effect | 役割 |
|---|---|
| `AttackEffect` | 攻撃チェーン全体を凝集（命中・配置/地形補正・ダメ・クリ・Shield・付帯 Rider・反撃） |
| `HealEffect` | §1.3 回復式 |
| `ApplyStatusEffectEffect` | `IEffect` テンプレを対象に Clone して付与 |
| `DispelBuffsEffect` / `DispelDebuffsEffect` / `CleanseStatusAilmentsEffect` | 3 系統の解除経路 |

汎用ヘルパ：
- `StatusEffectStacker`（純関数）：同 `EffectKind` の既存効果があれば MaxStacks までスタック加算＋強度・残ターンをリフレッシュ。`Unit.ImmunityKinds` による付与拒否も判定。複数 Effect で再利用される共通スタッキング処理を 1 箇所に集約

#### 4.5.3 AttackEffect の凝集設計

攻撃チェーンは Effect 同士の依存が強い（命中しなければダメ計算しない・回避時は付帯効果も発動しない・反撃は近接 vs 近接のみ）。Effect 並列モデルでは Effect 間の暗黙結合や反撃発動の責務漏れが発生するため、攻撃チェーンを `AttackEffect` 1 クラスに凝集する。

```
命中判定（EvasionUp + IsSureHit・遠隔限定・キャップ 50%）
   ↓
配置 ATK 補正・地形補正の動的計算
   ↓
DamageFormula → 与ダメ% → 反撃時のみ CounterDamageUp → 被ダメ% → クリ判定 → Shield
   ↓
ダメ適用 + HitOutcome
   ↓
onHitRiders 発動（命中時のみ・同 target 限定）
   ↓
反撃発動（CounterAttackResolver・反撃の反撃なし）
```

付帯効果（炎上付与・ATK デバフ付与等）は `onHitRiders` として `AttackEffect` コンストラクタに渡す。Rider は既存の `IActionEffect`（`ApplyStatusEffectEffect` 等）をそのまま再利用できる。

`IsSureHit`（必中フラグ）は Waza レベルではなく `AttackEffect` コンストラクタ引数として持つ。命中判定が `AttackEffect` 内に完全凝集されるため、Waza 側に同フラグを置く重複を排除。

#### 4.5.4 AI 行動評価方針（推測容易性の原則）

戦闘 AI は「推測容易性の原則」に従い、**プレイヤーが事前に「このユニットは何をするか」を予測できる**範囲に留める。詳細は [320 §3.6](320_vsprototype_combat_spec.md)。

| 項目 | 仕様 |
|---|---|
| AI スコア評価 | **実装しない**（旧仕様の「基礎予定ダメージ / 現在 HP」スコア比較は廃止） |
| 支援行動の優先順位判断 | **実装しない**（旧仕様の cleanse > heal > debuff > buff は廃止） |
| 複数 Waza の使い分け | `BattleWazas` のリスト順 + `IsForcedWhenReady` + `Cooldown` のみで表現 |

`RefactoredTargetEvaluator.DeclareAction` の流れ：

1. 行動不能判定（麻痺・凍結）→ 待機
2. `IsForcedWhenReady=true` の Waza を順に評価 → 使用可能 ＆ 対象あり なら採用
3. 通常 Waza を順に評価 → 同上
4. すべて空振り → `def_guard` フォールバック

設計意図：
- プレイヤーは Waza リスト順を確認するだけで「どの技をいつ撃つか」を予測できる
- 戦闘結果が AI の隠れた判断ロジックで揺れない
- 旧仕様の「軍師の交互発動」「司祭の HP 閾値回復」等の例外的判断は、`IsForcedWhenReady + Cooldown + TargetingCondition` の組み合わせで表現可能と判明

#### 4.5.5 属性シナジー（静的 Applier・データ駆動）

属性シナジー（火 / 水 / 光）は「戦闘開始時に確定して戦闘終了まで永続・解除されない」仕様のため、ConditionalBuffProcessor の動的再評価機構は使わず、データ駆動 + 静的 Applier で実装する。

| 要素 | 役割 |
|---|---|
| `SynergyBuff` POCO | 単一効果（EffectKind / Magnitude / InitialStacks） |
| `SynergyTier` POCO | 属性体数 1 段階の効果定義（Buffs[] / TargetCount / SortBy）。`TargetCount=-1` で全員 |
| `SynergyDefinition` POCO | 1 属性の完全定義（TriggerElement / SourceAbilityName / Tiers[]・index=体数） |
| `SynergyDefinitions` 静的 | プロト 3 属性の Definition 集約（Fire / Water / Light / All） |
| `SynergyApplier` 静的純関数 | `ApplyAll(BattleContext, IEnumerable<SynergyDefinition>)` を BattleStart で 1 回呼ぶ |

**付与方式**：`EffectFactory.CreateByKind` + `Lifetime=Permanent`・`IsUndispellable=true` で SynergyApplier が直接生成。`SourceAbilityName`（「炎の共鳴 Lv6」等）で識別表示。

**`SortBy` は既存 `TargetSelection` enum 流用**（`HighestAtk` 等）。「全員」は `TargetCount=-1` で表現。

**拡張パターン**：
- 新属性追加 → `SynergyDefinitions` に 1 要素追加するだけ
- 体数別効果値変更 → Definitions テーブル書き換え
- 新しいソート基準が必要になったら `TargetSelection` enum に値追加＋ Applier で 1 分岐追加
- 仮に将来「特定属性のみ動的にしたい」が来たら、`SynergyDefinition` に `IsDynamic` フラグを足して、該当 Definition のみ `ConditionalBuffProcessor` 派生を別途実装する形で疎結合に拡張可能

仕様は [320 §4.1〜§4.2 / §4.7](320_vsprototype_combat_spec.md) を SSoT とする。

---

## 5. Data 層

### 5.1 SO / POCO / Catalog の 3 層

```
ScriptableObject  (UnitDefinitionSO / WazaDefinitionSO)
        ↓ Inspector 編集・Asset 保存
POCO              (UnitDefinition / WazaDefinition)
        ↓ Catalog.Get(id) で構築
Domain            (Unit / Waza)
```

**中間 POCO を挟む理由**：テスト独立性（SO 介さず構築可能）・JSON 移行点の集約・SO シリアライズ可能型制約（`Func<>` 不可）の責務分離。

### 5.2 Catalog 実装（`Echolos.Data.Catalog`）

`UnitCatalog : IUnitCatalog` / `WazaCatalog : IWazaCatalog`。`Resources.LoadAll<UnitDefinitionSO>` で lazy init し、POCO → Domain 変換を担う。

Addressables 化が必要なフェーズでは `Resources.LoadAll` を差し替える（`IUnitCatalog` 実装の差し替えで完結し UseCase 側の変更なし）。

### 5.3 SaveStore 実装（`Echolos.Data.Save`）

**VSプロト現状**：`PlayerPrefsSaveStore : ISaveStore`（最小 KVS 実装）。`UseCase.VSPrototype.MetaProgressStore` 経由でメタ進行のみ永続化。複数スロット・マイグレーション・チェックサム未対応。

**Phase 1 で `JsonSaveStore : ISaveStore` に移行**：`Application.persistentDataPath/saves/` 配下に JSON でスロット保存。Load 時に `SaveMigrator` でバージョン互換マイグレーション。

Steam Cloud 連携が必要なフェーズでは `SteamCloudSaveStore` を追加（`ISaveStore` 実装の差し替えで完結）。

### 5.4 その他のインフラ実装

§4.2 の Scene / Event / Time / Logging 抽象に対応する Unity / .NET 実装を Phase 1 で追加。シーン遷移は Unity 依存実装、Pub/Sub・時間・乱数・ログは純 .NET 実装で対応する想定。命名は §10.1（Unity 依存実装は `Unity*` プレフィックス、その他は I なしの名詞）。

### 5.5 Editor ツール（`Echolos.Data.Editor`）

`includePlatforms: [Editor]`。本番ビルドに含まれない。コンバータ・データ検証スクリプト等。

### 5.6 SO 化対象

§5.1〜5.4 の基本 SO / 抽象実装に加え、§1.4 データ駆動原則の対象として以下を SO 化対象とする。

VSプロト範囲で SO 化したものは「POCO（`*Definition`）＋ SO ラッパー（`*DefinitionSO`）＋ Catalog 実装」の 3 層構成を採用。Catalog は Domain 完成品（`Unit` / `MetaUpgrade` / `DraftPool` / `StoryScene` 等）に変換して返し、`Data.Definitions` の POCO は Catalog 実装内部だけが知る（Domain → Data 逆依存回避）。

| 対象 | POCO / SO ラッパー / Domain 完成品 | 配置 |
|---|---|---|
| ストーリーシーン（ナレ文・画像パス・フェード秒数） | `StoryPageDefinition` / `StorySceneDefinition` / `StorySceneDefinitionSO` / `StoryScene` | POCO は `Data.Definitions`、SO ラッパーは `Data` 直下、Domain は `Domain.Story` |
| ドラフトプール構成（兵種ID リスト＋確率） | `DraftPoolDefinition` / `DraftPoolDefinitionSO` / `DraftPool` | POCO は `Data.Definitions`、SO ラッパーは `Data` 直下、Domain は `Domain.Draft` |
| 報酬計算式＋パラメタ | `MetaRewardFormulaDefinition` / `MetaRewardFormulaSO` / `MetaRewardFormula` ＋ `MetaRewardFormulaRegistry`（`DamageFormulaRegistry` と同パターン） | POCO は `Data.Definitions`、SO ラッパーは `Data` 直下、Domain は `Domain.Formula` |
| メタ強化定義（コスト・上限・効果） | `MetaUpgradeDefinition` / `MetaUpgradeDefinitionSO` / `MetaUpgrade` | POCO は `Data.Definitions`、SO ラッパーは `Data` 直下、Domain は `Domain.Meta` |

ID 識別子（`MetaUpgradeIds.PrincessAtk` / `MetaUnitIds.Bridget` / `VSPrototypeStorySceneIds.EndingTrue` 等）は SO の主キーとしてコード側に残す（§6.2 例外条項）。

---

## 6. UseCase 層

### 6.1 責務

ゲーム進行のシナリオロジック。Domain の抽象（`IUnitCatalog`・`ISaveStore`・`ISceneFlow`・`IEventBus`・`IClock`・`IRandom` 等）に依存し、Unity API は使わない。

### 6.2 全 UseCase クラスは sealed instance class

`static class` は禁止。コンストラクタで依存（抽象）を受け取る。

**例外**：以下は `static class` のままで可（DI 注入で得るものがないため）。

- **定数 ID 集約**：const string のみで構成される識別子集約（`MetaUpgradeIds` / `MetaUnitIds` 等）
- **純データ集約**：const / static readonly 配列のみで構成され、メソッドを持たない（または持っても定数値を返すだけ）
- **純関数ヘルパ**：state を全て引数で受け取り副作用を持たない（`MetaProgressSerializer.ToJson(state)` 等）

ただし上記例外に該当しても、§1.4 データ駆動原則の対象（演出データ・ゲーム調整データ）に該当する場合は SO 化を優先する。

### 6.3 namespace 分割規約

UseCase は機能領域でサブ namespace に分割する。

| サブ namespace | 内容 |
|---|---|
| `Echolos.UseCase.VSPrototype` | VSプロト範囲のラン進行・領地マップ・敵編成・内政・メタ通貨・エンディング判定（[§3.2 マッピング表](#32-ラン進行領地マップ内政メタ通貨を実現するために) 参照） |
| `Echolos.UseCase.Demo` | 試遊モードのセーブ／シナリオカタログ（[310 §1.13](310_vsprototype_spec.md)） |

フル版実装時は機能領域別の細分化（Campaign / Strategy / Meta / Story / Unit 等）を別途設計する。本書はプロト範囲の現状を SSoT とする。

---

## 7. Presentation 層

### 7.1 責務

UI（MonoBehaviour・OnGUI）・シーン制御・演出・DI 配線（Composition Root）。

### 7.2 Composition Root（Bootstrap）

`Bootstrap` MonoBehaviour が Composition Root として機能。`Awake` / `Start` で：

1. Catalog 実装をインスタンス化
2. インフラ実装をインスタンス化
3. UseCase 群をコンストラクタ注入で組み立て
4. 各 GUI MonoBehaviour に UseCase インスタンスを渡す

DI コンテナ導入後は `Bootstrap` が Container 構築に集約される。

### 7.3 GUI / 演出層のサブレイヤー

| サブ namespace | 責務 |
|---|---|
| `Echolos.Presentation.Common` | 共通 UI 基盤（カラーテーマ・アイコン／背景／ガイド／ストーリー文言／ユニットラベルの集約と画像遅延ロード） |
| `Echolos.Presentation.Story` | ストーリーオーバーレイ描画（`Domain.Story.StoryProgress` 連動） |
| `Echolos.Presentation.Story.Cinematic` | 「ありがとう」演出・拠点ビジュアル進化・悲劇追想シーン |
| `Echolos.Presentation.Battle` | 戦闘可視化バッジ（バフ・状態異常オーバーレイ） |
| `Echolos.Presentation.Battle.Fx` | 戦闘エフェクト（剣閃・矢の軌跡・魔法陣・着弾フラッシュ等） |
| `Echolos.Presentation.Audio` | SE / BGM 統合管理 |
| `Echolos.Presentation.VSPrototype` | VSプロトの Composition Root + 各 *GUI（戦略マップ／内政サブモード／戦闘 UI（Phase 2 仮作成）／ストーリー演出／メタ拠点） |

### 7.4 UI 設計方針

**IMGUI 採用はプロト段階の判断**：VSプロトでは実装負荷を抑えるために Unity 標準の IMGUI（OnGUI）で UI を組んでいる。フル版実装では uGUI / UI Toolkit 等への移行を含めて GUI 設計を再考する想定。本節の設計方針のうち §7.4.2 / §7.4.3 / §7.4.6 は IMGUI 前提だが、§7.4.1 プラットフォーム前提・§7.4.4 アセット取り込みパターン・§7.4.5 ビジュアル方針はフレームワーク非依存で本実装にもそのまま持っていける普遍的判断。

IMGUI 由来の落とし穴と回避パターン（OnGUI モーダル 3 大落とし穴・GUILayout の罠・WebGL フォント遅延ロード等）は [920_implementation_notes.md §1](920_implementation_notes.md) を参照。本節は「変えてはいけない設計原則」、920 は「落とし穴と回避パターン」という役割分担。

#### 7.4.1 プラットフォーム前提

PC ブラウザ専用（[900 §2.1](900_development_rules.md)）。ホバー前提 UI（ツールチップ等）を躊躇なく採用する。スマホ縦持ち・タッチ操作は対象外。Canvas 解像度基準 1920×1080。

#### 7.4.2 画面遷移とフェーズガード

- 画面遷移の Single Source of Truth は `VSPrototypePhase` enum と [VSPrototypeBootstrap](../Assets/Scripts/Presentation/VSPrototype/VSPrototypeBootstrap.cs)。各 GUI は OnGUI 冒頭で自身の描画条件 Phase を判定し、対象外なら即 `return`
- 同 GameObject に複数 GUI が並ぶ場合は **Phase 別早期 return で描画担当を排他化** する（`[DefaultExecutionOrder]` は実機で不安定なケースあり）
- 進行シーケンスは [310 §1.10](310_vsprototype_spec.md) を参照

#### 7.4.3 共通 UI 基盤の SSoT 集約

- 色パレットと `GUIStyle` の Single Source of Truth は [GuiTheme.cs](../Assets/Scripts/Presentation/Common/GuiTheme.cs)
- 各 GUI クラスは `GuiTheme.*` を参照するだけで、独自色は最小化する
- フォント・アイコン・背景画像も同様に [Presentation.Common](../Assets/Scripts/Presentation/Common/) 配下の Registry / Theme クラスに集約し、各 GUI からは参照のみ

#### 7.4.4 アセット取り込みパターン（フォールバック付き遅延ロード）

ユーザーアセット供給と Claude コード対応を並行可能にするため、画像系アセットは以下のパターンで取り込む：

- **遅延ロード＋ミッシングキャッシュ**：`Resources.Load<Texture2D>(path)` を最初のアクセスでロードし、結果（成功 Texture2D／失敗 null）をキャッシュ。次フレーム以降は毎回 `Resources.Load` しない
- **フォールバック描画**：呼び出し側は「ロード成功なら描画／失敗なら従来の単色塗りや矩形フォールバック」の分岐を組む。アセットが配置されれば次回起動で自動反映
- 実装：[IconRegistry](../Assets/Scripts/Presentation/Common/IconRegistry.cs)（`Resources/Icons/Battlers/{Allies\|Enemies\|Bosses}/{unitId}.png` 優先＋旧 `Resources/Icons/{unitId}.png` フォールバック）／[BackgroundRegistry](../Assets/Scripts/Presentation/Common/BackgroundRegistry.cs)（`Resources/Images/VSPrototype/{key}.png`）

#### 7.4.5 ビジュアル方針

- **チビキャラ規格**：全身横向き／汎用ユニット 2 頭身／ボス 3 頭身。AI 生成（ChatGPT 等）で量産する前提
- **戦闘演出**：チビキャラはアニメーションを採用せず**一枚絵固定**で扱う。動きはエフェクト（剣閃・矢の軌跡・魔法陣・着弾フラッシュ等）で表現する方針（少フレーム＋エフェクト主体のアプローチ）
- **背景＋透過アイコン重ね**：戦闘画面・マップ画面とも、背景画像 1 枚＋透過 PNG アイコンの重ね方式。`GUI.color` で死亡時グレースケール・α 制御
- **対峙構図**：右向き素材で統一し、敵スロットは [IconRegistry.TryDrawIcon](../Assets/Scripts/Presentation/Common/IconRegistry.cs) の `flipHorizontal=true` で対峙化
- **透過素材の上に装飾矩形を描かない**：スロット枠／カード背景／半透明陣営色などを透過 PNG の裏に敷くと、せっかくの透過素材のシルエットが矩形でブツ切れて見える。敵味方区別は**位置（左右配置）＋ flipHorizontal ＋下部ステータスリスト**で十分視認可。死亡視認は `GUI.color` 暗色化で代替（VSプロト 戦闘 UI フォロー②で確立）
- **背景画像は描画対象 Rect のアスペクトに合わせる**：全画面に対して 16:9 等の縦長画像を ScaleAndCrop で貼ると、`GUI.enabled` で上塗りされる下部 UI 領域に画像下端 25% が埋もれて無駄になる＋画像中央付近の地平線がキャラ位置と合わない。**背景は描画対象 Rect だけに描画＋画像アスペクトをその Rect に合わせて作る**ことで、画像全体を活かしつつ地平線位置を意図通り配置できる（VSプロト 戦闘 UI フォロー③で確立）
- **複数レーン縦並びは擬似 3D 遠近で奥行きを出す**：前後 3 レーン×2 列の戦闘グリッドのようなレイアウトは、平面的に並べるとスカスカに見える。各レーンを Y 方向で重ねつつ、**奥のレーンほどスケールを縮小＋中央軸寄りに X オフセット**（消失点は画面中央奥）すると擬似 3D の遠近表現になる。数値は仕様書側に持つ（[310 §1.8.2](310_vsprototype_spec.md)）
- **マップ系の装飾は背景画像で表現し、コード側で重ねない**：背景画像にガイド（ノード配置位置の円・進攻ルートの道・領境の川や森など）を描き込み、コード側はその位置にノードを配置するだけにする。マス背景色塗り／マスラベル文字／配置可能ハイライト枠／接続線描画などをコード側に持つと、せっかくの背景画像を覆い隠してノイズになる（VSプロト マップ画面フォロー③④で確立）
- **絵側のデザインで階層差を表現**：同じ用途のアイコンでも「拠点（city・城）と野営地（camp・テント）」のような視覚階層差は絵側のデザインで表現し、コード側でサイズ分岐するロジックは最小限に。コード側で差をつけるとしても「直径 2 値の分岐」程度に抑える（VSプロト マップ画面 city/camp 分岐で確立）

#### 7.4.6 数値調整パラメータの Inspector 公開

UI レイアウト数値（座標・サイズ・パディング等）で「実機で見ながら微調整したい」ものは、`[SerializeField] private` で MonoBehaviour に公開する。`[Range]` `[Header]` `[Tooltip]` で UX を整える。Inspector で Play 中含めて即反映 → 確定値を後でコードのデフォルトに反映する運用。

IMGUI のコードハードコード調整サイクル（コード変更 → ドメインリロード待ち → 確認）が回らない問題への対策として有効。本実装でも使える普遍的なパターン。

- 実装例：[VSPrototypeMapGUI._nodeImageCenters / _cityDiameter / _campDiameter](../Assets/Scripts/Presentation/VSPrototype/VSPrototypeMapGUI.cs)
- §1.3（状態変化通知 API）との関係：`private` フィールドなのでカプセル化は保たれる。`[SerializeField] private` は §1.3 の `public` 直接公開禁止と矛盾しない
- Inspector Override の罠（コードのデフォルトを変えても Inspector の値が優先される件）は [920 §1.5](920_implementation_notes.md) を参照

---

## 8. テスト戦略

### 8.1 テスト対象

- **Domain**：純 POCO のロジック・状態遷移
- **UseCase**：モック抽象（`IUnitCatalog` / `ISaveStore` / `IRandom` / `IEventBus` 等）注入でシナリオロジックを網羅検証
- **MonoBehaviour・UI 自動テストは書かない**（Play モード・画面描画依存）

### 8.2 モック注入パターン

抽象（`IUnitCatalog` / `IRandom` / `IEventBus` 等）に対応する Stub / Spy をテスト側に用意し、UseCase 単体テストで決定論的に検証する。

### 8.3 SO 実体ロードテスト

「全 SO アセットが正しくロード可能か」を網羅するテストを `Echolos.Tests.Domain` に置く。`UnitCatalog.GetAllIds()` の件数とアセットフォルダ実体の数を照合し、兵種抜けを構造的に検出。

### 8.4 Registry 単体テスト

- `DamageFormulaRegistry`：各 Formula ID の数式単独テスト
- `TriggerConditionRegistry`：発動条件の境界値テスト

### 8.5 シナリオ統合テスト

UseCase レベルで「1 ラウンドの完全な流れ」を統合テスト。モックの `IRandom` と `IEventBus` で決定論的に検証。

### 8.6 ヘッドレステスト不可・Test Runner 実行

本プロジェクトの開発環境ではヘッドレステスト実行不可（exit 198）。Claude 側でテスト実行確認はせず、ユーザーに Unity Editor の Test Runner で All Tests 実行を依頼する（[900_development_rules.md §3.7](900_development_rules.md)）。

---

## 9. プロト範囲との切り分け

### 9.1 戦闘プロトの扱い（`_old/` 退避）

再利用しない陳腐化資産は `_old/` 配下に退避し、コンパイル単位から物理除外する。退避先には専用 asmdef を配置し、`defineConstraints`（`ECHOLOS_OLD_PROTO` / `ECHOLOS_OLD_BATTLE` 等の未定義シンボル）で常にコンパイル対象外にする。退避先の asmdef は `autoReferenced: false` を設定し、他 asmdef から自動参照されないようにする。

| 退避先 | asmdef | 役割 |
|---|---|---|
| `Assets/Scripts/Domain/Prototype/_old/` | `Echolos.Domain.OldPrototype` | 段階1-2 古資産＋戦闘プロト固有 Domain（旧 Roster 系含む） |
| `Assets/Scripts/Domain/Battle/_old/` | `Echolos.Domain.OldBattle` | 旧戦闘システム本体（BattleManager / TargetEvaluator / ActionExecutor / StatusEffectProcessor / BattleRunner / ActionDeclaration / Conditional 派生 4 種）＋旧 `Skills/`（Waza / RuntimeWaza / Skill）＋旧 `Formula/`（DamageFormulaRegistry / TriggerConditionRegistry） |
| `Assets/Scripts/Data/_old/` | `Echolos.Data.Old` | 旧 SO 5 層（WazaCatalog / UnitCatalog / WazaDefinitionSO / UnitDefinitionSO / Definitions/WazaDefinition / Definitions/UnitDefinition） |
| `Assets/Scripts/Presentation/_old/` | `Echolos.Presentation.Old` | 戦闘プロト固有 GUI |
| `Assets/Scripts/Presentation/DevTools/Editor/_old/` | `Echolos.Presentation.DevTools.OldEditor` | 旧戦闘前提のバランス検証ツール（BalanceReportTool 系） |
| `Assets/Scenes/_old/` | （asmdef なし） | 戦闘プロトメインシーン |

戦闘プロトのシーン・テストは動作保証しない。

---

## 10. 命名規約

### 10.1 クラス・interface

- インタフェースは `I` プレフィックス：`IUnitCatalog` / `IWazaCatalog` / `ISaveStore` / `IEventBus`
- 実装クラスは名詞だけ：`UnitCatalog` / `JsonSaveStore` / `EventBus`
- プロトプレフィックス（`Stage3` 等）を共通資産に付けない（旧戦闘プロト由来の命名）
- 継承前提でないクラスは全て `sealed`

### 10.2 ファイル・ディレクトリ

- ファイル名は対象クラス名と一致：`UnitCatalog.cs` / `IUnitCatalog.cs`
- ディレクトリは namespace と一致（§2.3）

### 10.3 シーン

- 本番系：`EcholosProto_*.unity`（『Echoes of the Lost Kingdom』略称 echolos + プロト）
- デバッグ系：`Debug_*.unity` プレフィックス。Build 対象外

### 10.4 `_old/` 退避

段階移行で不要になったシーン・スクリプト・仕様書は削除せず `_old/` 配下に退避する。退避先に専用 asmdef を配置し、`defineConstraints` でコンパイル対象外化（[900_development_rules.md §4.2](900_development_rules.md)）。
