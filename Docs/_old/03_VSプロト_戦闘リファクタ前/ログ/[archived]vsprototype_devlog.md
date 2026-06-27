# Echoes of the Lost Kingdom — VSプロト実装ロードマップ

> **【アーカイブ通知】2026-06-16**
> 戦闘プロトからの引継ぎ～VSプロトの初期実装のClaude作業計画とログ
> 記録として残すが参照はしないこと。

> 本書は **VSプロト（USP実証 Vertical Slice プロトタイプ）** の実装計画と進捗を管理する。
> VSプロトの方針は [300_vsprototype_policy.md](300_vsprototype_policy.md)、仕様は [310_vsprototype_spec.md](310_vsprototype_spec.md) を参照。

---

## 1. VSプロトの位置づけ

| 段階 | 主目的 | 完了状態 |
|---|---|---|
| **VSプロト** | **USP（H4）を視覚的・体験的に実証** | 進行中 |
| 試遊版（採択後 or Phase 0 後段） | 製品品質の1ラン体験 | 採択後 or 個人完遂 Phase 0 後段 |

---

## 2. 実装ステップ

> **重さ**：大／中／小は実装コストの相対感（具体工数は遊びながら調整するので未記載）。
> 大＝新規アーキ＋多数の派生変更、中＝既存資産再利用しつつまとまった新規実装、小＝局所的な追加・調整。

### Phase 0：データ駆動リアーキ（戦闘プロト捨て可前提） — 重さ：**特大**

> **着手判断**（2026-06-08 確定）：Stage3Roster.cs での「C# ファクトリ関数群によるユニット定義」は技術的負債と認識。「最終的に個人開発でフル版を完成させる」目標を優先し、コンテスト締め切りに縛られず**本実装に持っていける設計**にする。
>
> **戦闘プロトは捨て扱い**：戦闘プロト関連コードを `Assets/Scripts/Domain/Prototype/_old/` に退避し、コンパイル単位から物理除外する。VSプロト USP 実証が最優先。
>
> **詳細設計**：[500_architecture.md §1.4 / §3 / §5](500_architecture.md) を参照（4 層アーキ＋データ駆動原則＋Catalog 実装）。

**Phase 0 のサブステップ**：

#### Step 0-1：設計ドキュメント整備 ✅ 完了（2026-06-08）
- [500_architecture.md §1.6](500_architecture.md) にデータ駆動設計原則を追加
- [500_architecture.md §9](500_architecture.md) にデータ駆動設計章を追加（POCO + SO + Registry + Catalog の全体像）
- 本 Phase 0 セクションをロードマップに挿入
- メモリ更新（project-vsprototype-plan に Phase 0 反映・本実装持ち込み方針を記録）
- **コミット**：`13b0869` / `2dedee7`（既存記述の最新化）

#### Step 0-2：Core 新規実装 ✅ 完了（2026-06-08）
- POCO：`UnitDefinition` / `WazaDefinition`（StatusEffectData / AuraEffectData は不要と判明・既存 `StatusEffect` を `[Serializable]` 化＋auto-property → public field 化で対応）
- SO ラッパー：`UnitDefinitionSO` / `WazaDefinitionSO`
- 新規 asmdef：`ProjectCitadel.Data`（noEngineReferences=false・Core 純度温存のため分離）
- Registry：`DamageFormulaRegistry`（標準セット5種）/ `TriggerConditionRegistry`（標準セット2種）
- Catalog：`UnitCatalog` / `WazaCatalog`
- 新規 Editor テスト：Registry 単体テスト（25ケース・Damage 13 + Trigger 12）
- `Unit` クラス拡張：`PlacementHint` / `CombatRoles` / `PrimaryAttackKind` / `AbilityLabels` 追加、`Roles` → `TargetTags` リネーム
- **コミット**：`2e023f9`（POCO+Registry）/ `680873e` / `db21c7e`（テスト修正）/ `6b364da`（SO ラッパー）/ `989add4`（Catalog＋Unit 拡張）/ `321e556`（.meta フォロー）

#### Step 0-3：データ移行 ✅ 完了（2026-06-08）
- **Editor 専用コンバータで一括生成方針**（当初の手作業案を変更）
  - `Assets/Scripts/Data/Editor/Stage3RosterToSoConverter.cs`
  - `[MenuItem]` ProjectCitadel/Data/Stage3Roster を SO に変換
  - Stage3Roster.X() を呼んで Unit インスタンス生成 → POCO に変換 → SO アセットを `AssetDatabase.CreateAsset`
  - 冪等：既存 SO は SerializedObject 経由で上書き再書き換え
- 生成結果：**Unit 20 体 / Waza 21 個**（杖打ち3個除外）
  - 配置：`Assets/Resources/Data/Units/unit_*.asset` / `Assets/Resources/Data/Wazas/waza_*.asset`
- **攻撃しない化（論点6・2026-06-08 確定）反映**：
  - 司祭・巫女・踊り子は杖打ち Waza（heal_staff/med_staff/buf_staff）を除外
  - 3兵種に NoNormalAttack タグ自動付与
- **シート由来情報の保持方法**：
  - `UnitMetadataMap`（コンバータ内）に PlacementHint / CombatRoles / PrimaryAttackKind / AbilityLabels を明示記述
  - `WazaFormulaMap`（コンバータ内）に DamageFormulaId / Params / TriggerConditionId / Params を明示記述（Stage3Roster のクロージャからは抽出不可）
- **コミット**：`c848cbb`（コンバータ実装）/ `a828c18`（StatusEffect auto-property → field 修正）/ `7fd7dff`（SO アセット41個＋Editor .meta）

#### Step 0-A：500_architecture.md 全面再設計＋900 §8 ドキュメント執筆規約追加 ✅ 完了

- 500_architecture.md を 4 層アーキテクチャ（Domain / Data / UseCase / Presentation）ベースに全面再設計
- §3 「仕様要素 → 実装観点マッピング表」を骨格として導入（[100_game_design.md](100_game_design.md) / [110_combat_spec.md](110_combat_spec.md) 各要素を実装観点と対応付け）
- 900_development_rules.md §8 にドキュメント執筆規約を追加（責務・抽象・判断のみ書く／経緯ログ禁止／二重ドリフト防止）
- **コミット**：`71525d6`

#### Step 0-B-1：asmdef Echolos.* 化＋ディレクトリ刷新＋namespace 一括変換 ✅ 完了

- asmdef 4 個 rename：`ProjectCitadel.{Core, Data, Data.Editor, Tests.Editor.Core}` → `Echolos.{Domain, Data, Data.Editor, Tests.Domain}`
- ディレクトリ 3 個 rename：`Scripts/Core` → `Scripts/Domain` / `Scripts/UnityView` → `Scripts/Presentation` / `Tests/Editor/Core` → `Tests/Editor/Domain`
- namespace 一括変換 115 ファイル：`ProjectCitadel.*` → `Echolos.*`
- Unity Editor メニューパス更新（`CreateAssetMenu` / `MenuItem`）
- **コミット**：`6426034`

#### Step 0-B-2：IUnitCatalog / IWazaCatalog 抽象化＋ UnitCatalog / WazaCatalog 実装化 ✅ 完了

- `Echolos.Domain.Catalog.IUnitCatalog` / `IWazaCatalog` を抽象として定義
- 既存 `UnitCatalog` / `WazaCatalog` を sealed instance class 化（IUnitCatalog / IWazaCatalog 実装）
- UnitCatalog はコンストラクタで IWazaCatalog を受け取る（DI 注入準備）
- 既存呼び出し側ゼロ（事前 grep 確認）のため Bootstrap 配線は 0-B-3 で実施
- **コミット**：`f918c0f`

#### Step 0-B-3：UseCase / Presentation asmdef 新設＋ VSプロト Core 引き上げ＋ DI 配線 ✅ 完了

5 サブコミットで段階実施（2026-06-07）。

- **0-B-3a**：`Echolos.UseCase` / `Echolos.Presentation` asmdef 2 個新設 — コミット `7d8337a`
- **0-B-3b**：Unity 自動生成 .meta 3 個取り込み（asmdef 2 個＋ UseCase ディレクトリ）— コミット `bcbb63a`
- **0-B-3c**：VSプロト Core 15 ファイルを `Echolos.UseCase.VSPrototype` に引き上げ＋ namespace 変換＋参照側 18 ファイル using 追従＋ `Echolos.Tests.Domain.asmdef` に `Echolos.UseCase` 参照追加 — コミット `df0c56e`
- **0-B-3d**：サービス 3 個（`VSPrototypeRoundManager` / `VSPrototypeEndingResolver` / `VSPrototypeEnemyPatterns`）を sealed instance class 化＋コンストラクタ DI＋ Bootstrap で配線＋ MapGUI を `Bootstrap.RoundManager` 経由に追従＋テスト 3 ファイル SetUp 追従 — コミット `4c1226a`
- **0-B-3d フォロー**：namespace 親子関係が切れて `Stage3Roster` 暗黙参照不可になった 2 ファイルに `using Echolos.Domain.Prototype;` 追加＋新規ディレクトリ .meta 取り込み — コミット `9b668df`

**据え置き**（500 §6.2 のサービス対象外と解釈）：
- 純データ集約：`MetaUpgradeIds` / `MetaUnitIds` / `VSPrototypeStorySceneIds`（SO 主キーとして const のまま維持）
- 純関数ヘルパ：`MetaProgressSerializer`（純関数のまま維持・呼び出しは MetaProgressStore 経由）

#### Step 0-B-4：Stage3 共通資産 14 クラスを中立名にリネーム＋移動 ✅ 完了

- [500_architecture.md §13.2](500_architecture.md) 対応表に従い、14 クラスを中立名にリネーム
- Domain.Battle.Replay：BattleRunner / BattleEvent / BattleResolver / BattleReport（同居クラス 3 個を独立 .cs 化）
- Domain.Story：StoryProgress / StoryPage / StoryStage（同居クラス 2 個を独立 .cs 化）
- Presentation.Common：GuiTheme / IconRegistry / GuideContent / StoryContent
- Presentation.Battle：UnitBadgeOverlay / StatusEffectOverlay
- Presentation.Story：StoryOverlay
- 戦闘プロト純粋資産（CharacterRoster〈旧 Stage3Roster〉/ Stage3RoundManager 等の現役利用組 / Stage3CampaignGUI 等）は Phase 0-B-4 当時は Stage3 プレフィックス残置（Phase 0-B-7 で _old/ 退避＋ Phase 0 完了後の Stage3 整理で CharacterRoster にリネーム）
- **コミット**：`c023a0a`

#### Step 0-B-5：UnitDefinition / WazaDefinition を Data 配下に移動＋ Formula Registry を Domain.Formula 配下に移動 ✅ 完了

- 0-B-5a：UnitDefinition / WazaDefinition / FormulaParam を `Echolos.Data.Definitions` に移動
  - **コミット**：`9036649`
- 0-B-5b：DamageFormulaRegistry / TriggerConditionRegistry を `Echolos.Domain.Formula` に移動
  - 暫定 namespace `Echolos.Domain.Data` を完全消滅させた
  - **コミット**：`18c7a27`

#### Step 0-B-6：Waza / RuntimeWaza 改修＋ BattleManager 追従 ✅ 完了

**0-B-6a：RuntimeWaza 分離**（コミット `1509d23` / フォロー `36eb454`）
- 新規 `Echolos.Domain.Skills.RuntimeWaza`（sealed instance・BaseWaza 参照＋ CurrentCooldown / CurrentUses ＋ Magnitude 強化適用済 AppliedEffects）
- `Waza` から `CurrentCooldown` / `CurrentUses` / `CreateBattleCopy()` 削除
- `RuntimeUnit.BattleWazas` を `List<Waza>` → `List<RuntimeWaza>`
- `ActionDeclaration.DeclaredWaza` を `Waza` → `RuntimeWaza`
- `BattleManager.InitializeBattle` で RuntimeWaza 生成（§10.10 Magnitude 強化吸収を RuntimeWaza コンストラクタに統合）
- `BattleManager.ExecuteEndPhase` / `ExecuteMainPhase` / `ComputeBaseDamage` の Waza 参照を RuntimeWaza 経由に
- `TargetEvaluator` / `ActionExecutor` 全関数のシグネチャ追従
- `TargetEvaluator.CreateNormalAttack` は内部で Waza 生成 → RuntimeWaza ラップで返却
- `TargetEvaluator.SelfGuardWaza` は static 共有 Waza のまま、呼び出し時に毎回 `new RuntimeWaza` でラップして CD 共有を回避
- テスト追従 6 ファイル（Step2 / Step3 / Step4 / Stage3Assassin / Prototype_Support*）

**0-B-6b：FormulaId 経路保証**（コミット `445161d`）
- 新規 `VSProto_WazaCatalogIntegrationTests`：SO 由来 Waza 21 個が `WazaCatalog.BuildWaza` 経由で DamageFormulaRegistry / TriggerConditionRegistry を通り、RuntimeWaza でラップして CalculateBaseDamage / CalculateHealAmount まで貫通することを保証
- `Echolos.Tests.Domain.asmdef` に `Echolos.Data` 参照追加

**Func 撤去について**：
`Waza` クラスの 3 つの `Func`（CalculateBaseDamage / CalculateHealAmount / TargetingCondition）は当面残置。SO 由来 Waza は WazaCatalog.BuildWaza で Registry → クロージャに詰めて流す形で FormulaId ルートが既に機能している。Stage3Roster の Func 直接構築は Phase 0-B-9 SO 化第二弾で消滅させる（Waza に DamageFormulaId フィールドを追加する案は二重抽象化を生むため見送り）。

#### Step 0-B-7：戦闘プロト純粋資産・テスト・シーン `_old/` 退避 ✅ 完了

[500_architecture.md §13.1](500_architecture.md) に従い、VSプロトで再利用しない戦闘プロト資産を `_old/` 配下に物理移動し、専用 asmdef + `defineConstraints: ["ECHOLOS_OLD_PROTO"]` でコンパイル対象外化した。

**退避対象（合計 15 ファイル）**：

| 退避先 | ファイル |
|---|---|
| `Assets/Scripts/Domain/Prototype/_old/`（7 個） | BattleFrontResolver / CampaignManager / CampaignModels / PrototypeCampaign / PrototypeRoster / Stage3EnemyPatterns / Stage3RoundManager |
| `Assets/Scripts/Presentation/_old/`（2 個） | Stage3CampaignGUI / DebugBattleStarter |
| `Assets/Tests/Editor/Domain/_old/`（5 個） | Prototype_BattleFrontResolverTests / Prototype_CampaignTests / Stage3EnemyPatternsTests / Stage3RoundManagerTests / Stage3PrincessTests |
| `Assets/Scenes/_old/`（1 個） | EcholosProto_Main.unity |

**新規 asmdef 3 個**：
- `Echolos.Domain.OldPrototype`（noEngineReferences=true）
- `Echolos.Presentation.Old`
- `Echolos.Tests.Domain.Old`

すべて `defineConstraints=["ECHOLOS_OLD_PROTO"]` ＋ `autoReferenced=false`。

**退避対象外**（VSプロトが流用するため Domain/Prototype/ に残置）：
- CharacterRoster〈旧 Stage3Roster〉のみ Domain.Prototype に残置。Stage3DraftService / Stage3InteriorService / Stage3CampaignModels は Phase 0 完了後の Stage3 整理で `_old/` 退避済
- 上記に依存するテスト 7 個（Stage3AssassinTests / Stage3BattleEventTests / Stage3CampaignStateTests / Stage3DraftServiceTests / Stage3InteriorServiceTests / Stage3MatchupTests / Stage3StoryProgressTests）
- Debug シーン用 Presentation（Stage3BattleSpectatorView / Stage3BattleSpectatorSandboxGUI / Stage3SandboxGUI）

**Phase 0-B-8 で対応**：姫騎士関連の VSプロト 検証は、拠点Lv連動強化を撤廃した形で VSプロト 用テストとして新規作成する（Stage3PrincessTests は拠点Lv連動が大半を占めていたため退避）。

**ユーザー側 Unity Editor 追加作業**：
- File > Build Settings から EcholosProto_Main を削除（Build 対象外化）
- 残るシーンは EcholosProto_VS / Debug_BattleLogSandbox / Debug_BattleSpectatorSandbox の 3 つ

**コミット**：`89b5f76`（Domain/_old + Tests/_old）→ `c22287b`（Presentation/_old）→ `bdf46dd`（Scenes/_old）→ 退避漏れ追従（DebugBattleStarter / Stage3PrincessTests）

#### Step 0-B-8：既存テスト追従＋ Catalog 統合テスト＋姫騎士／ブリジット VSプロト 用テスト ✅ 完了

Step1〜5 / Stage3* 残置 / VSProto_* の既存テスト追従は Phase 0-B-6a で完了済。本ステップでは未着手分（UnitCatalog 統合テスト・姫騎士／ブリジット VSプロト 用テスト）を追加した。

**新規追加テスト 3 ファイル**：

| ファイル | 内容 |
|---|---|
| `VSProto_UnitCatalogIntegrationTests` | Resources/Data/Units 配下 SO 全件ロード・DI null チェック・BaseWazas が WazaCatalog 経由で構築されること・AuraEffect / Tags の deep copy 保証（Get するたびに別インスタンス・テンプレ汚染なし） |
| `VSProto_PrincessTests` | s3_princess SO 由来データ検証（HP170 / ATK28 / 聖剣の一閃 / DefenseUp 永続 Magnitude=5・RuntimeWaza ラップ＋ CalculateBaseDamage 経路） |
| `VSProto_BridgetTests` | bridget SO 由来データ検証（HP150 / ATK26 / 二刀の祈り / AttackUp 永続 Magnitude=3・拠点Lv連動なし・RuntimeWaza ラップ＋ CalculateBaseDamage 経路） |

**0-B-8 で発見した実装フォロー候補**：
`VSPrototypeInteriorService.CanUpgradeUnitType` に姫騎士・ブリジット（固有ユニット）を兵種強化対象外として弾くロジックが無い。Stage3InteriorService にはあった（`unitTypeId == Stage3Roster.PrincessId → false`）。VSプロトで「固有ユニット = 兵種強化対象外」原則を実装に反映するかは、Phase 1 プレイ感向上時に判断する。

**仕様書修正なし**（500 §4.1 / §5・310 §1.x で本テストの観点は既に記述済のため）。

**0-B-8d：カバレッジ棚卸し後の補完**：Explore subagent による全 18 個 VSProto_*テスト棚卸しの結果、優先度「中」2 件・「低」5 件の漏れが発見された。優先度低の大半は Phase 0-B-9 SO 化で自動検証される予定。実装した補完：

| 追加対象 | 内容 |
|---|---|
| `VSProto_EnemyPatternsTests`（新規・12 ケース） | マス種別別敵編成 5 パターンの戻り値内容・CreateForNode ディスパッチ・Home で例外・呼ぶたびに別 RuntimeUnit インスタンス |
| `VSProto_MapNodeTests` 追加 3 ケース | ClearAllies の発火順序（配置順）・空状態時の非発火・イベント発火時の再入安全（スナップショット取得後の内部 Clear） |
| `VSProto_RunFlowTests` 追加 1 ケース | 本拠地連続防衛 3 回中 2 回目で敗北 → 残り戦闘スキップ＋ HomeBattleReports 2 件で打ち切り |

#### Step 0-B-9：データ駆動化第二弾（残債タスク・段階実施中）

500 §1.4 データ駆動原則の対象として、VSプロト Phase 0-B 完了後も C# 直書きで残る 5 項目を SO 化する。サブステップごとに独立コミットで段階実施。

| サブステップ | 項目 | 改修内容 | 状態 |
|---|---|---|---|
| 0-B-9a | `MetaRewardCalculator` | `DamageFormulaRegistry` 同パターン（式 ID ＋パラメタ）＋ Domain 完成品 `MetaRewardFormula` ＋ `IMetaRewardFormulaCatalog` | ✅ 完了 |
| 0-B-9b | メタ強化定義 | `MetaUpgradeDefinitionSO` で一元化＋ Domain 完成品 `MetaUpgrade` ＋ `IMetaUpgradeCatalog` | ✅ 完了 |
| 0-B-9c | `VSPrototypeDraftPool` | `DraftPoolDefinitionSO`（兵種 ID リスト＋確率）に分離＋ Domain 完成品 `DraftPool` ＋ `IDraftPoolCatalog` ＋ DraftService に `IUnitCatalog` 追加注入 | ✅ 完了 |
| 0-B-9d | `VSPrototypeStoryContent` | `StorySceneDefinitionSO` / `StoryPageDefinition` POCO に分離＋ Domain 完成品 `StoryScene` ＋ `IStorySceneCatalog`（多言語化キー化は Phase 2 以降） | ✅ 完了 |
| 0-B-9e | `MetaProgressStore` 永続化 | `ISaveStore` 抽象＋ `PlayerPrefsSaveStore` 実装＋ MetaProgressStore を sealed instance class 化（Serializer 本体は据置） | ✅ 完了 |

**対象外**：`MetaUpgradeIds` / `MetaUnitIds` の `const string`（SO の主キー識別子なので const のまま残す）。

#### Step 0-B-9a：MetaReward 報酬式 SO 化 ✅ 完了

旧 `MetaRewardCalculator.Calculate`（const + ハードコード分岐）を「式 ID + パラメタ」パターンに置換した。

**5 層構成（UnitDefinition → UnitCatalog → Unit と同パターン）**：

| 層 | クラス | 役割 |
|---|---|---|
| Domain.Formula | `MetaRewardContext` | 計算入力 struct |
| Domain.Formula | `MetaRewardFormulaRegistry` | 式 ID → 実装 delegate。標準 `vsproto_standard_v1` 1 件登録 |
| Domain.Formula | `MetaRewardFormula` | Domain 完成品（Id / FormulaId / Params / `Calculate(ctx)`） |
| Domain.Catalog | `IMetaRewardFormulaCatalog` | 抽象（Get / IsRegistered / GetAll → `MetaRewardFormula`） |
| Data.Definitions | `MetaRewardFormulaDefinition` | POCO（SO シリアライズ可能） |
| Data | `MetaRewardFormulaSO` | SO ラッパー |
| Data | `MetaRewardFormulaCatalog : IMetaRewardFormulaCatalog` | Resources/Data/MetaReward LoadAll 実装＋ POCO → Domain 変換 |

旧 `MetaRewardCalculator` は完全削除（呼び出しは `VSPrototypeRoundManager.FinishRun` の 1 箇所のみ、Catalog 注入で代替）。Domain → Data の逆依存を避けるため、Catalog は Domain 型 `MetaRewardFormula` を返し、POCO `MetaRewardFormulaDefinition` は Catalog 実装内部だけが知る。

**SO アセット**：`Assets/Resources/Data/MetaReward/meta_reward_formula_vsproto_standard.asset`（perRound=10 / reachedBoss=50 / bridgetRescue=100 / bossDefeated=200 / trueEnd=150）。Editor メニュー `Echolos/Data/SO アセットを生成` で生成（[SoAssetGenerator.cs](../Assets/Scripts/Data/Editor/SoAssetGenerator.cs)・冪等再生成可）。

**テスト**：
| ファイル | 内容 |
|---|---|
| `VSProto_MetaRewardFormulaRegistryTests`（新規） | 旧 const 値による獲得量検証を Params 経路で再現＋ Registry API（Get/IsRegistered/GetAllIds）＋パラメタ欠損時の既定値フォールバック |
| `VSProto_MetaRewardFormulaCatalogTests`（新規） | SO ロード件数・SO ↔ Registry 解決可能・パラメタ仕様準拠・end-to-end 計算 |
| `_Helpers/TestStubs.cs`（新規） | `StubMetaRewardFormulaCatalog`（仕様準拠の固定パラメタ）。RoundManager 系テストの SetUp で本物 Catalog（Resources 依存）の代わりに注入 |
| `VSProto_RunFlowTests` / `VSProto_RoundManagerTests` | SetUp に Stub 注入で追従 |
| `VSProto_MetaRewardCalculatorTests` | 削除（Calculator 廃止のため） |

**ユーザー側 Unity Editor 追加作業**：
- 初回コンパイル後、Editor メニュー `Echolos > Data > SO アセットを生成` を実行（SO アセット 1 個生成）
- Test Runner で All Tests 実行（旧 MetaRewardCalculator 系テストが消えて、Registry / Catalog の新規テストがグリーンになる想定）

#### Step 0-B-9b：メタ強化定義 SO 化 ✅ 完了

旧 `MetaProgressState.CapPrincessAtk` / `CapActionPoints` / `CapInitialUnit` const 3 個＋ `MetaHubGUI.UpgradeOption[]` 3 行のハードコードを SO に一元化。

**5 層構成**：

| 層 | クラス | 役割 |
|---|---|---|
| Domain.Meta（新規 ns） | `MetaUpgrade` | Domain 完成品（Id / DisplayName / EffectText / Cost / Cap） |
| Domain.Catalog | `IMetaUpgradeCatalog` | 抽象（Get / IsRegistered / GetAll → MetaUpgrade） |
| Data.Definitions | `MetaUpgradeDefinition` | POCO |
| Data | `MetaUpgradeDefinitionSO` | SO ラッパー |
| Data | `MetaUpgradeCatalog : IMetaUpgradeCatalog` | Resources/Data/MetaUpgrades LoadAll 実装 |

**改造**：
- `MetaProgressState`：const `Cap*` 3 個削除（`ApplyUpgrade(id, cap)` の cap 引数は維持・呼び出し側が SO 値を渡す）
- `MetaHubGUI`：`UpgradeOption[]` 撤去・`_bootstrap.MetaUpgradeCatalog.GetAll()` で描画
- `VSPrototypeBootstrap`：`MetaUpgradeCatalog` インスタンス化＋ public プロパティ `MetaUpgradeCatalog` 公開

**SO アセット**：`Assets/Resources/Data/MetaUpgrades/meta_upgrade_princess_atk.asset` / `meta_upgrade_action_points.asset` / `meta_upgrade_initial_unit.asset`（仕様 §3.2 のコスト・上限）。`Echolos/Data/SO アセットを生成` メニューで一括再生成（既存アセット上書き）。

**テスト**：
| ファイル | 内容 |
|---|---|
| `VSProto_MetaUpgradeCatalogTests`（新規） | 本物 Catalog で SO 3 件ロード・Id 一致・コスト/上限の仕様準拠・end-to-end で MetaProgressState.ApplyUpgrade 経路 |
| `_Helpers/TestStubs.cs` | `StubMetaUpgradeCatalog` 追加（仕様値固定の 3 件） |
| `VSProto_MetaProgressStateTests` | `MetaProgressState.CapPrincessAtk` 等の参照を当該テストファイル内 const に置換（cap は引数経由で受ける構造を維持） |

**ユーザー側 Unity Editor 追加作業**：
- メニュー `Echolos > Data > SO アセットを生成` 再実行（メタ強化 SO 3 個が追加生成される）
- Test Runner で All Tests 実行

#### Step 0-B-9c：DraftPool SO 化 ✅ 完了

旧 `VSPrototypeDraftPool`（`static readonly Func<Unit>[] Normal/Rare` ＋ const）を **完全廃止** し、SO 由来の `DraftPool`（Domain）に置換。VSプロトで初めて `IUnitCatalog` を Bootstrap に配線（DraftService が Unit 生成に使用）。

**5 層構成**：

| 層 | クラス | 役割 |
|---|---|---|
| Domain.Draft（新規 ns） | `DraftPool` | Domain 完成品（Id / NormalUnitIds / RareUnitIds / RareProbability / CandidatesPerOffer） |
| Domain.Catalog | `IDraftPoolCatalog` | 抽象（Get / IsRegistered / GetAll → DraftPool） |
| Data.Definitions | `DraftPoolDefinition` | POCO |
| Data | `DraftPoolDefinitionSO` | SO ラッパー |
| Data | `DraftPoolCatalog : IDraftPoolCatalog` | Resources/Data/DraftPools LoadAll 実装 |

**改造**：
- `VSPrototypeDraftPool` **完全削除**（呼び出しは DraftService のみだった）
- `VSPrototypeDraftService`：コンストラクタに `IDraftPoolCatalog` ＋ `IUnitCatalog` 追加注入。`Pick3From(IReadOnlyList<string> unitIds, int candidatesPerOffer, bool isRare)` シグネチャに変更し `_unitCatalog.Get(unitId)` で Unit 生成
- `VSPrototypeBootstrap`：`WazaCatalog` / `UnitCatalog` / `DraftPoolCatalog` 新規インスタンス化＋ DraftService に注入（VSプロトで初めて UnitCatalog が Composition Root に登場）

**SO アセット**：`Assets/Resources/Data/DraftPools/draft_pool_vsproto_standard.asset`（通常 12 体・レア 3 体・RareProbability=0.15・CandidatesPerOffer=3）。`Echolos/Data/SO アセットを生成` メニューで一括再生成。

**テスト**：
| ファイル | 内容 |
|---|---|
| `VSProto_DraftPoolCatalogTests`（新規） | 本物 Catalog で SO 1 件ロード・Normal12/Rare3 構成・抽選パラメタ・全 UnitIds が UnitCatalog で解決可能（SO ↔ UnitCatalog 整合性） |
| `_Helpers/TestStubs.cs` | `StubDraftPoolCatalog` 追加（公開定数 `NormalIds` / `RareIds` で DraftService テストから参照） |
| `VSProto_DraftServiceTests` | SetUp で StubDraftPoolCatalog + 本物 UnitCatalog 注入、`VSPrototypeDraftPool.Normal` 参照を `StubDraftPoolCatalog.NormalIds` に置換 |

**ユーザー側 Unity Editor 追加作業**：
- メニュー `Echolos > Data > SO アセットを生成` 再実行（DraftPool SO 1 個追加生成）
- Test Runner で All Tests 実行

#### Step 0-B-9d：StoryContent SO 化 ✅ 完了

旧 `VSPrototypeStoryContent`（5 メソッド × 平均 3 ページ = 14 ページの `new StoryPage(...)` 直書き＋画像パス const 7 個＋ボタン文言 2 個）を**完全廃止**し、`StorySceneDefinitionSO` 由来の Domain 完成品 `StoryScene` に置換。`StoryPage` は既存 Domain クラス（get-only プロパティ）なので、SO シリアライズ用に `StoryPageDefinition` POCO を中間層として新設。

**6 層構成（StoryPage が既存 Domain にあるため StoryScene が完成品）**：

| 層 | クラス | 役割 |
|---|---|---|
| Domain.Story（既存 ns） | `StoryScene` | Domain 完成品（Id / IReadOnlyList<StoryPage>） |
| Domain.Story（既存） | `StoryPage` | 既存・触らず |
| Domain.Catalog | `IStorySceneCatalog` | 抽象（Get / IsRegistered / GetAll → StoryScene） |
| Data.Definitions | `StoryPageDefinition` / `StorySceneDefinition` | POCO（SO シリアライズ用・StoryPage の get-only を public field 化） |
| Data | `StorySceneDefinitionSO` | SO ラッパー |
| Data | `StorySceneCatalog : IStorySceneCatalog` | Resources/Data/StoryScenes LoadAll 実装＋ POCO → Domain 変換（StoryPage 再構築） |
| UseCase.VSPrototype | `VSPrototypeStorySceneIds` | const string 5 個（MetaUpgradeIds と同パターン） |

**改造**：
- `StoryProgress.Initialize` シグネチャを `IList<StoryPage>` → `IReadOnlyList<StoryPage>` に変更（既存テストは `List<StoryPage>` 渡しのため共変性で影響なし）
- `VSPrototypeBootstrap`：`StorySceneCatalog` 配線＋ `AdvanceAfterRound` / `ResolveEndingPages` を `_storySceneCatalog.Get(sceneId).Pages` 経由に。`ResolveEndingPages` は static → instance メソッド化
- `VSPrototypeStoryGUI`：旧 `VSPrototypeStoryContent.ButtonNext` / `ButtonSkip` 参照を自身の `private const` に置換（UI 文言なので Presentation 層が持つ）

**削除**：
- `VSPrototypeStoryContent` **完全廃止**（5 メソッド・const 7 個・ボタン文言 2 個）
- `VSProto_StoryContentTests` 廃止

**SO アセット**：5 個（story_scene_bridget_bond / bridget_confront / ending_defeat / ending_bitter / ending_true）。`Echolos/Data/SO アセットを生成` で一括再生成。

**テスト**：
| ファイル | 内容 |
|---|---|
| `VSProto_StorySceneCatalogTests`（新規・17 ケース） | 本物 Catalog で SO 5 件ロード・5 シーンページ件数（2/2/4/3/3）・「ありがとう」「兄さま」「To be continued」等の重要文言検証 |
| `_Helpers/TestStubs.cs` | `StubStorySceneCatalog` 追加（5 シーン軽量版） |

**ユーザー側 Unity Editor 追加作業**：
- メニュー `Echolos > Data > SO アセットを生成` 再実行（StoryScene SO 5 個追加生成）
- Test Runner で All Tests 実行
- 実機でラン通し（特に BridgetEvent / 3 種類のエンディング演出）をスクショ確認

#### Step 0-B-9e：MetaProgressStore の ISaveStore 抽象化 ✅ 完了

旧 `static class MetaProgressStore`（PlayerPrefs 直接呼び出し）を `sealed instance class` ＋ `ISaveStore` 注入版に置換。Phase 1 のセーブ要件（500 §3.7：複数スロット・チェックサム・マイグレーション・Steam Cloud）に向けた下地を整備。

**3 層構成**：

| 層 | クラス | 役割 |
|---|---|---|
| Domain.Save（新規 ns） | `ISaveStore` | 抽象（Load / Save / Has / Delete の文字列 KVS） |
| Data | `PlayerPrefsSaveStore : ISaveStore` | PlayerPrefs 実装（VSプロト範囲） |
| UseCase.VSPrototype | `MetaProgressStore` | sealed instance class・ISaveStore 注入＋ MetaProgressSerializer 経由 |

**改造／移動**：
- `MetaProgressStore`：`Presentation.VSPrototype.MetaProgressStore` → `UseCase.VSPrototype.MetaProgressStore`（UnityEngine 依存が ISaveStore 実装側に隔離された結果、UseCase 層配置が可能に）。`static class` → `sealed class` 化、コンストラクタで ISaveStore 受け取り、namespace 変更（履歴は git mv で保持）
- `VSPrototypeBootstrap`：`PlayerPrefsSaveStore` インスタンス化＋ `MetaProgressStore(_saveStore)` 配線。`MetaProgressStore.Load()` 等の static 呼び出し 4 箇所を `_metaProgressStore.Load()` インスタンス呼び出しに追従

**据え置き**（スコープ外・Phase 1 で対応）：
- `MetaProgressSerializer`（手書き JSON パーサー）：現状動作問題なし。Newtonsoft.Json 等への置換は Phase 1 のセーブ要件と合わせて判断（900 §7.7 外部ライブラリ事前承認の手順を踏む）
- `SaveSchema` / マイグレーション / 複数スロット / オートセーブ：Phase 1 のスコープ

**テスト**：
| ファイル | 内容 |
|---|---|
| `VSProto_SaveStoreTests`（新規・9 ケース） | StubSaveStore で Load / Save / Has / Delete / 上書き / null / 複数キー独立性の契約検証 |
| `VSProto_MetaProgressStoreTests`（新規・10 ケース） | コンストラクタ null チェック・未保存 Load 初期状態・全フィールド往復・PrefsKey 仕様準拠・DeleteAll・不正 JSON フォールバック |
| `_Helpers/TestStubs.cs` | `StubSaveStore`（Dictionary<string, string> ベース）追加 |

**ユーザー側 Unity Editor 追加作業**：
- Test Runner で All Tests 実行（新規 SaveStore / MetaProgressStore テスト計 19 ケースがグリーン想定）
- 実機ラン通し（メタ拠点 → ラン → エンディング → メタ拠点復帰）で永続化動作を確認

### Phase 0 完了宣言

Phase 0-A・0-B-1〜0-B-8d・0-B-9a〜0-B-9e すべて完了。Phase 0 完了基準（[#Phase-0-完了基準](#phase-0-完了基準)）達成。

### Phase 0 完了後・Phase 1 着手前：Stage3 プレフィックス整理 ✅ 完了

Phase 0-B-4 で残置されていた「VSプロトが流用するため Stage3 プレフィックスを残した資産」を 4 ステップで整理。現役コード（`_old/` 外）から Stage3 プレフィックスはゼロに。

| Step | 内容 | コミット |
|---|---|---|
| 1 | `Stage3RosterToSoConverter` を完全削除（Phase 0-B-9 で役目終了） | `2212ecb` |
| 2 | C グループ（Stage3CampaignModels / Stage3DraftService / Stage3InteriorService ＋テスト 3）を `_old/` 退避 | `121401b` |
| 3 | B グループ（Stage3BattleSpectatorView / SandboxGUI 系 3 .cs ＋ Debug シーン 2 .unity）を `_old/` 退避 | `80d6628` |
| 4 | `Stage3Roster → CharacterRoster` リネーム＋テスト 4 個（Stage3Assassin/BattleEvent/Matchup/StoryProgress → 中立名）名前中立化 | `aafd1c0` |

**ユーザー側 Unity Editor 対応**：
- File > Build Settings から Debug_BattleLogSandbox / Debug_BattleSpectatorSandbox の 2 シーンを削除（残りは EcholosProto_VS のみ）
- 旧「Echolos/Data/Stage3Roster を SO に変換」メニューは消滅（SO 再生成は「Echolos/Data/SO アセットを生成」に統一）

### Phase 0 完了基準

- VSプロト 1 ラン通しが動作する（Hub → InitialDraft → InteriorAction → Run → 戦闘 → エンディング）
- 全 SO アセットがロード可能（20 兵種＋関連 Waza）
- 既存 VSプロト Editor テストがグリーン
- 新規 Registry / Catalog テストがグリーン
- 戦闘プロト純粋資産は `_old/` に退避済（壊れた状態を許容）

---

### Step 5-0：データモデル追加（VSプロト土台） — 重さ：**中**

> 後続全ステップの前提となるモデル変更を先行投入する。
> 永続化（PlayerPrefs シリアライズ）は Step 5-2 とまとめて実装する。

**追加クラス（Core/Prototype/VSPrototype/ 配下）**
- `MapNode` + `MapNodeKind`（マス1つ・種別・列／層・配置スロット）
- `VSPrototypeMapState`（9マス＋本拠地のグラフ状態・ブリジット救出フラグ）
- `MetaProgressState`（メタ通貨・周回数・解禁ユニット・トゥルーエンド到達フラグ。シリアライザは Step 5-2 で別途）
- `VSPrototypeEndingKind` + `VSPrototypeEndingResolver`（3分岐エンディングのロジック関数）
- `VSPrototypeRoundManager`（判定ロジック関数群：陥落数・配置可否・救出デッドライン・R7判定。実際のラウンド進行・敵自動侵攻は Step 5-1 で UI と統合）

**Editor テスト追加（Assets/Tests/Editor/Core/ 配下）**
- `VSProto_MapNodeTests`：Capture/MarkFallen の Kind 制約・ResetForNewRound
- `VSProto_MapStateTests`：9マス＋本拠地構造・GetNode・列挙
- `VSProto_MetaProgressStateTests`：通貨・解禁・強化・上限・LoadFromSerializedState
- `VSProto_EndingResolverTests`：3分岐ロジック・ApplyEndingToMeta 副作用
- `VSProto_RoundManagerTests`：陥落数・配置可否（攻略順序依存）・救出デッドライン・R7判定

**完了基準**：純 Core テストがグリーン。UI 変更なし。

---

### Step 5-1：領地マップ画面＋戦闘画面の見栄え改善 — 重さ：**大**

3サブステップに分解：
- **5-1a**（重さ：中）戦略マップ GUI 最小実装 ✅ 完了（2026-06-07・コミット `65060fc`）
- **5-1b**（重さ：中）戦闘画面の見栄え改善（戦場背景＋透過アイコン）
- **5-1c**（重さ：小）マップ見栄え強化（背景＋拠点アイコン）

> **実装順序の方針**（2026-06-07 ユーザー判断）：5-1b と 5-1c は **全機能が動いてから最後に着手** する。
> 着手順序は **5-2 → 5-3 → 5-4 → 5-5 → 5-6 → 5-7 → 5-1b → 5-1c**。
> 理由：見栄え強化はアセット待ちでブロックされやすく、機能が動かない状態で先に磨いてもピッチ訴求にならない。
> 5-7 統合・バランスは矩形でも内部プレイテストで判断可能。

---

#### Step 5-1a：戦略マップ GUI 最小実装 ✅ 完了

**実装内容**（コミット `65060fc` 時点）
- 新規 `VSPrototypeMapGUI.cs` 一式（OnGUI・案 B 3列分割レイアウト）
- 9マス＋本拠地の矩形＋進攻ルート線描画
- 色分け（自領=青／敵領=橙／敵拠点=赤／バルドゥイン=金枠／本拠地=紫）
- 手駒選択 → マスクリックで配置 UI（攻略順序依存判定込み）
- 配置可能マスのハイライト
- 戦況サマリ（陥落自領数・バルドゥイン制圧状況・配置中ユニット数）
- リセットボタン

**完了基準達成**
- プレイヤーが「どこを救えば／どこを諦めるか」のジレンマを視覚的に体感できる ✅
- バルドゥイン拠点が左上にあることが画面1枚で伝わる ✅

---

#### Step 5-1b：戦闘画面の見栄え改善 — 重さ：**中** ✅ コード完了（2026-06-08・アセット待ち）

[310 §1.10 ビジュアル品質方針](310_vsprototype_spec.md) 準拠。**コード先行実装方針**（2026-06-08 ユーザー判断）：アセット供給を待たずに「ファイルがあれば反映／無ければ従来描画」のフォールバック実装でコード側を完成させ、ユーザーアセット作成と並行進行できる状態にする。

**実装内容**
- 新規 [BackgroundRegistry](../Assets/Scripts/Presentation/Common/BackgroundRegistry.cs)（Presentation.Common）：`Resources/Images/VSPrototype/{key}.png` 遅延ロード＋ミッシングキャッシュ＋ `TryDrawCover`（ScaleAndCrop）／ `TryDrawFit`（ScaleToFit＋α 指定）の 2 API
- [VSPrototypeBattleGUI](../Assets/Scripts/Presentation/VSPrototype/VSPrototypeBattleGUI.cs) 全画面背景を `TryDrawCover("battlefield_grassland")` に切替。アセット無しなら従来の `ColorBg` 単色塗りに自動フォールバック
- 透過ちびキャラは [IconRegistry](../Assets/Scripts/Presentation/Common/IconRegistry.cs) が既に `Resources/Icons/{unitId}.png` を遅延ロードしているのでファイル差替えで自動反映（追加実装不要）
- エフェクト・モーション・装飾 UI パネルはスコープ外

**必要アセット**（ユーザー作業・配置すれば次回 Play で自動反映）
- `Assets/Resources/Images/VSPrototype/battlefield_grassland.png`：戦場背景 1枚（草原系・共通・人物や軍勢は描かない）
- ちびキャラ19枚を **背景透過版** で再生成（既存 `Assets/Resources/Icons/s3_*.png` の差し替え）
- `Assets/Resources/Icons/bridget.png`：未配置の固有ユニット（unit_bridget.asset 対応）

**完了基準**
- コード：✅ 完了（コミット `4213331`）
- アセット投入後：戦闘画面が「絵を並べただけ」から「戦場に立つ」への質的転換を達成

---

#### Step 5-1c：マップ見栄え強化（背景＋拠点アイコン） — 重さ：**小** ✅ 完了（2026-06-09・磨きこみ①〜⑤＋実機試遊 OK）

5-1a の「矩形＋色＋ラベル」を「マップ風景＋拠点アイコン」に格上げする見栄え強化。

**実装内容**
- [VSPrototypeMapGUI.DrawMap](../Assets/Scripts/Presentation/VSPrototype/VSPrototypeMapGUI.cs) 冒頭で中央領域に `BackgroundRegistry.TryDrawCover("map_background")` を試行（無しなら何もせず透ける）
- `DrawMapNode` の描画順を「背景色 → 拠点アイコン → バルドゥイン金枠 → 配置可能枠 → ラベル → 配置スロット小アイコン」に変更
- 拠点アイコンマッピング：`Home→node_home` / `Friendly→node_friendly` / `EnemyTerritory→node_enemy_territory` / `EnemyStronghold＋IsBalduinStronghold→node_balduin` / `EnemyStronghold その他→node_enemy_stronghold`
- 通常時 α=0.85、陥落自領は GUI.color で彩度を落として「色あせた」見栄えに
- アセット未配置時は従来の矩形色塗りに自動フォールバック

**必要アセット**（ユーザー作業・配置すれば次回 Play で自動反映）
- `Assets/Resources/Images/VSPrototype/map_background.png`：大陸風景（草原・荒野の境界・遠景の山・3 列縦の進攻ルートが視覚的に成り立つレイアウト）
- `Assets/Resources/Images/VSPrototype/node_friendly.png`：自領＝王国の城（青系）
- `Assets/Resources/Images/VSPrototype/node_enemy_territory.png`：敵領＝侵攻軍野営（橙系）
- `Assets/Resources/Images/VSPrototype/node_enemy_stronghold.png`：敵拠点＝要塞（赤系）
- `Assets/Resources/Images/VSPrototype/node_balduin.png`：バルドゥイン拠点＝古城・籠城感（金属感）
- `Assets/Resources/Images/VSPrototype/node_home.png`：本拠地＝王宮（紫系）

**完了基準**
- コード：✅ 完了（コミット `5e4abf3`）
- アセット投入後：画面が「色塗りの記号」から「風景の上に拠点が並ぶ」見栄えに昇格

##### マップ画面磨きこみ履歴（2026-06-09）

アセット投入後の試遊フォローで以下を順次実施：

| # | 内容 | コミット |
|---|---|---|
| ① | マップ背景画像 (strategy_map.png 1254×1254) 投入＋差替版反映 | `20e77f3` / `22eb1a4` |
| ② | 拠点アイコン 5 種（node_home/friendly/enemy_territory/enemy_stronghold/balduin）投入 | `b322210` / `0e6fef5` |
| ③ | マス装飾全廃（背景色塗り／バルドゥイン金枠／配置可能ハイライト／マスラベル／配置スロット小アイコン×6）＋野営地縮小 | `0bcedb2` |
| ④ | ノード配置を画像内円位置に合わせ込み＋接続線撤廃＋ city/camp サイズ分岐 | `a89ed79` / `d9b0a68` / `ee8bb1e` |
| ⑤ | 配置済みユニット表示の再設計（案 2 ホバーツールチップ＋案 3 左ペイングルーピング） | `662da24` / `ca5d1b7` |

##### Step 5-1c フォロー⑤：配置済みユニット表示の再設計 ✅ 完了・実機試遊 OK（2026-06-09）— 重さ：**中**

**背景**：マス装飾全廃（フォロー③）で Phase 1-F 「マス内に配置スロット小アイコン×6」を撤去したため、配置済みユニットがマップ画面で見えない状態に。**案 3 主軸＋案 2 補助**で対応（2026-06-09 ユーザー判断）。

| # | 案 | 役割 | 実装 |
|---|---|---|---|
| **3** | 左ペイン王国軍リストを「配置先マス別グルーピング」に再設計 | 主軸（常時情報源・全体俯瞰） | 11 グループ固定順（未配置＋本拠地＋自領 3＋敵領 3＋敵拠点 3）。各見出しに `N/6` 配置済人数。空グループも見出しを残して配置不在マスを一目可視化。グループ内ソートは固有→Normal→Rare 継承。行表示は HP/ATK/Range/Role の簡易ステータス（配置先は見出しで識別可なので「配置：◯◯」バッジ削除） |
| **2** | 拠点ホバー時に味方配置セクション追加 | 補助（空間的確認） | 旧 `DrawEnemyTooltip` を `DrawNodeTooltip` に拡張。マス種別自動切替（敵領／敵拠点は敵編成＋味方／自領／本拠地は味方のみ）。兵種名 ×N グルーピング＋前列／後列件数サマリ。両セクションゼロのマスでは非表示 |

**没案**：案 1（マス内に簡易記号 ●●〇 を再配置）は「中央マップの雰囲気優先」方針と本質的にぶつかるため不採用。

---

### Step 5-2：メタ通貨「王国の記憶」永続化＋獲得式 — 重さ：**小** ✅ 完了（2026-06-07）

> Step 5-0 で `MetaProgressState` のロジックは完成済。本ステップでは永続化と獲得式を実装する。

**実装内容**
- `MetaProgressSerializer`（純 Core：JSON 文字列⇔State 変換・往復テスト付き）
- `MetaProgressStore`（UnityView：PlayerPrefs Wrapper・`vsproto_meta_progress` キー）
- ラン終了時の獲得量計算（経過R×10＋ボス到達 50＋救出 100＋クリア 200＋トゥルーエンド +150）
- ラン開始時に `MetaProgressStore.Load()` で復元・ラン終了時に `Save()` で永続化

**完了基準**：ラン後にメタ通貨が増減し、PlayerPrefs に保存され、次起動時にも引き継がれる

---

### Step 5-3：メタ拠点 UI（亡国の遺跡・テキスト UI） — 重さ：**小** ✅ 完了（2026-06-07）

**実装内容**
- 新規 `Phase.Hub` 追加
- 新規 `MetaHubGUI.cs`（メタ通貨残高・周回数・トゥルーエンド達成フラグ表示）
- 次ラン強化 3 項目（王女 ATK +3 / 行動力 +1 / 初期ユニット +1）の購入 UI
- ブリジット解禁状態の表示
- 「次のランへ →」ボタン
- 各エンディング完了時の遷移先を Hub に変更

**完了基準**：メタ拠点画面で次ラン強化を選択すると、PlayerPrefs に保存され、次ランに反映される

---

### Step 5-4：ラン進行ロジック＋加入システム＋トゥルーエンド分岐 — 重さ：**大** ✅ ロジック完了（2026-06-07・実機動作確認はユーザー側で別途）

> **2026-06-07 スコープ拡大**：当初の「加入システム＋トゥルーエンド分岐」のみでは加入条件・R7 ボス戦の実装が不可能と判明したため、ラン進行ロジック（敵自動侵攻・戦闘解決・自領陥落判定・本拠地連続防衛・R7 ボス戦）を統合する形にスコープを拡大。演出（ストーリーページ・Phase 追加）は Step 5-5 へ。

#### A. ラン進行ロジック

- `VSPrototypeEnemyPatterns`（新規・純Core）：マス種別ごとの仮固定敵編成
  - 自領 = 弱パターン（帝国偵察兵 ×3）
  - 敵領 = 中強パターン（サムライ ×2 + アサシン ×1）
  - 敵拠点 = 強パターン（サムライ ×3 + 大槌兵 ×1 + 弓兵 ×2）
  - R7 本拠地 = 隻眼のサムライ編成丸ごと流用（皇太子っぽい演出は Step 5-5）
- `VSPrototypeRunResult`（新規・純Core）：ラウンド・ラン結果集約値オブジェクト
- `VSPrototypeRoundManager` 拡張：
  - `StartRound(state, round)`：マスのラウンドリセット＋敵編成セット
  - `ResolveAllBattles(state, round, resolver)`：プレイヤー配置済みマス＋未配置の敵マスを処理
  - `ApplyFrontFallenJudgement`：戦闘敗北 or 敵あり×プレイヤー未配置 で自領陥落マーク
  - `ResolveHomeContinuousDefense`：陥落数だけ連続戦・1敗で即敗北
  - R7 ボス戦専用解決パス
- 戦闘解決は既存 `Stage3BattleRunner.Run` を流用

#### B. 加入システム＋トゥルーエンド分岐ロジック

- `Stage3Roster.Bridget()` 追加（ステータス・技は姫騎士類似）
- バルドゥイン拠点（左列敵拠点）攻略 → `TryMarkBridgetRescued` 自動連動
- R5 終了時にフラグ立て（救出済 / 未救出）— **演出は Step 5-5**
- ラン終了時：救出済なら `UnlockedUnits.Add("bridget")`、EndingKind=True なら `MarkTrueEndReached`
- `Bootstrap.StartNewRun` で `UnlockedUnits.Contains("bridget")` 判定 → 初期 Roster に Bridget 追加
- R7 ボス勝利時の EndingKind 確定（`VSPrototypeEndingResolver.ResolveAfterBossRound` 既存活用）
- ラン終了 → `Bootstrap.FinishRun` で `ApplyEndingToMeta` → メタ Save → Hub 戻り

#### C. MapGUI 拡張

- 「戦闘実行 →」ボタン追加
- ラウンド表示更新（R{n}/7）
- 戦闘結果サマリ表示（陥落・救出・本拠地状況）
- ラン終了で結果テキストを Flash に出して Hub 自動戻り（演出は Step 5-5）

#### D. テスト

- `VSProto_RoundManagerTests` 既存テスト拡張：戦闘解決後の Capture/Fallen・救出フラグ連動
- `VSProto_RunFlowTests` 新規：ラン全体の進行（R1〜R7・各エンディング分岐）

**スコープ外**（Step 5-5 で実装）
- BridgetBond / BridgetConfront / EndingDefeat / EndingBitter / EndingTrue 演出ページ
- Phase 追加（Story 系）
- 皇太子（ボス）アイコン・スチル
- 1周目スキップ

**完了基準**：
1. R1〜R7 を通しで実行可能（戦闘実行→次ラウンド→…→R7 ボス戦→Hub 戻り）
2. バルドゥイン制圧→IsBridgetRescued 自動連動
3. ラン終了時 EndingKind が3分岐で正しく確定（Defeat/Bitter/True）
4. 次ラン開始時にブリジットが初期手駒に加わる（解禁済の場合）
5. Editor テストグリーン

---

### Step 5-5：エンディング3分岐シーン拡張 — 重さ：**中** ✅ ロジック完了（2026-06-07・アセット待ち）

**実装内容**（2026-06-07 完了）
- 新規 `VSPrototypeStoryContent`（純Core・Core/Prototype/VSPrototype/ 配下）：5種類のページ集約
  - `BridgetBondPages` / `BridgetConfrontPages`（R5 終了演出・2 ページ各）
  - `EndingDefeatPages`（「ありがとう」拡張・4ページ）
  - `EndingBitterPages`（既存スチル流用・「魔王の影は残った」 3ページ）
  - `EndingTruePages`（皇太子救出シーン・3ページ）
- 新規 `VSPrototypeStoryGUI`（UnityView）：Phase=BridgetEvent / EndingEvent の全画面描画
  - `Stage3StoryProgress` / `Stage3StoryOverlay` 流用＋「次へ →」「Skip」ボタン
- `VSPrototypeBootstrap` 拡張：
  - Phase 2状態追加（`BridgetEvent` / `EndingEvent`）
  - `StoryProgress` プロパティ・`AdvanceAfterRound()` / `EnterEndingEvent()` API
  - 演出完了コールバックで次ラウンド／ラン終了の自動遷移
  - `LastRunReward` / `LastEnding`（Hub 表示用）
- `VSPrototypeMapGUI`：「次のラウンドへ →」「メタ拠点へ →」を `AdvanceAfterRound` / `EnterEndingEvent` 経由に切替
- `MetaHubGUI`：ヘッダ下に「── 直近のラン ──」セクション追加（エンディング種別＋獲得 +N）

**設計判断（記録）**
- 当初想定の「`Stage3StoryContent.EndingDefeatPages()` を拡張」は、戦闘プロト 2026-06-06 現状凍結原則と衝突するため **取りやめ**。VSプロト専用に `VSPrototypeStoryContent` を新規分離した（戦闘プロト側の挙動に副作用を出さない）。
- ファイルは純データかつ UnityEngine 非依存のため Core 配下に配置（Editor テスト容易）。Stage3StoryContent との「データ層と表示層」の置き場所差は意図的。

**必要アセット**（フォールバックで暫定動作中・後で生成）
- `Assets/Resources/Images/scene_prince_rescue.png` → `ending_victory_0` にフォールバック
- `Assets/Resources/Images/scene_bridget_confront.png` → `ending_defeat` にフォールバック
- `Assets/Resources/Images/scene_bridget_bond.png` → `ending_victory_0` にフォールバック

**完了基準**：3分岐エンディングが感情曲線として機能する／トゥルーエンドで達成感が出る → 内部試遊で確認

---

### Step 5-8：内政層（召集・ユニット強化＋前列後列＋常時偵察） — 重さ：**中** ✅ ロジック完了（2026-06-07・実機試遊未）

> 2026-06-07 Step 5-5 完了直後のユーザー指摘で、メタ強化「行動力 +1」「初期所持ユニット +1」が消費先を持たないドリフトが発覚。
> VSプロト USP「成長曲線（周回でユニット数・強さが増えてバルドゥイン救出に手が伸びる）」を成立させるため、ラン内の内政層を後付けで追加する。**仕様詳細は [310 §1.12](310_vsprototype_spec.md) 参照**。

#### A. 内政フェーズ＋行動力（Core）

- 新規 `VSPrototypeInteriorState`（ラウンドごとの行動力残数・実行済コマンドセット）：戦闘プロト `Stage3CampaignState` の該当フィールド（`ActionPoints` / `ExecutedInteriorActions`）を VSプロト用に切出し
- `VSPrototypeBootstrap` に Phase=InteriorAction 追加・ラウンド開始時に内政フェーズに入る
- 行動力初期値はメタ強化 `ActionPoints` の Lv で決定（2 or 3）
- 同一ラウンド内で同じコマンドを2回実行不可

#### B. 召集ドラフト（Core）

- 戦闘プロト `Stage3DraftService` をラッパー経由でそのまま流用
- ドラフトプールは `Stage3DraftPools` から：
  - 固有ユニット（ブリジット・姫騎士・皇太子）は **除外**
  - VSプロト用に既存通常／レアプール再利用（Skirmisher / Attacker1 / Archer / FireMage / Healer1 など）
- `Bootstrap.StartNewRun` でメタ強化 `InitialUnit` Lv 分だけ追加ドラフトを実行（初期手駒+1）

#### C. ユニット強化（Core）

- 戦闘プロト `UpgradeUnitType` ／ `UnitTypeEnhancementLevels` ／ `RuntimeUnit.EffectiveATK` を流用
- 同一兵種2回まで制約
- 強化値は戦闘プロトと同値（`Unit.EnhancementATKPerLevel` / `EnhancementHPPerLevel`）
- **姫騎士の拠点Lv連動強化は無効化**（VSプロト独自・拠点強化撤廃のため。代わりに `PrincessAtk` メタ強化で底上げ）

#### D. 前列後列・距離・守護（Core）

- `RuntimeUnit.IsFrontRow` / `IsBackRow` / `TargetEvaluator` / `ActionExecutor` は既に Stage3BattleRunner.Run 経由で動いている
- 配置時に「マス → 前列3／後列3 のいずれか」を指定する 2段階フローへ拡張
- `MapNode.AssignAlly` の slotIndex を呼び出し側で適切に渡す（既存 API のシグネチャは変えない）

#### E. 常時偵察（UI）

- 偵察コマンド廃止
- マスホバー時（PC前提・900 §2.1）に**敵編成のツールチップ表示**：兵種名×N＋総HP概算
- `VSPrototypeMapGUI.DrawMapNode` 拡張

#### F. 内政 UI（UI）

- `VSPrototypeMapGUI` または新規 `VSPrototypeInteriorGUI` で内政フェーズ描画：
  - ヘッダに「行動力 X/Y」表示
  - 「召集」「ユニット強化」ボタン（既出済コマンドは disabled）
  - 召集中：3択ドラフトカード UI（戦闘プロト `Stage3CampaignGUI.DrawDraftPickUI` の OnGUI パターン参考・新規構築）
  - ユニット強化中：兵種選択リスト（Lv 表示）
  - 「内政終了 →」ボタンで配置フェーズへ
- 配置フェーズの拡張：マスクリック後、6スロット（前列3＋後列3）の選択ポップアップ

#### G. テスト

- `VSProto_InteriorTests`（新規・純Core）：行動力消費・同一禁止・召集／強化の状態遷移
- `VSProto_RunFlowTests` 既存：前列後列を持つように Place ヘルパーを拡張、戦闘解決後の状態が引き継がれることを確認

**流用調査**（[Step 5-8 着手前チェック](#step-5-8-着手前チェック) 参照）：
- Stage3DraftService / DraftPools：✅ 即流用
- Stage3InteriorService / CampaignState：✅ 構造流用（VSプロト用に拠点強化・偵察コマンド除く）
- TargetEvaluator / ActionExecutor：✅ 既に流用中（無変更）
- Stage3CampaignGUI：⛔ 新規構築（戦闘プロト凍結原則）

**スコープ外**（戦闘プロトにあるが VSプロト撤廃）
- 拠点強化（戦線拠点 BaseLevel 概念ごと撤廃）
- 偵察コマンド（常時可視化で代替）
- 治療／建設／資材購入／イベント

**完了基準**：
1. ラウンド開始時に内政フェーズが入る／行動力が減る／同一禁止が効く
2. 召集で3択ドラフトが出る／メタ「初期所持ユニット +1」で初期手駒が増える／メタ「行動力 +1」で内政枠が増える
3. ユニット強化で兵種ごとに ATK/HP が上がる（戦闘で反映）
4. マスに前列3／後列3 で配置できる／近距離は前列のみ攻撃可
5. 敵編成がホバーで見える（偵察コマンドなし）

#### Step 5-8 実装結果（2026-06-07 完了・3コミットで完了）

| サブ | 内容 | コミット |
|---|---|---|
| 5-8a Core | InteriorAction enum / InteriorState / DraftPool / DraftService / InteriorService と3テスト | `6ef81ac` |
| 5-8b UI | Bootstrap Phase=InitialDraft/InteriorAction 拡張 ／ InteriorGUI 新規 ／ MapGUI スロット選択モーダル＋ホバーツールチップ | `0bac657` |
| 5-8c 仕様書 | 310 §1.12 と 320 を「実装済」に更新／実機セットアップ手順 | 本コミット |

**仕様変更点**（実装中にユーザー指摘で変更）：
- 初期所持ユニット +1 は当初「ランダム自動追加」予定だったが、「完全ランダムだと前衛後衛バランスが成立しない」というユーザー指摘で **初期ドラフト形式**（プレイヤーが3択から選ぶ）に変更。Phase=`InitialDraft` を新規追加し、メタ強化はドラフト回数 +1 として作用する形に
- 固定加入は姫騎士のみ（解禁時ブリジット追加）。残りは初期ドラフト 5 回（メタ強化で最大8回）から選ぶ

**着手前チェック実施結果**：
- ✅ `Stage3DraftService` / `Stage3InteriorService` を Read して契約把握済
- ✅ 守護は `BattleManager.RowCoverTag` で実装済・`Stage3Roster.GeneralTank/HpTank/Paladin` に付与済（VSプロトでこれらを召集すれば自動で機能）
- ✅ State 設計：VSプロト独立の `VSPrototypeInteriorState` を新規作成（Stage3 構造に依存しない）
- ✅ 配置 UI：新規 `VSPrototypeInteriorGUI` に分離（StoryGUI と同パターン）

#### 実機セットアップ手順（Unity Editor 側でユーザーが実施）

シーン `Assets/Scenes/EcholosProto_VS.unity` を開いて Bootstrap GameObject に以下を追加：

1. **`VSPrototypeInteriorGUI` コンポーネントを Add Component**（Bootstrap GameObject 上）
   - これを忘れると Phase=InitialDraft / InteriorAction で画面が真っ暗になる（Step 5-5 の StoryGUI 追加忘れと同じ症状）
2. シーン保存（Ctrl+S）
3. Play → メタ拠点「次のランへ →」→ 初期ドラフト5回 → R1 内政フェーズ → 配置→戦闘 の流れが動くこと
4. 内政フェーズで「召集」→ 3択カード → 1体追加 → 行動力1減ることを確認
5. 「ユニット強化」→ 兵種を1つ強化 → 戦闘解決時に ATK/HP 反映を確認

新規コンポーネントは1つだけ（StoryGUI 追加と同様の作業）。

---

### Phase 1：プレイ感向上 — 重さ：**中**（2026-06-08 着手中）

> 機能 Vertical Slice 完成（Phase 0 ＋ Step 5-0〜5-8）後の体験ブラッシュアップ。
> ゲーム層の完成度を高めて人に遊んでもらえる状態にするための UI 動線改修。

#### Phase 1 第1案：動線一体化（A/B/F）✅ 完了

「内政→配置→戦闘」のラウンド内動線を、「マップ常時表示＋必要時にモーダル」型に統合。

| Step | 内容 | 状態 | コミット |
|---|---|---|---|
| 1-A-1 | MapGUI に内政コマンド統合（右パネルに召集/強化/内政終了＋行動力表示） | ✅ | `7ae0f70` |
| 1-A-2 | InteriorGUI サブモード描画を半透明オーバーレイ＋中央モーダル化 | ✅ | `c4fdb19` |
| 1-B-1 | 配置統合モーダル新設（左=未配置ユニット一覧／右=6スロット 2クリック動線） | ✅ | `c91477a` |
| 1-B-2 | 左パネル整理（配置先マス名表示＋編成サマリヘッダ） | ✅ | `d5b8f9b` |
| 1-F-1 | マス内に配置スロット小アイコン6個（前列3＋後列3）可視化 | ✅ | `e7710f0` |

仕様書同期：310 §1.8 / §1.8.1 / §1.12.6 / §1.12.9 に新動線・新 UI を反映済。

**主要 API 変更**：
- `VSPrototypeBootstrap.CurrentInteriorSubMode`（新規・None/Conscript/Upgrade）
- `BeginUpgradeSubMode()` / `CancelInteriorSubMode()`（新規）
- 既存 `BeginConscript` / `AcceptConscriptPick` / `CancelConscript` / `ExecuteUpgradeUnitType` / `FinishInteriorPhase` / `BeginRoundInteriorPhase` で SubMode 同期

#### Phase 1 第2案：体験向上（D/G/E/C-1/C-2）✅ 完了

| Step | 内容 | 状態 | コミット |
|---|---|---|---|
| 1-D-1 | 兵種ソート（固有→Normal→Rare 順で王国軍リスト＋配置モーダル一覧を整列） | ✅ | `1c176e3` |
| 1-G-1 | 固有ユニット（姫騎士・ブリジット）を兵種強化対象から除外＋GUI 行非表示＋テスト 4 ケース | ✅ | `8ce1493` |
| 1-E-1 | 配置引き継ぎ：R2-R6 開始時に前ラウンドの生存配置を同マス・同スロット自動復元＋テスト 5 ケース | ✅ | `3d76d26` |
| 1-C-1 | 役割タグ表示：UnitDisplayLabels ヘルパ新設＋ドラフトカード／配置モーダル一覧／強化リストに `[盾/攻]` 等の短ラベル | ✅ | `be42696` |
| 1-C-2 | 1 周目限定で初期ドラフトをスキップ＋固定構成投入（VSPrototypeFirstRunFixedRoster 新設） | ✅ | `f76314d` |

仕様書同期：310 §1.8.1（配置引き継ぎ／役割タグ／ソート）／§1.12.4（1 周目固定構成）／§1.12.5（固有ユニット強化対象外）に反映済。

#### Phase 1 フォロー案A：実機試遊 FB 対応（不具合 2 + 改善 1）✅ 完了

| Step | 内容 | 状態 | コミット |
|---|---|---|---|
| ① fix | R7 本拠地配置不可バグ修正（CanAssign の Home ケース true 固定＋テスト 2 件） | ✅ | `c51e2d0` |
| ② fix | 召集ドラフトのキャンセル封じ（キャンセルボタン削除＋モーダル外 MouseDown を SubMode 別分岐） | ✅ | `4ac16f7` |
| ③ feat | 配置モーダルに「他マスに配置済み（移動可）」セクション＋スロットクリック時に旧マスから自動 Unassign | ✅ | `0883af3` |

仕様書同期：310 §1.4（本拠地常時配置可）／§1.8.1（配置モーダル動線・配置済セクション）／§1.12.4（キャンセル不可）に反映済。

#### Phase 1 完了宣言

Phase 1 第1案・第2案・フォロー案A の合計 15 コミットで動線一体化・体験向上・不具合修正がすべて反映され、実機試遊 OK 確認済（2026-06-08）。残改善案（戦闘実行警告／強化結果表示／デバッグコマンド）は今回スコープから外し、戦闘 UI フェーズへ進む。

---

### Phase 2：戦闘 UI 仮作成（A2 規模） ✅ 実機試遊 OK（2026-06-08）

Phase 1 完了後の次タスク。戦闘ロジックは既に [BattleReport](../Assets/Scripts/Domain/Battle/Replay/BattleReport.cs) / [BattleEvent](../Assets/Scripts/Domain/Battle/Replay/BattleEvent.cs) で時系列再構築可能だが、プレイヤーには [VSPrototypeRoundResult](../Assets/Scripts/UseCase/VSPrototype/VSPrototypeRunResult.cs) のサマリ（「制圧 +2 / 陥落 +0」）しか見えていなかった。戦闘経過を見せる UI を仮作成し、「戦闘が起こっていて、誰が何をしたか」を伝達する。

| # | 範囲 | 内容 | コミット |
|---|---|---|---|
| 1 | 骨格 | `VSPrototypePhase.Battle` 追加＋ Bootstrap に再生キュー（`VSPrototypeBattleSegment`）＋ `ResolveAndBeginBattleReplay`／`AdvanceToNextBattleSegment`／`FinishBattleReplay`＋ `VSPrototypeBattleGUI` 雛形＋ MapGUI 戦闘実行ボタンを Phase=Battle 経由に切替 | `15701fb` |
| 2 | 再生エンジン | Step / Auto x1/x2/x4＋ `_hp` / `_alive` 辞書＋戦闘終了判定＋死亡グレーアウト＋名前先頭 × | `517a1cb` |
| 3 | HP バー | スロット下端 14px＋ステータスリストミニバー 6px（緑→黄→赤グラデ＋黄色補間トレイル・補間時間 = SecondsPerEvent × 0.7） | `302e639` |
| 4 | ログ表示 | 最下部 1 行＋全ログモーダル（縦スクロール・モーダル外 MouseDown で閉じる・戦闘ごとリセット・モーダル中は背景ボタン全 disabled） | `f2980a4` |
| 5 | 連鎖＋スキップ | Auto モード自動連鎖（AutoAdvanceDelay=0.8 秒余韻）＋ `SkipToEnd`＋メインボタンのラベル分岐（スキップ／次戦闘／ラン続行） | `979564d` |
| 6 | Docs 同期 | 310 §1.8.2 戦闘画面節新設＋ §1.11 / §1.12.9 Phase=Battle 反映＋本セクション追加＋メモリ更新 | `8862748` |
| 7 | フォロー① | 連戦時ログモーダル即時切替（次戦闘ボタン押下時に `CloseLogModal` で旧 Report 表示を防止）＋スロット内 HP バー・名前ラベル削除（ユニット情報は下部ステータスリストに集約・FB 反映） | 本コミット |

**設計流用**：[Stage3BattleSpectatorView](../Assets/Scripts/Presentation/_old/Stage3BattleSpectatorView.cs)（_old/）の Tick / `_hp` / `_alive` / 黄バー補間 / 反転対峙構図のパターンを**新規再実装**（900 §4.2 _old/ から copy 禁止）。

**スコープ外**（Step 5-1b アセット待ち）：戦場背景画像／エフェクト演出／装飾枠／リーダースキル・撤退／ターゲット線／状態異常バッジ。

**実機セットアップ手順**：
1. `EcholosProto_VS.unity` を開き、Bootstrap GameObject に `VSPrototypeBattleGUI` コンポーネントを **Add Component**（忘れると Phase=Battle 遷移時に画面が真っ暗になる症状・Step 5-5 / 5-8 と同じ）
2. シーン保存（Ctrl+S）
3. Play → ラウンド配置 → 「戦闘実行 →」で戦闘再生画面に切替わることを確認

`.cs.meta` は Unity 起動時に自動生成 → 別途取り込みコミットを実施予定。

---

### Step 5-6：ストーリー再構成 — 重さ：**大**（2026-06-09 プロット確定・Phase A 確定）

プロット原文・フラグ管理・発火条件・分岐ロジックは **[330_vsprototype_storyplot.md](330_vsprototype_storyplot.md) を Single Source of Truth** とする。本節では実装計画のみ管理。

**Phase A：仕様確定**（✅ 完了・2026-06-09）

| 項目 | 確定方針 |
|---|---|
| EndingDefeat の演出分割粒度 | **3 SO 分割**（`ending_defeat_first` / `ending_defeat_normal_clear` / `ending_defeat_repeated`） |
| ペンダント気づきイベント | **挟む**（フラグ 2 段：`HasRescuedBalduin` → `HasNotedPendantPower`） |
| BridgetConfront 廃止 | **廃止**（B-b2 が R5 開始時に移動するため不要） |
| EndingBitter 廃止 | **廃止**（R7 ボス勝利は構造的に必ず A-c2 経路＝必ず True） |
| マップ初期状態動的化 | 解 C：`HasRescuedBalduin` 時に左列敵拠点のバルドゥイン拠点扱いを解除＝通常の敵拠点として初期化（[330 §7](330_vsprototype_storyplot.md)） |
| R6 = SLG クリア体裁 | 採用（R7 はストーリー的やりこみ） |

**Phase B：ロジック改修** ✅ 完了（2026-06-09・テスト緑確認済）

| # | 内容 | コミット |
|---|---|---|
| B-1 | `EndingResolver` Bitter 経路削除＋呼び出し追従（Bootstrap/MetaHubGUI/MapGUI/Tests 全件） | `8874b32` |
| B-2 | `MetaProgressState` に 3 フラグ追加＋ Mark* API（冪等）＋ Serializer 後方互換＋ Tests | `8b255fa` |
| B-3 | `VSPrototypeMapState` に `balduinAlreadyRescued` オプション追加＋ Bootstrap 配線＋ Tests | `e59d21d` → 挙動修正 `83ac829`（バルドゥイン拠点扱い解除に変更） |
| B-4 | `IMetaProgressView` 新設（Domain.Meta）＋ RoundManager コンストラクタ DI ＋ `CreateBossPattern(bool)` 二分岐＋ Tests | `fb2757e` |
| B-5a | `VSPrototypeStorySceneIds` に新規 11 件追加（旧 5 件は後方互換で残置） | `7017191` |
| B-5b | `VSPrototypePhase` を `StoryEvent` に統合＋ `VSPrototypeRoundStartEventResolver` 純関数新設＋ Bootstrap `TryBeginRoundStartEvent` ＋ PendantNote → SwordEmpowered 連鎖＋ F-5（17 ケース） | `775827c` |
| B-6 | A-a プロローグ演出：1 周目 `StartNewRun()` 冒頭で `TryBeginOpeningEvent` 試行＋ SO 不在時フォールバック | `4187f81` |

**Phase B 設計判断ポイント**（P1/P2 共に「将来抽象化」案を採用・2026-06-09 確定）：
- P1：`IMetaProgressView` 抽象を新設して Meta フラグ伝搬を一元化（メソッド引数追加方式は将来の引数膨張を生むため不採用）
- P2：演出 Phase を `StoryEvent` 1 つに統合（BridgetEvent / EndingEvent / RoundStartEvent 並列増殖を回避）

**Phase C：シーン追加＋ SO 生成＋ Bootstrap 切替** ✅ 完了（2026-06-09・第3 セッション）

| # | 内容 | コミット |
|---|---|---|
| C-1 | `SoAssetGenerator` 全面書き換え：旧 5 シーン生成削除＋新 14 シーン生成＋旧 .asset 4 個の冪等明示削除 | `35acf04` |
| C-2 | `StubStorySceneCatalog`（14 シーン化）＋ `VSProto_StorySceneCatalogTests` 全面書き換え（14 件ロード／重要文言検証） | `60c536b` |
| C-3 | `Bootstrap.AdvanceAfterRound` の R5 終了経路を `BalduinRescue` のみに切替（救援失敗時は R5 開始時 `BalduinSurrender` 既出で抑制）＋ `AdvanceToNextRoundAfterR5RescueEvent` リネーム | `9ca8997` |
| C-4 | Defeat 3 分割振り分け：`VSPrototypeDefeatSceneResolver` 純関数新設＋ `_hadFirstReachedBossAtRunStart` スナップ＋ EnterEndingEvent で `MarkFirstReachedBoss` 連動＋ Tests 6 ケース | `371f310` |
| C-5 | アクティブコード全 Grep で旧 const 参照ゼロ確認（コード変更なし） | （C-6 と統合） |
| C-6 | `VSPrototypeStorySceneIds` から旧 4 const（BridgetBond / BridgetConfront / EndingDefeat / EndingBitter）を物理削除＋セクション再整理 | `8b201ae` |

**Phase C 設計判断ポイント**：
- Defeat 3 分割は Bootstrap 内に閉じず、`VSPrototypeDefeatSceneResolver` 純関数として `UseCase/VSPrototype` に切り出し（`VSPrototypeRoundStartEventResolver` と同パターン）。NUnit でテスト可能・Bootstrap は呼び出すだけに集約
- 旧 const は後方互換用に残さず一気に物理削除（[[feedback-no-technical-debt]] に従う・「使われていない const を残すと意図不明な負債になる」）
- Bootstrap の演出関連メソッド名「BridgetEvent」は新仕様の R5RescueEvent にリネーム（コード自明性を維持）

**ユーザー側 Editor 作業**（Phase C コミット取り込み後）：
1. メニュー `Echolos > Data > SO アセットを生成` を再実行
   - 旧 4 .asset（bridget_bond / bridget_confront / ending_defeat / ending_bitter）が自動削除される
   - 新 14 .asset（opening / b_a_balduin / b_b1_letter / b_b2_surrender / b_c_girl / balduin_rescue / pendant_note / b_e_sword / a_c1_attack / a_c2_purify / ending_defeat_first / ending_defeat_normal_clear / ending_defeat_repeated / ending_true）が生成される
2. Test Runner で All Tests 実行
   - `VSProto_StorySceneCatalogTests`（14 件ロード／ページ件数／重要文言）
   - `VSProto_DefeatSceneResolverTests`（3 分岐 6 ケース）
   - 既存 `VSProto_RoundStartEventResolverTests` 17 ケース
   - すべて緑になる想定
3. 生成された 18 .asset の .meta 増減（4 削除＋14 新規）をユーザー側でコミット取り込み
4. 実機ラン通し：
   - 1 周目 R7 敗北 → `ending_defeat_first` 演出
   - 6R 防衛達成＋ R7 必敗（Pendant 未気づき）→ `ending_defeat_normal_clear` 演出
   - 2 周目以降通常敗北 → `ending_defeat_repeated` 演出
   - R5 救援成功 → `balduin_rescue` 演出
   - R5 救援失敗時の R5 終了は演出なし（R5 開始の `b_b2_surrender` で既出）

**Phase D：ライティング**（ユーザー作業）：13 シーンの文章執筆。Claude は仮文＋構造（ページ数・改ページ・画像参照）テンプレ SO を用意。

**Phase E：スチル**（ユーザー作業）：新規 ~10 枚（AI 生成＋選定）。`BackgroundRegistry` 経由で自動反映。

**Phase F：テスト** ✅ Phase B と並行完了（2026-06-09・テスト緑）

| # | 対象 | コミット |
|---|---|---|
| F-1 | `VSProto_EndingResolverTests` Bitter 削除＋シグネチャ追従 | B-1 と同梱 |
| F-2 | `VSProto_MetaProgressStateTests` / `VSProto_MetaProgressSerializerTests` 新フラグ 3 つ＋後方互換 | B-2 と同梱 |
| F-3 | `VSProto_MapStateTests` 救援済初期化＋（挙動修正でバルドゥイン拠点扱い解除に追従） | B-3 と同梱 |
| F-4 | `VSProto_EnemyPatternsTests` R7 二分岐 3 ケース | B-4 と同梱 |
| F-5 | `VSProto_RoundStartEventResolverTests` 新規 17 ケース | B-5b と同梱 |

**セッション分割実績**：

| セッション | 範囲 | 状態 |
|---|---|---|
| 2026-06-09 第1 | Phase A 確定＋ 330/310/320 整合 | ✅ 完了 |
| 2026-06-09 第2 | Phase B 全部＋ Phase F | ✅ 完了 |
| 2026-06-09 第3 | Phase C 全部（C-1〜C-6 計 6 コミット） | ✅ 完了 |
| 並行 | Phase D（ライティング）＋ Phase E（スチル）はユーザー側随時 | 完成形 |

---

### Step 5-9：ユニット整理＋敵専用兵種追加＋ R-1 統合 — 重さ：**大**

**SSoT**：[340_unit_lineup.md](340_unit_lineup.md)（味方 14 ＋ 既存敵 3 ＋ 新規敵 10 ＝ 27 体）。

**着手タイミング**：Step 5-7（統合テスト＋バランス調整）の**直前**。バランス調整に入る前にユニット ID／命名規約／敵専用兵種をすべて確定させる方針。

**進捗**

| Phase | 内容 | 状態 |
|---|---|---|
| **A：仕様確定** | 既存 20 兵種整理表／新ラインナップ提案／命名規約確定／340 新規作成 | ✅ 完了（2026-06-09） |
| **B-1** 基盤拡張 | `Waza.DispelsBuffs` 追加＋ `ActionExecutor` で Debuff カテゴリ purge 処理＋テスト | ✅ 完了（`a8e98d2`） |
| **B-2** Roster 3 分割 | `CharacterRoster.cs` → `AlliesRoster`/`EnemiesRoster`/`BossesRoster`＋共通 `RosterHelpers` ／帝国軍 10 体新規追加（`imperial_*`）／呼び出し側 12 ファイル一括追従 | ✅ 完了（`9b76041`） |
| **B-3** リメイク／調整／削除 | 巫女・騎士・軍師リメイク／サムライ・大槌兵バランス調整／大盾兵・アサシン・旧 s3_ninja 削除（性能は `ImperialShadow` に移植） | ✅ 完了（`07d566c`） |
| **B-4** Id 一斉切替 | Unit.Id `s3_*` 撤去／Unit SO 17 個 git mv＋内部 Id YAML 置換／旧 SO 4 個削除／Waza SO rename 2 個＋ `tac_dispel` 属性変更／新規 Waza SO 2 個生成（`waza_heal_small_aoe` / `waza_tac_purge`） | ✅ 完了（`fa44cf9` ＋ hotfix 5 件） |
| **B-5-1** IconRegistry パス分岐 | `Resources/Icons/Battlers/{Allies\|Enemies\|Bosses}/{unitId}.png` 優先＋旧フラットパスフォールバック／陣営判定は Id プレフィックス | ✅ 完了（`a37e28f`） |
| **B-5-2** GuideContent 追従 | `Stage3UnitGuide` → `UnitGuide` クラスリネーム／全 28 体ガイド整備（帝国軍 10 体は文言完全分離）／ `UnitSpecOrder` 順序追従 | ✅ 完了（`6ba18bf`） |
| **B-5-3** 散兵表示名追従 | `EnemiesRoster.Skirmisher` 表示名「散兵」→「帝国偵察兵」＋ SO 内部 Name 追従 | ✅ 完了（`bdeb8ce`） |
| **B-6** 残テスト追従 | `VSProto_EnemyPatternsTests` の「散兵」表記を「帝国偵察兵」に追従（マッチアップ系は Step 5-7 送り） | ✅ 完了（`1af701d`） |
| **B-7** Docs/メモリ同期 | 310 §1.4／320 §2／340 §6／500 §2.2 等の追従＋メモリ更新 | ✅ 完了（本コミット） |
| **C：アセット投入** | 既存 PNG を `Allies/` へ git mv／敵専用 10 体を ChatGPT 生成 → `Enemies/` 配置／ Princess / Bridget 配置調整 | ⏳ ユーザー作業 |
| **D：動作確認** | 実機試遊（バランス調整は Step 5-7 で別途） | ⏳ ユーザー作業 |

**設計判断ログ**
- **軍師リメイク**：「2 ターン交互」は新規 TriggerCondition `alternate_every_n_turns` を追加せず、**2 Waza（`tac_purge` / `tac_dispel`）＋ CD 互い違い（`InitialCooldown=0/1`）＋ `IsForcedWhenReady=true`** で表現可能と判明。Trigger 抽象に新規概念を入れずに済む（[340 §6.1](340_unit_lineup.md)）
- **旧 s3_ninja 性能**：廃止せず `EnemiesRoster.ImperialShadow` に移植（敵専用に保持）
- **アイコンパス**：陣営別階層化＋旧フラットパスフォールバック。Phase C 過渡期と本実装移行を両立
- **帝国軍ガイド文言**：性能は味方コピーだが GuideContent では文言完全分離（敵専用視点で再執筆・ユーザー判断 2026-06-09）

**Step 5-7 バランス調整時の課題（B での未確定事項）**
- マッチアップ系テスト NG（HpTank 削除＋サムライ/大槌兵調整で結果変化）
- 帝国軍 10 体の数値独立調整（現状は味方コピー）
- 6R 中ボス（`PoisonBaron`/`OneEyedSamurai`）編成詳細＋配置先決定

**後回し（Step 5-9 内・Phase B 後段）**
- 前衛新規（味方）：大盾兵廃止＋サムライ Rare 化で Normal 前衛が手薄
- 皇太子（ボス）：現状 `OneEyedSamurai` ワークアラウンドで A-c1（無敵）／A-c2（戦える）2 形態を上書き実装中

**完了基準**：味方・敵が別 ID／別アセットで描画される＋既存テスト全 PASS（マッチアップ系は Step 5-7 送り）＋ Phase D 実機試遊で破綻ゼロ。

#### 320 から移動：ユニット変更詳細の Why（2026-06-10 320 を仕様書化した際に転記）

**リメイク 4 体**

- **巫女**（state 治癒 → 全体小回復）
  - 削除：浄化（cleanse）／杖打ち
  - 追加：`heal_small_aoe`（CD 2・amount 14・NoNormalAttack）
  - Why：状態異常の発生源が VSプロト範囲では限定的（炎魔導士の Burn rider・男爵の毒）で、状態異常治癒が司祭の下位互換になっていた。シンプルな全体補助に役割を作り直す
- **騎士**（自己ガード → かばう）
  - 削除：守りの祈り（自己回復＋自己 DefenseUp）
  - 追加：`RowCoverTag`／聖剣は据え置き
  - Why：レアから一般枠へ格下げ。「重装兵より硬くないが、殴れるかばう役」のポジションを明確化
- **忍者**（旧 s3_assassin の性能継承）
  - 旧 `s3_assassin`：物理削除
  - 旧 `s3_ninja`（麻痺・回避）：性能は `EnemiesRoster.ImperialShadow` に移植して廃止
  - 新 `ninja`：旧アサシン性能継承（HP 95 / ATK 42 / SPD 16 / MageHunter / 暗殺＋疾風刃）
- **軍師**（dispel 限定 → 2 ターン交互）
  - 削除：解呪（味方デバフ解除のみ）
  - 追加：2 Waza ＋ CD 互い違い（奇数ターン＝敵バフ解除／偶数ターン＝味方デバフ解除）
  - 置物オーラ AttackUp Mag5 はそのまま

**バランス調整 2 体**

- **大槌兵**：PDEF 4→3 ／ ATK 28→32（タンク寄りデバッファー → アタッカー寄りデバッファー）
- **サムライ**：HP 115→130 ／ ATK 32→36 ／ 薙ぎ払い mult 0.9→1.0（Normal → Rare 格上げ＋数値強化／和風ユニットはレアに集約する方針）

**削除済（Step 5-9 物理削除）**

| 種別 | 対象 |
|---|---|
| Roster 関数 | `HpTank`（大盾兵）／`Assassin`（旧アサシン）／旧 `Ninja`（麻痺・回避版） |
| SO アセット | `unit_s3_tank_hp.asset` / `unit_s3_assassin.asset` / `waza_med_purify.asset` / `waza_pal_guard.asset` |
| Roster ファイル | `CharacterRoster.cs`（Allies/Enemies/Bosses/Helpers の 4 ファイルに分割） |

旧 `s3_ninja` の性能（麻痺・回避）は新 Id `imperial_shadow` として `EnemiesRoster` に移植済。

**帝国軍編成方針（中ボス取り巻き）**

- 旧 `PoisonBaronParty` / `SamuraiParty` は中身を帝国軍 10 体に差し替える前提（味方ユニットを敵側に混在させない）
- 配置先（左／中央／右）と編成の選定ロジックは Step 5-7 バランス調整マター
- ストーリー的に中ボス撃破時の演出は持たない（330 スコープ外）

**旧 s3_ninja 性能の保持判断（2026-06-09 ユーザー承認）**

メモ「現忍者の性能で悪くないが要調整」を踏まえ、旧 s3_ninja の麻痺・回避性能を「帝国の影」に保持する案を採用。これにより旧 s3_ninja 性能は廃止されず敵専用に移植された。

#### Step 5-9 Phase G：新規ユニット追加（傭兵＋皇太子 2 形態）✅ 完了（2026-06-10）

**追加ユニット**：
- 傭兵（mercenary）：Normal 11 体目。大剣の前衛物理アタッカー。
- 皇太子（闇）（boss_prince_dark）：R7 必敗ラスボス（A-c1 経路）。
- 皇太子（浄化）（boss_prince_light）：R7 最強ラスボス（A-c2 経路）。

**新基盤**：
- `StatusEffect.IsUndispellable` フラグ：`DispelsBuffs` / `DispelsDebuffs` 経路で剥奪されない印。`CleansesStatusAilments`（状態異常解除）は別経路なので対象外。
- `LonerWolfProcessor`（新規 Domain/Battle）：陣営生存数連動の動的バフ評価。
  - 戦闘開始時：`BattleManager.InitializeBattle` から呼ぶ（編成時点で 3 体以下でも発動）
  - 死亡時：`ActionExecutor.OnUnitDied` を BattleRunner で購読
  - 強度：3 体→+10／2 体→+20／1 体→+30（等差・両ステ共通）
  - 効果は `AuraSourceId="loner:<id>"` で識別し、再評価時にまず剥奪→新強度で再付与

**ワークアラウンド撤去**：
- VSPrototypeEnemyPatterns.CreateBossPattern の「OneEyedSamurai を Name 上書き＋ステ書き換え」を廃止。`PrinceDarkParty()` / `PrinceLightParty()` を直接呼ぶ形に。

**設計判断ログ**：
- 皇太子（闇）の x1.5 攻撃力は「Magnitude=15・MaxStacks=3」で蓄積式に。EffectiveATK の Magnitude は加算（+15）なので、基礎 ATK 30 に対して +50% 相当。3T 毎にスタックが積まれて段階的に火力が上がるドラマチック演出を採った。
- 皇太子（浄化）の 3 行動サイクルは軍師の互い違い CD パターン踏襲（CD3 / 初期 CD 0/1/2）。NoNormalAttack タグで通常攻撃を抑止し、CD 中の挙動も指定 3 行動のみに制限。
- 傭兵能力「孤高の戦士」の人数判定は「自分含めて 3 体以下」。本人死亡時は剥奪のみ（再評価がそのまま死亡剥奪としても機能）。
- 帝国軍版傭兵（imperial_mercenary）は追加しない方針。既存 `imperial_samurai`（帝国傭兵・サムライコピー）が同じ役割を担う。
- 皇太子（浄化）の取り巻き編成（6 人）は別途検討（Step 5-7 マター）。BossesRoster.PrinceLightParty で帝国軍精鋭を仮置き。

**コミット**：
- `8947c6e`：IsUndispellable フラグ追加＋ Dispel 経路改修
- `e2f4610`：LonerWolfProcessor 新規＋ BattleManager / BattleRunner 結線
- `b6d68d2`：傭兵 Roster 追加＋ GuideContent 追加
- `f754466`：皇太子 2 形態 Roster 追加＋ VSPrototypeEnemyPatterns 差し替え

#### Step 5-9 Phase H：戦闘単体検証用 Debug シーン（DebugBattleSandbox）✅ 完了（2026-06-10）

**目的**：Step 5-7 バランス調整に向けて「全ユニットを自由に組み合わせて戦闘だけ単体で検証する」場を用意。本実装にエフェクト等を加えた際にも自動で恩恵を受けるよう、戦闘再生 GUI を本シーンと共有する設計。

**新基盤**：
- `IBattleReplayHost`（Presentation/Common）：戦闘再生 GUI が依存する薄い抽象。VSPrototypeBootstrap と DebugBattleSandboxBootstrap の両方が実装し、`VSPrototypeBattleGUI` は `_host` 経由で透過的に動く。`[RequireComponent(VSPrototypeBootstrap)]` は削除し、Awake で `GetComponents<MonoBehaviour>()` から interface 実装を検索する形（[RequireComponent] は C# interface 非対応のため）。
- `UnitIdResolver`（Presentation/DevTools）：Unit.Id 文字列 → Unit インスタンスの解決マップ。味方 16・敵 15 体（汎用 11＋ボス 4）の static factory を辞書化。`Get*` は毎回新規 Unit を返す（戦闘間の共有汚染防止）。将来 IUnitCatalog へ切り替える際は本クラスの中身を入れ替えるだけ。
- `DebugBattlePresets`（Presentation/DevTools）：プリセットを純粋データ（UnitIds: List<string>）で味方／敵別に定義。追加は List に new() を 1 件追記するだけで、コードべた書き感を排除。
- `DebugBattleSandboxBootstrap`（Presentation/DevTools）：Debug シーンの Bootstrap。配置画面 GUI＋ IBattleReplayHost 実装で、VSPrototypeBattleGUI を流用する。

**配置画面の構成**（OnGUI）：
- 上部ヘッダ：タイトル＋味方プリセット◀▶＋敵プリセット◀▶＋シード TextField＋🎲ランダム化＋全クリア＋Run ▶
- 左カラム：味方プール（UnitIdResolver.AllAllyIds 16 体）
- 中央：味方 6 スロット（前列 3＋後列 3）＋敵 6 スロット
- 右カラム：敵プール（UnitIdResolver.AllEnemyIds 15 体）
- 操作：プールでクリック→選択／空スロットクリック→配置／配置済スロットクリック→解除

**戦闘実行**：
- `BattleRunner.Run(allies, enemies, maxTurns: 15, random0to99: () => rng.Next(0, 100))` でレポート生成
- `_segments` に 1 件キュー化＋ `_isActive=true` → 次フレームから VSPrototypeBattleGUI が描画
- 「ラン続行 →」（FinishAll）で `_isActive=false` に戻り、配置画面に**編成保持で復帰**

**設計判断ログ**：
- VSプロト本シーン（Bootstrap・MapGUI・InteriorGUI 等）には一切手を入れない方針を貫いた。IBattleReplayHost の導入と VSPrototypeBootstrap の明示的実装の追加で、既存利用箇所の改修ゼロを達成。
- 配置 UI は MapGUI の DrawPlacementModal* を**共有しない**判断。MapGUI は「左=未配置 1 列／右=6 スロット」の片陣構図、Debug は「左=味方プール／中央=12 スロット／右=敵プール」の対称構図で根本的に違うため、見た目だけ揃えて新規実装。
- 配置は UnitId 文字列で持つ（`Dictionary<int, string>`）。戦闘実行ボタン押下時に Resolver で一気に Unit インスタンス化する遅延構築方式。プリセット適用も文字列のままで済むので軽量。
- 編成保存（PlayerPrefs）は採用せず。代わりにプリセットを `DebugBattlePresets.cs` 内に List で外出し、追加は 1 行で済む形にした。本実装セーブとの混線リスクを完全に断つ判断。
- 乱数シードは Debug GUI で手動入力＋🎲ボタンで Randomize。`new System.Random(seed)` を `Func<int>` でラップして BattleRunner に渡し、同シード＋同編成で完全再現を担保。

**セットアップ手順**（ユーザー側 Unity Editor）：
1. 空シーン `Debug_BattleSandbox.unity` を新規作成（900 §4.1 命名規約）
2. 空 GameObject「Bootstrap」を作り、以下 2 コンポーネントを Add：
   - `DebugBattleSandboxBootstrap`
   - `VSPrototypeBattleGUI`
3. シーン保存 → Play で起動確認

**コミット**：
- `cffe28b`：IBattleReplayHost 抽象追加＋ VSPrototypeBootstrap 実装宣言
- `b371334`：VSPrototypeBattleGUI を IBattleReplayHost 経由に切り替え
- `c352f4b`：UnitIdResolver＋DebugBattlePresets（味方 7／敵 8 件）＋テスト
- `bae38b9`：DebugBattleSandboxBootstrap 新規（配置 GUI＋IBattleReplayHost 実装）

#### Step 5-9 Phase J：麻痺仕様を「許容量倍化方式」へ変更 ✅ 完了（2026-06-10）

**背景**：机上レビューで旧麻痺仕様の欠陥が判明（連続再付与で永続麻痺ループが成立、`ParalysisIncapacitateCount` 指数減算が外部付与で容易に上書きされる）。ユーザー提示の新仕様で全面置換。

**新仕様**：
- ユニットは戦闘中に `ParalysisTolerance`（許容量）を持つ。初期値は `Unit.BaseParalysisTolerance`（既定 1）
- 自分の行動順時、麻痺スタック合計 ≥ 許容量 で行動不能（`IsParalyzed`）
- 行動不能発動時に Paralysis 効果全削除＋許容量倍化（1→2→4→8…）
- 結果：短期戦で強力／長期戦で自然に無効化／複数体麻痺で行動不能回数は微増のみ＝ハメ防止

**設計判断ログ**：
- 麻痺発動処理は `BattleManager.OnActionSkipped` を購読する `StatusEffectProcessor.HandleActionSkipped` に集約。BattleManager 側は無改修で、既存の行動スキップ判定（`IsParalyzed` / `IsFullyFrozen`）経路をそのまま流用。
- 凍結による行動スキップでは許容量を上昇させない（`HandleActionSkipped` 内で `IsParalyzed` チェック）。これにより凍結技を持つユニットが将来追加されても誤発動しない。
- 帝国の影の `MaxStacks=2 → 99` で事実上撤廃。理由：新仕様では行動不能発動時にスタック消去なので、付与上限は冗長。同ターン中の複数体付与でスタック合算が機能するために大きい値が必要。
- `Unit.BaseParalysisTolerance` を Unit 永続データに持たせた理由：ユニット個別の耐麻痺性を実装できる余地を残すため（プロト段階は全員 1）。将来の魔導士耐性ユニット・帝国軍精鋭などで活用可能。
- Cleanse 経由で麻痺スタック削除しても `ParalysisTolerance` は据置：戦闘中の累積耐性は解呪で失われない設計。

**テスト**：
- 旧麻痺テスト 4 件を書き換え（reduction 計算式は廃止）
- 新規 7 件追加：シミュレーション再現（1 体・3 体毎ターン付与）／BaseParalysisTolerance の初期値反映／EndPhase で減算しないこと等
- `MatchupTests` の「サムライ vs デバフバフ火力編成は勝てない」は新仕様で挙動変化の可能性あり（Step 5-7 マッチアップ再調整で吸収）

**コミット**：
- `cff301e`：麻痺仕様「許容量倍化方式」への置換（Unit / RuntimeUnit / StatusEffectProcessor / EnemiesRoster / テスト）

#### Step 5-9 Phase K：コード内コメントの大規模整理 ✅ 完了（2026-06-11）

**背景**：Phase J 完了後、コード内の `§10.4` 章番号参照（41 箇所）が旧戦闘プロト仕様書（210 系・現在は `_old/` 退避）由来で漂流していることを発見。当初「コード→ドキュメント参照禁止」規約化で対応しようとしたが、ユーザー指摘で「**本質はコメントが過剰**」と判明。

ただし「コメントを書くな」の意味ではなく、「コードを読まなくても実装内容が伝わる」役割を果たすコメントは積極的に残す。削除対象は「同じことを 2 回書くパターン」（Docs 参照＋直後に同内容説明）と「読み手に価値がないパターン」（経緯・作業ログ・パス転写）に限定。

**方針確定（素振り）**：
- コミット `b1edd5b`：900 §7.11 規約初版（「デフォルトは書かない」厳格版・後に方針転換で改訂）
- コミット `9c0c755`：§7.11 文面差し替え（「役割重視」スタンスに改訂）
- 素振り対象 LonerWolfProcessor.cs：消えすぎ → ユーザー FB → 控えめ版（同セッション内で書き直し）
- 追加サンプル AlliesRoster.cs / DebugBattlePresets.cs / ActionExecutor.cs 冒頭で方針合意

**実施範囲**：
- Domain/Battle（11）／ Models+Skills+Formula（14）／ Prototype（4）／ Catalog+Meta+Draft+Save+Story+GameCycle+Items（21）
- UseCase/VSPrototype（17）／ Data（22）／ Presentation（22 ヘッダのみ）
- 合計 約 112 ファイル・8 コミット

**コミット**：
- `b414d4f`：Domain/Battle
- `b065111`：Domain/Models+Skills+Formula
- `a3ccedc`：Domain/Prototype
- `12a76bf`：Domain その他
- `8cf4492`：UseCase/VSPrototype
- `81450ee`：Data
- `08e7b32`：Presentation

**残置（→ 後日対応・2026-06-11）**：
- SoAssetGenerator.cs 内部のシーン定義に残る `[330 §X.X]` 参照（影響軽微）→ 全削除（コミット `d1a7ba3`）
- Presentation 大規模 GUI ファイル（Bootstrap / BattleGUI / MapGUI / InteriorGUI / StoryGUI / MetaHubGUI）内部のセクション見出しコメント＋段階番号参照（コミット `725c2bd`）
- 約 7 ファイル -123 行のコメント整理。コンパイル影響なし

---

### Step 5-7：統合テスト＋バランス調整 — 重さ：**中**

**実装内容**
- 全ステップ統合後の自分プレイで H4 検証
- メタ通貨獲得量・敵強度・本拠地連続防衛難度のチューニング
- ラスボス取り巻き 5 体の最終決定（皇太子（闇）／（浄化）それぞれ）。現状 `PrinceDarkParty` / `PrinceLightParty` で仮置き
- 中ボス取り巻きの配置先（左／中央／右）と編成の選定ロジック確定
- 2〜3 名のプレイテスト依頼

**完了基準**：プレイテストで「次のラン、トゥルーエンドを見たい」「もう一度やりたい」の反応が出る

#### Step 5-7 Phase B：戦闘イベントのアクション単位スコープ集約（2026-06-11）

戦闘ログ整形の議論からスタートして、根本的に「Domain と Replay/GUI の接面はアクション単位スコープを表現していなかった」という設計負債が見つかった。中案（GUI 側でマーカー集約）と重案（Domain 側で ActionResolved イベント新設）を比較し、ユーザーの「Claude が『既存影響が少ない』理由で重案を捨てているときは疑え」という指摘で重案に転回。

**変更内容**：
- `Domain.Battle.HitOutcome`（新規・純 POCO）：1 ターゲットへの 1 ヒット結果を表す不変値オブジェクト
- `ActionExecutor.OnActionResolved`（新規イベント）：ExecuteAction 1 回ごとに Outcomes リストを束ねて発火
- 既存の OnHitLanded / OnHealed / OnHitEvaded / OnUnitDied は無修正（後方互換）
- `BattleEvent.ActionResolved` + `BattleEvent.Outcomes`（新規）：観戦ビュー用の集約イベント
- `BattleRunner` は OnActionResolved 購読に切替：アクション内の個別 HitLanded/Evaded/Died/Healed の Events 追加とテキストログを廃止し、集約版に統合
- `BattleGUI.ApplyEvent`：ActionResolved 1 件で Outcomes を一括反映（範囲攻撃の HP 同時更新が可能に）
- テキストログ：Outcomes ベースで「対象別ダメージ／回避／戦闘不能／付帯効果」を 1 アクション内にまとめ、Outcomes.Count >= 2 で総ダメージ行を追加

**設計原則**：
- 「アクション単位」は Domain 概念であり、Domain で表現すべき（中案のヒューリスティック集約は反撃技・連続行動・死亡連鎖などの将来拡張で破綻する）
- アクション「外」の StatusEffectApplied/Expired・BurnTick・TurnStart 等は引き続き個別 Event

**コミット**：
- `eee6abd`：HitOutcome 値オブジェクト追加
- `7cbcecb`：ActionExecutor に OnActionResolved 追加（テスト 8 ケース）
- `f9d551a`：BattleEvent.ActionResolved + BattleRunner 購読
- `8f354a2`：BattleGUI ApplyEvent ActionResolved 対応
- `df76b63`：テキストログを集約版に統合

**後続セッション候補**：
- StatusEffect/Outcome に SourceAbilityName 追加（「孤高の戦士」「王家の加護」表示）
- 反撃技・追撃の入れ子化（ActionResolved の入れ子対応）
- ログモーダルの cursor 連動（戦闘進行と同期）
- 「姫騎士」→「王女」表記置換

---

#### Step 5-7 Phase A：ドラフト仕様の刷新（2026-06-11）

戦闘プロト時代の「最序盤マッチアップ→終盤ボス→中間」順に倣ってバランス調整を始めるが、その前提として **ドラフト仕様の固め直し** を実施。

**変更内容**：
- ドラフトの Rare 抽選を「3 択全体で 1 回判定」→「**枠ごとに独立抽選＋★全 Rare スペシャル併用**」へ
  - AllRareSpecialProbability = 0.03（明示の★モード）
  - RarePerSlotProbabilities = [0.15, 0.15, 0.15]（枠別微調整可・初期値全枠統一）
  - 偶発全 Rare も同じ★ヘッダ表示で演出統一
- Rare プールを 3 → **5 体**（雷魔導士・巫女を Normal から Rare 移動）
- Normal プールに **傭兵（mercenary）追加**（仕様書 320 にはあったが SO 生成器・Stub 未登録の実装漏れ修正）
- 初期ドラフトは引き続き全枠 Normal 固定（Rare 抽選なし）

**狙い**：
- Rare を見かける頻度を 15% → 約 40.5% に拡大
- Rare/Normal 混在の 3 択という新しい選択判断を提供
- 傭兵を実装漏れから救出

**未確定の残論点（次セッション以降）**：
- 1 周目固定構成 5 体（重装兵／双剣士／弓兵／司祭／炎魔導士）の妥当性検証
- 2 周目以降の構成変更案：「王女＋固定 3 体＋初期ドラフト 3 体」とその固定 3 体は何にするか
- 騎士の Normal 据え置きナーフ案
- 召集の天井（連続非排出救済）を入れるか

**コミット（実装）**：
- `0e14a14`：Domain/Data 5 層拡張（DraftPool / Definition / Catalog）
- `6832170`：UseCase ロジック改修（DraftService / DraftOffer）
- `2289c6e`：SO 生成器更新＋Stub 構成同期（Normal 9 / Rare 5）
- `2828b18`：InteriorGUI 枠別 Rarity 表示追従
- `0da84d8`：テスト更新（DraftServiceTests / DraftPoolCatalogTests）

---

### リファクタ TODO（着手時期未定）

VSプロト Step とは独立した磨きこみ・命名整理タスク用の枠。

- ~~R-1：アイコン命名規約整理（`s3_` プレフィックス撤去）~~ → **Step 5-9 に統合**（2026-06-09）

現状この枠に登録されているタスクはなし。

---

## 3. 実装重さの全体感

実装順序通りに並べ替え：

| 順 | ステップ | 重さ |
|---|---|---|
| 1 | Step 5-0 データモデル追加 | **中** ✅完了 |
| 2 | Step 5-1a 戦略マップ GUI 最小実装 | **中** ✅完了 |
| 3 | Step 5-2 メタ通貨 | **小** ✅完了 |
| 4 | Step 5-3 メタ拠点 UI | **小** ✅完了 |
| 5 | Step 5-4 ラン進行＋加入＋エンディング分岐ロジック | **大** ✅完了 |
| 6 | Step 5-5 エンディング3分岐演出 | **中** ✅完了 |
| 7 | Step 5-8 内政層（召集・強化・前後列・常時偵察） | **中** ✅完了 |
| 8 | Phase 1 第1案：動線一体化（A/B/F） | **中** ✅完了（2026-06-08） |
| 9 | Phase 1 第2案：体験向上（D/G/E/C-1/C-2） | **中** ✅完了（2026-06-08） |
| 10 | **Phase 2 戦闘 UI 仮作成（A2 規模）** | **中** ✅完了（2026-06-08） |
| 11 | **Step 5-1b 戦闘画面の見栄え改善** | **中** ✅コード完了（2026-06-08・アセット待ち） |
| 12 | **Step 5-1c マップ見栄え強化** | **小** ✅完了（2026-06-09・磨きこみ①〜⑤＋実機試遊 OK） |
| 13 | Step 5-6 ストーリー再構成 | **大**（2026-06-09 Phase A/B/C/F 完了・残りは Phase D ライティング＋ E スチル＝ユーザー作業） |
| 14 | **Step 5-9 ユニット整理＋敵専用追加＋ R-1 統合** | **大** ✅ Phase A〜B 完了（2026-06-10・C/D アセット投入と実機試遊はユーザー作業） |
| 15 | Step 5-7 統合・バランス | **中** |

未完了の「大」は実質ゼロ（Step 5-9 は Phase B 完了）。Phase 2 ＋ Step 5-1c 完了・Step 5-1b はコード先行＋透過アセット待ち。残るは**残アセット投入＋ストーリー再構成＋バランス調整**の 3 軸。

**実装順序更新**（2026-06-09 ユニット整理タスク Step 5-9 を Step 5-7 直前に挿入）：見栄えアセット投入と Step 5-6 ストーリー再構成は引き続き並行進行可。Step 5-9 はバランス調整に入る前に「最終形に近いユニットラインナップ＋ ID 体系」を確定させる目的で挿入。

### マイルストーン：「人に遊んでもらえる形」（2026-06-08 確定）

以下 3 軸が揃った時点を一区切りのマイルストーンとする：

| 軸 | 主作業 | 担当 |
|---|---|---|
| **見栄え** | アセット 8〜12 種投入（戦場背景＋透過ちびキャラ 19＋ bridget＋マップ背景＋拠点アイコン 5）→ Claude 側でチューニング | ユーザー＋Claude |
| **ストーリー** | Step 5-6 プロット再考＋既存 5 シーン（BridgetBond/BridgetConfront/EndingDefeat/EndingBitter/EndingTrue）の再ライティング | ユーザー（プロット）＋Claude（実装） |
| **バランス** | Step 5-7 内部プレイ＋ 2〜3 名プレイテスト → 敵強度／メタ通貨／本拠地連続防衛のチューニング＝H4（USP）検証 | Claude＋ユーザー＋テスター |

---

## 4. 進捗

実装順序通りに並べ替え：

| 順 | Step | 状態 | 着手日 | 完了日 |
|---|---|---|---|---|
| 1 | 5-0 データモデル追加 | ✅ 完了 | 2026-06-07 | 2026-06-07 |
| 2 | 5-1a 戦略マップ GUI 最小実装 | ✅ 完了 | 2026-06-07 | 2026-06-07 |
| 3 | 5-2 メタ通貨 | ✅ 完了 | 2026-06-07 | 2026-06-07 |
| 4 | 5-3 メタ拠点 UI | ✅ 完了 | 2026-06-07 | 2026-06-07 |
| 5 | 5-4 ラン進行＋加入＋エンディング分岐ロジック | ✅ ロジック完了（実機動作確認はユーザー側で別途） | 2026-06-07 | 2026-06-07 |
| 6 | 5-5 エンディング3分岐演出 | ✅ ロジック完了（アセット待ち・実機試遊未） | 2026-06-07 | 2026-06-07 |
| 7 | 5-8 内政層（召集・強化・前後列・常時偵察） | ✅ ロジック完了 | 2026-06-07 | 2026-06-07 |
| 8 | Phase 1 第1案：動線一体化（A/B/F） | ✅ コード完了（実機試遊 OK・2026-06-08） | 2026-06-08 | 2026-06-08 |
| 9 | Phase 1 第2案：体験向上（D/G/E/C-1/C-2） | ✅ コード完了（実機試遊 OK・2026-06-08） | 2026-06-08 | 2026-06-08 |
| 10 | **Phase 2 戦闘 UI 仮作成（A2 規模）** | ✅ コード完了（FB 反映済・実機試遊 OK 2026-06-08） | 2026-06-08 | 2026-06-08 |
| 11 | **5-1b 戦闘画面の見栄え改善** | ✅ コード完了（アセット待ち） | 2026-06-08 | 2026-06-08 |
| 12 | **5-1c マップ見栄え強化** | ✅ 完了（磨きこみ①〜⑤＋実機試遊 OK） | 2026-06-08 | 2026-06-09 |
| 13 | 5-6 ストーリー再構成 | Phase A/B/C/F 完了（テスト緑）／残りは Phase D ライティング＋ E スチル（ユーザー作業） | 2026-06-09 | - |
| 14 | 5-7 統合・バランス | 未着手 | - | - |

### 5.1 開発ルール 900 違反履歴（2026-06-07 発覚分）

VSプロト Step 5-0〜5-4 実装中、`Docs/900_development_rules.md` を実装着手前に Read していなかったため、以下の違反を作り込んだ。Step 5-5 着手前に修正完了。

| 違反 | 内容 | 修正 |
|---|---|---|
| §1.3 イベント駆動 | `MapNode.EnemyComposition` / `AssignedAllies` を public List で公開し、`VSPrototypeRoundManager` と `VSPrototypeMapGUI` から直接 `Add` / `Remove` していた（過去の `RuntimeUnit.ActiveEffects` と同じ構造的問題） | API 経由（`AssignAlly` / `UnassignAlly` / `SetEnemyComposition` / `OnAllyAssigned` 等）にリファクタ＋関連テスト追加＋310 §1.3 更新 |
| §4.1 シーン命名 | `Assets/Scenes/VSPrototype.unity` が `EcholosProto_*` プレフィックス規約から外れていた | `EcholosProto_VS.unity` にリネーム（git mv で履歴維持） |

**再発防止策**：自動メモリに `feedback_check_development_rules` を新規追加（実装着手前の 900 Read を必須化）。

---

---

## 5. リスクと対策

| リスク | 影響 | 対策 |
|---|---|---|
| 領地マップ視覚化が「平面的すぎて伝わらない」 | USP実証失敗 | Step 5-1 着手前にラフ画像を描いて感触確認（ユーザー作成済みの PowerPoint 案あり） |
| 本拠地連続防衛が難しすぎ／易しすぎ | バランス曲線崩壊 | Step 5-7 で 2〜3 名プレイテスト後にチューニング |
| トゥルーエンドの皇太子救出スチル制作コスト | スケジュール圧迫 | テキスト主体 + 既存スチル流用フォールバック準備（最悪、新規スチルなし） |
| 既存戦闘プロト GUI と VSプロト GUI の共存複雑性 | コード可読性低下 | VSプロトは別 Scene + 別 GUI で開発し、安定後に統合判断 |

---

## 関連ドキュメント

- [300_vsprototype_policy.md](300_vsprototype_policy.md) — VSプロト方針
- [310_vsprototype_spec.md](310_vsprototype_spec.md) — VSプロト仕様
