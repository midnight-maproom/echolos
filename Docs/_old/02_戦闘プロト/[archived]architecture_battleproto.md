# ローグライト×オートバトル ミニゲーム アーキテクチャ設計書 (MVP版)（アーカイブ）

> **【アーカイブ通知】2026-06-07**
> 最初期のプロトタイプから戦闘プロト開発完了までの設計を継続記録していたドキュメントだが、VSプロト制作に際してより拡張性の高い 4 層アーキテクチャを採用したため設計書を刷新。
> 記録として残すが参照はしないこと。

## 1. 基本方針 (Architecture Rules)

- **UIとロジックの完全分離:** バトルロジックは純粋なC#クラス（POCO）で構築し、Unityの `MonoBehaviour` を継承させない。Core 層から UI 層を参照しない（逆は OK）。
- **イベント駆動:** スキル発動や状態変化のフックは、イベント通知（ObserverパターンまたはC# Action/Func）を用いて疎結合に実装する。
- **状態の独立性:** 戦闘中の状態（バフ、デバフ、現在HP）と永続データ（最大HP、基礎ステータス）は完全に分離して管理する。
- **状態変化は API メソッド経由で必ず通知発火:** ロジック層が状態を変える際は public フィールド直接公開を避け、`AddX` / `RemoveX` メソッド経由でのみ変更可能とし、メソッド内で `OnXAdded` / `OnXRemoved` イベントを発火する。これで UI 連動忘れを構造的に防ぐ（[`900_development_rules.md` §1.3](900_development_rules.md) 参照）。模範例：`ActionExecutor.OnHealed` / `OnHitLanded` / `RuntimeUnit.OnEffectAdded` / `OnEffectRemoved`。
- **Core 配下は UnityEngine 禁止:** `ProjectCitadel.Core.asmdef` は `noEngineReferences: true`。`UnityEngine.Mathf` 等を使えないので `System.Math` + 自前 `Clamp01` 等で代替する。
- **データ駆動設計（ユニット定義はコードに書かない）:** ユニット・技・状態異常の **定義データ**（ステータス値・技構成・付帯効果テンプレ）はコード（C# ファクトリ関数）に直書きせず、ScriptableObject + POCO の二段構成で外部データとして持つ。コードは「構造」と「振る舞いの実装」のみを担う。詳細は §9 を参照。
  - **何をコードから外すか**：固有のステータス値、技の構成パラメタ、付帯効果テンプレ、置物オーラ設定
  - **何をコードに残すか**：ダメージ計算式の実装、特殊発動条件の実装、戦闘ロジック全般
  - **接合方式**：データ側は「式ID + パラメタ」を持ち、コード側のレジストリで実装を引く（DamageFormulaRegistry / TriggerConditionRegistry）
- **SO 内ネスト POCO は必ず public field 形式:** ScriptableObject 内にネストするデータクラス（StatusEffect / UnitDefinition / WazaDefinition / FormulaParam 等）のフィールドは **auto-property（`{ get; set; }`）を使わない**。Unity のシリアライザは auto-property を認識せず SO に保存できない（[System.Serializable] を付けても無効）。
  - **正：** `public StatusEffectType EffectType;`
  - **誤：** `public StatusEffectType EffectType { get; set; }`
  - 既存 StatusEffect は VSプロト Phase 0-3 で全フィールドを field 形式に書き換え済。今後 SO 内に持たせる POCO を追加する時は最初から field 形式で書く。

---

## 2. コアデータ構造 (Core POCOs)

### 2.1 状態定義 (Enums)

- `UnitState { Active, Reserve, Dead }`
- `PhaseState { Start, Main, InterventionStandby, End }`
- `Element { None, Fire, Water, Ice, Lightning, Wind, Earth, Light, Dark }`
- `BattleResult { None, PerfectVictory, AdvantageousVictory, MarginalDefeat, CrushingDefeat }`（5値：完勝／辛勝／惜敗／完敗。`Defeat` 1値を惜敗・完敗の2値に分解済・§4.6 参照）
- `UpgradeType { StatBoost, SkillAdd, WazaUpgrade, MasteryBonus }`
- `AttackRange { Melee, Mid, Ranged }`（射程・[VSプロト Phase 0 で確定の定義](310_vsprototype_spec.md)：近接=自前衛→敵前衛のみ／中距離=自前後→敵前衛のみ／遠隔=自前後→敵前後）
- `PlacementHint { Any, Front, Back }`（VSプロト Phase 0 追加・推奨配置・UI ヒント）
- `UnitRole { Tank, Attacker, Support, Healer }`（VSプロト Phase 0 追加・戦術的役割・複数指定可）
- `AttackKind { None, Physical, Magical }`（VSプロト Phase 0 追加・主攻撃種別・UI 用・`Waza.IsPhysical` とは別軸）

### 2.2 プレイヤー（指揮官）データ (CommanderData)

- `string CommanderId, Name`
- `int BaseScoutingValue` (基礎偵察値)
- `int CurrentGauge, MaxGauge` (必殺技リソース)
- `Waza UltimateSkill` (指揮官専用アクティブスキル)
- `List<Item> Consumables` (消費アイテム。最大3スロット)
- `List<Equipment> EquipmentInventory` (所持している装備品のインベントリ)
- `int TotalExpItems` (所持経験値アイテム数)
- `int AccumulatedFailures` (敗北と撤退の累計回数。3でゲームオーバー)
- `string SelectedLeaderUnitId` (現在リーダーに指定されているユニットのID)
- `List<Unit> UnitRoster` (ランを通じて所持している全ユニット一覧。Active・Reserve・Deadを含む)

### 2.3 永続ユニットデータ (Unit) - 非戦闘時の実体

> **データ駆動化（VSプロト Phase 0 以降）**：本セクションのデータは **`UnitDefinition` (POCO) ＋ `UnitDefinitionSO` (ScriptableObject)** で外部化し、`UnitCatalog.Get(id)` 経由で実体化する。コードに直書きしない（§9 参照）。
>
> Phase 0 で追加・改名されたプロパティ：
> - `PlacementHint`（推奨配置・UI ヒント）
> - `CombatRoles`（戦術的役割・List\<enum\>）
> - `PrimaryAttackKind`（主攻撃種別・UI 用）
> - `AbilityLabels`（能力ラベル・UI 表示用）
> - `TargetTags`（特効判定タグ・旧 `Roles` を改名）

- `string Id, Name`
- `Element UnitElement`
- `PlacementHint` (推奨配置・Front/Back/Any・UI ヒント・システム制約なし)
- `int CurrentLevel` (1〜5)
- `int CurrentExp`
- `int MaxHP, CurrentHP`
- `int BaseATK` (基礎攻撃力)
- `int PDEF, MDEF`
- `int BaseEvasion` (基礎回避率)
- `AttackRange Range` (通常攻撃の射程・Melee/Mid/Ranged)
- `List<UnitRole> CombatRoles` (戦術的役割・Tank/Attacker/Support/Healer・複数可)
- `AttackKind PrimaryAttackKind` (主攻撃種別・Physical/Magical/None・UI 用)
- `List<string> AbilityLabels` (能力ラベル・「多段攻撃」「範囲攻撃」「暗殺」等・UI 表示用)
- `Equipment EquippedGear` (装備スロット。最大1つ。null可)
- `List<string> Tags` (内部処理タグ・NoNormalAttack / RowCover / MageHunter 等)
- `List<string> TargetTags` (特効判定タグ・「Mage」等・旧 `Roles` を VSプロト Phase 0 で改名)
- `List<Waza> BaseWazas`
- `List<Skill> BaseSkills`
- `StatusEffect AuraEffect` (置物オーラ・戦闘開始時に同陣営全員へ付与・[210 §4.4](210_prototype_spec.md))
- `List<UnitUpgrade> AvailableUpgrades` (固有の4つの強化肢)
- `List<UnitUpgrade> AppliedUpgrades` (適用済みの強化肢)
- **兵種強化フィールド（[210 §7.5](210_prototype_spec.md)）**：素データを変えず実効値で吸収するためのレベル係数群
  - `int EnhancementLevel` (現在の強化レベル・0で未強化)
  - `int EnhancementHPPerLevel` / `EnhancementATKPerLevel` / `EnhancementPDEFPerLevel` / `EnhancementMDEFPerLevel` / `EnhancementEvasionPerLevel` / `EnhancementMagnitudePerLevel`
  - 実効値は `RuntimeUnit.MaxHP` / `EffectiveATK` 等のプロパティで `素値 + 強化係数 × EnhancementLevel` を算出する

### 2.4 実行時ユニット (RuntimeUnit) - バトル用インスタンス

- `Unit BaseUnit`
- `int SlotIndex` (0〜5。0-2が前衛、3-5が後衛)
- `bool IsLeader` (戦闘開始時に `CommanderData.SelectedLeaderUnitId` と照合して決定。途中で死亡しても他に移譲しない)
- `int CurrentShield` (現在付与されているシールド量)
- `bool CanCarryOverShield` (ターン終了時にシールドを維持するかのフラグ)
- `List<Waza> BattleWazas` (バトル用の技リスト。CD/使用回数が戦闘中に変動する)
- `bool HasActedThisTurn`
- `int ParalysisIncapacitateCount` (この戦闘中に麻痺で行動不能になった累計回数)
- `int CurrentReviveCount` (戦闘中の復活可能回数スタック)
- `bool IsSummoned` (戦闘中にスキルによって召喚されたユニットか。優勢勝利の撃破数カウントから除外される)

#### 状態効果（ActiveEffects）— API 経由でのみ変更可能

`RuntimeUnit.ActiveEffects` は **`IReadOnlyList<StatusEffect>`** の読み取り専用ビューとして公開する。直接 `.Add` / `.Remove` を呼ぶことは禁止（コンパイル時点で不可能）。変更は以下の API メソッド経由のみ：

- `AddEffect(StatusEffect effect)` — 追加し `OnEffectAdded` 発火
- `RemoveEffect(StatusEffect effect)` — 削除し `OnEffectRemoved` 発火（戻り値で成否）
- `RemoveEffectsWhere(Predicate<StatusEffect> match)` — 述語マッチ全件削除・各削除で `OnEffectRemoved` 発火・戻り値で件数
- `ClearAllEffects()` — 全削除・件数ぶん `OnEffectRemoved` 発火
- `FindEffect(Predicate)` / `FindEffect(StatusEffectType)` / `FindEffects(Predicate)` — 検索 Helper（`IReadOnlyList` には `List.Find/FindAll` が無いため）

イベント：

- `event Action<StatusEffect> OnEffectAdded` — 効果追加時
- `event Action<StatusEffect> OnEffectRemoved` — 効果削除時

**設計理由**：以前は `public List<StatusEffect>` で公開されており、5ファイル10箇所から直接 Add/Remove されて UI 連動忘れが構造的に発生していた（A10 バフデバフアイコンが Phase 1 完了時から動いていなかった原因）。本構造で「ロジック追加したけど UI 連動忘れ」を**コンパイラ強制レベルで防ぐ**。§1 基本方針の「状態変化は API メソッド経由で必ず通知発火」の代表実装。

#### 計算系プロパティ（実効値の吸収）

兵種強化（[210 §7.5](210_prototype_spec.md)）と ActiveEffects のバフ／デバフを計算で吸収する読み取り専用プロパティ群。素データ（`BaseUnit.PDEF` 等）は不変。

- `int MaxHP` — `BaseUnit.MaxHP + EnhancementHPPerLevel × EnhancementLevel`
- `int CurrentHP` — `BaseUnit.CurrentHP` へのプロキシ
- `int EffectiveATK` — `BaseATK + 兵種強化 + AttackUp - AttackDown`（最低 0）
- `int EffectivePDEF` — `PDEF + 兵種強化 + DefenseUp - DefenseDown`（最低 0）
- `int EffectiveMDEF` — `MDEF + 兵種強化 + DefenseUp - DefenseDown`（最低 0）
- `int TotalEvasion` — `BaseEvasion + 兵種強化 + EvasionUp - EvasionDown`（100 以上で必中回避）
- `bool IsAlive` — `BaseUnit.CurrentHP > 0 && BaseUnit.State != Dead`
- `bool IsFrontRow` / `IsBackRow` — SlotIndex から判定
- `bool IsParalyzed` — Paralysis 1スタック以上で行動不能
- `bool IsFullyFrozen` — Freeze スタック合計 10 以上で行動不能
- `bool IsCovering` — Cover 効果持ち かつ 麻痺・完全凍結でない
- `bool HasReviveInvalid` — 復活無効化デバフ付与中

### 2.5 バトルコンテキスト (BattleContext) - 戦闘状態の管理

- `List<RuntimeUnit> AllyUnits, EnemyUnits`
- `int InitialAllyCount` (戦闘開始時の非召喚味方ユニット数。InitializeBattleでセット)
- `int InitialEnemyCount` (戦闘開始時の非召喚敵ユニット数。InitializeBattleでセット)
- `int CurrentTurn, MaxTurnLimit` (ターン制限管理)
- `Queue<Action> ReactionStack` (反撃や自爆などの割り込み行動を積むキュー)
- `PhaseState CurrentPhase`

---

## 3. 技とスキルの構造 (Waza & Skill)

### 3.1 技 (Waza) - アクティブスキル

> **データ駆動化（VSプロト Phase 0 以降）**：本セクションのデータは **`WazaDefinition` (POCO) ＋ `WazaDefinitionSO` (ScriptableObject)** で外部化し、`WazaCatalog.Get(id)` 経由で実体化する（§9 参照）。
>
> Phase 0 でクロージャ → ID 化された 2 プロパティ：
> - `CalculateBaseDamage` (`Func<...>`) → `DamageFormulaId` (string) + `DamageFormulaParams` (Dictionary)。`DamageFormulaRegistry.Get(id)` で実装を解決
> - `TargetingCondition` (`Func<...>`) → `TriggerConditionId` (string) + `TriggerConditionParams` (Dictionary)。`TriggerConditionRegistry.Get(id)` で実装を解決

- `string WazaId, Name`
- `WazaCategory Category` (Attack / Heal / Buff / Debuff の区分・AI の支援行動選択ロジックで使う・§4.1)
- `bool IsPhysical` (物理か魔法か)
- `Element WazaElement` (無属性可)
- `int SPD, Cooldown, HitCount` (HitCountは多段攻撃の回数)
- `int MaxUsesPerBattle, CurrentUses` (回復制限等のための使用回数)
- `string TriggerConditionId` (発動条件 ID・`TriggerConditionRegistry` で実装解決・例：「自HP50%以下」)
- `IReadOnlyDictionary<string, float> TriggerConditionParams` (条件パラメタ)
- `bool IsSureHit` (必中フラグ)
- `TargetingType` (単体、前衛範囲、全体など)
- `string DamageFormulaId` (ダメージ計算式 ID・`DamageFormulaRegistry` で実装解決・例：「standard_physical」)
- `IReadOnlyDictionary<string, float> DamageFormulaParams` (式パラメタ・例：`{"mult": 1.0}`)
- `List<StatusEffect> AppliedEffects` (技ヒット時に対象へ付与する状態効果テンプレ。Burn / DefenseDown / AttackUp 等。スタック上限・残ターン数を含む)
- `bool CleansesStatusAilments` (味方の状態異常を解除する治療系フラグ・燃焼/凍結/麻痺/呪い)
- `bool DispelsDebuffs` (味方の能力デバフを解除する治療系フラグ・攻撃/防御/回避ダウン)

### 3.2 パッシブスキル (Skill)

- `TriggerType` (常時、行動前、被ダメージ時、1ヒット毎、死亡時など)
- `Condition` (自身がリーダーの時、自身のHPが50%以下の時など)
- `Action<BattleContext, RuntimeUnit, ...> Effect` (ステータスアップ、リアクションスタックへの追加など)

---

## 4. バトルシステムの実装ロジック

### 4.1 ターゲット評価ロジック (評価フェーズ)

実行フェーズとは完全に分離し、以下の順でターゲットを「1体（または範囲）」決定する。

1. **技の絞り込み:** 未行動状態かつ、CDが明けており、`TargetingCondition` を満たす対象が盤面に存在する技のみを抽出。
2. **フォールバック:** 使用可能な技がない場合「通常攻撃（CD0）」を宣言。「通常攻撃を行わない」スキルを持つ場合は「待機」とする。
3. **有効ターゲットの取得（保護の原則）:** - 前衛(Slot 0,1,2)が存在すれば、対応その後衛(Slot 3,4,5)は対象外。
   - 前衛が空き（死亡）の場合、その後衛は「むき出し」としてターゲット対象に含める。
   - 前衛範囲攻撃の場合、空き枠の真後ろにいる「むき出しの後衛」も巻き込み判定とする。
4. **スコア評価と決定:** `(基礎予定ダメージ / 対象の現在HP)` が最大となるユニットを選択。**シールド量は現在HPの計算に含めない。**
5. **タイブレーク:** `Ally > Enemy` の陣営順、次に `SlotIndex` の昇順 (前衛 左>中>右 ＞ 後衛 左>中>右)。

> **支援行動（Heal / Buff / Cleanse / Dispel / 自己防御）の選択ロジックは別系統**：[`110_combat_spec.md §5`](110_combat_spec.md) を参照。回復役は通常攻撃をせず、HP90% 未満の味方への回復を最優先・対象不在時は自己防御にフォールバック。`WazaCategory` フィールドで区分する。

### 4.2 多段攻撃とヒットごとの処理フロー (実行フェーズ)

多段攻撃の「行動回数」は1回とし、処理内で「ヒット数」分のループを回す。

1. **行動開始:** 実行者の行動回数依存のスキル判定。
2. **ヒットループ開始 (1 to HitCount):**
   - **かばう判定 (直前すり替え):** ターゲット対象を「かばう」状態のユニットがいれば、対象をタンクにすり替える。複数の場合は最後に発動したものを優先。
   - **回避判定:** 対象の `TotalEvasion` が100%以上か、確率で回避。`IsSureHit` ならスキップ。回避時はダメージ0＆追加効果無効。
   - **ダメージ計算:** `Max(1, 基礎威力 - DEF) -> 割合カット適用 -> ダメージ確定`。
   - **ダメージ適用:** `CurrentShield` から優先減算し、超過分を `CurrentHP` から減算。
   - **ヒット毎のリアクション判定:** トゲ（反射）などのスキルが発動した場合、`BattleContext.ReactionStack` にアクションを積む。
   - **死亡・かばう解除判定:** このヒットでタンクが死亡した場合、超過ダメージは切り捨てる。ただし次ヒット以降のかばう効果は消失し、元のターゲットへ攻撃が続行される。
3. **ヒットループ終了**
4. **行動終了:** 実行者の行動終了依存スキル判定。

### 4.3 割り込み行動 (リアクションスタック) の処理

- メインタイムラインの1ユニットの行動（ヒットループ含む）が完全に終了した直後、`ReactionStack` に積まれた処理（反撃、自爆ダメージ等）を LIFO(後入れ先出し) または FIFO で全て消化する。
- 消化中に新たなリアクションが発生した場合もスタックに積み、スタックが空になるまでメインの行動順（SPDソート）には戻らない。

### 4.4 属性干渉と状態異常管理

- **被ダメージ時フック (`OnDamageReceived`):**
  - Fire属性の技を受けた場合: 自身の Freeze を全解除。
  - Water/Ice属性の技を受けた場合: 自身の Burn を全解除。
- **行動開始時フック (`OnActionStart`):**
  - **呪い判定:** `CurrentHP <= Stacks * 10` の場合、即死処理（HP0）を実行しアクション中断。（シールド量は無視）
  - **麻痺判定:** Paralysis を持つ場合、行動をスキップし `ParalysisIncapacitateCount` を +1。
- **ターン進行フック (`OnActionEnd` / `EndPhase`):**
  - `OnActionEnd`: Burn のスタック数依存の固定ダメージを適用。
  - `EndPhase`: 
    - Paralysis スタック減算: `自身が行動不能になった回数(Count)` を参照し、`1, 2, 4, 7, 11...` の計算式に基づいてスタックを減らす。
    - Freeze スタック減算 (-1)。
    - シールド処理: `CanCarryOverShield` が False なら `CurrentShield` を 0 にリセット。

### 4.5 死亡・復活・ロスト判定

- **HP0到達時:**
  - `ReviveInvalid` (復活無効化デバフ・光属性等) が付与されている場合、即座に完全死亡（ロスト扱い）。
  - 上記がなく、`CurrentReviveCount > 0` の場合、回数を消費してHPを割合回復（アンデッド処理）。行動権は次ターンから。
- **ロスト時の処理:** 完全死亡が確定した場合、`LostProcessor.ProcessPermanentLost` が以下をすべて担う（Single Source of Truth）。
  - `Unit.State` を `Dead` に設定する。
  - `EquippedGear` が null でなければ、即座に `CommanderData.EquipmentInventory` へ返還し、`EquippedGear` を null にする。
  - `Unit.CurrentHP` を 0 に設定する（冪等）。
  - 戦闘死亡フロー（`ActionExecutor.OnUnitDied` 経由）・撤退フロー（`RetreatSystem` 直接呼び出し）の両方で使用する。

### 4.6 勝敗判定と評価 (Battle Evaluation)

`EndPhase` 終了後（または即時）に判定。`BattleResult` は5値（§2.1）：`PerfectVictory` / `AdvantageousVictory` / `MarginalDefeat` / `CrushingDefeat` / `None`。

- **敵全滅:** `PerfectVictory` (完勝・報酬増、高レア排出)
- **味方全滅:** `CrushingDefeat` (完敗)
- **ターン制限到達時:**
  - 損耗差で判定。`(InitialEnemyCount − 生存非召喚敵数) > (InitialAllyCount − 生存非召喚味方数)` なら `AdvantageousVictory` (辛勝・通常報酬)。
  - 損耗差が同等以下なら `MarginalDefeat` (惜敗)。完敗ほど厳しいペナルティではないが敗北扱い。
  - 撃破数はターンごとに累積せず、戦闘終了時点のユニット数差分として算出する。
  - いずれの場合も生存味方のHPは維持。

---

## 5. 指揮官介入システムの実装

### 5.1 待機とポーズ制御

- ターン終了時 `PhaseState.End` の直前に `PhaseState.InterventionStandby` へ移行。
- ロジック側でタイマーを起動（2〜3秒）。タイマー稼働中にUIから入力（アイテム使用等）を受け取った場合、タイマーを停止（ポーズ状態）する。
- 介入処理完了後、またはタイマー満了で次ターンへ進行。

### 5.2 撤退としんがり (Retreat)

- 撤退イベント発火時、引数として指定された `RuntimeUnit`（生存中かつ出撃中であること）を対象に、`LostProcessor.ProcessPermanentLost` を呼び出し `State = Dead`・`CurrentHP = 0`・装備返還を実行する。
- その後、`BattleContext.Result` を `Defeat` に設定し、`CommanderData.AccumulatedFailures` を +1 する。
- バトルループの中断と画面遷移は呼び出し元（`BattleManager` 等）の責務とする。

---

## 6. 特殊仕様の処理方針

### 6.1 奇襲 (Surprise Attack) と 警戒 (Vigilance)

- 戦闘開始の初期化処理直前（`PhaseState.Start` の前）に自軍奇襲 vs 敵軍警戒の確率判定を行う。
- 奇襲成功時、敵の配置スロットにおいて `SlotIndex` 0と3、1と4、2と5 のユニット参照をスワップする。

### 6.2 トークン召喚 (Summon)

- 戦闘中に動的に `RuntimeUnit` を生成し、空きスロットに配置する。
- 召喚されたターンは `HasActedThisTurn = true` とし、次ターンからタイムライン（SPDソート）に参加させる。戦闘終了時に消滅。
- 召喚時に `IsSummoned = true` を設定する。これにより優勢勝利の撃破数判定の生存数カウントから除外され、召喚ユニットの有無が勝敗評価に影響しない。

---

## 7. カスタマイズと成長システム

### 7.1 経験値とレベルアップ

- 非戦闘時、`TotalExpItems` を消費して強化。必要消費数はLv1→2が2個・Lv2→3が3個・Lv3→4が4個・Lv4→5が5個。
- Lv2〜4: `AvailableUpgrades` から1つを選択し `AppliedUpgrades` へ移行。`ApplyEffect` を実行し永続データを更新。強化選択のコールバックが `null` を返した場合はレベルアップを保留し、EXP消費・レベルインクリメントを行わずにループを中断する。
- Lv5到達時: 残りの強化肢と `IsMasteryBonus` 肢をすべて自動適用。その後 `CurrentExp` を0にリセットする（Lv5以降はEXPが蓄積されても意味がないため）。

### 7.2 控えユニットの回復

- ノードクリア（戦闘終了やイベント完了）の処理フックにて、`UnitState.Reserve` かつ生存しているユニットの `CurrentHP` を最大HPの30%回復させる（上限はMaxHP）。回復割合はゲーム設定により変更可能。

---

## 8. 段階3/4 プロト固有の補助構造

> **凍結＋退避（2026-06-08）**：本章で記述する戦闘プロト関連コード（`Stage3BattleRunner` / `Stage3RoundManager` / `Stage3CampaignState` / `Stage3Roster` 等）は **VSプロト Phase 0 で `Assets/Scripts/Core/Prototype/_old/` および `Assets/Scripts/UnityView/_old/` に退避**され、コンパイル単位から物理除外された。**戦闘プロトのシーン・テストは動作保証なし**（VSプロト USP 実証が最優先・ユーザー判断）。本章の記述は**歴史情報として残す**。本実装（[100_game_design.md](100_game_design.md) / [110_combat_spec.md](110_combat_spec.md)）への移行時は §9 のデータ駆動設計に乗せて別系統で書き直す。

本章は段階3/4 プロト（仕様 [`210_prototype_spec.md`](210_prototype_spec.md)）で本来仕様（§1〜§7）と並行して構築した補助構造の概要。本番版（仕様 [`100_game_design.md`](100_game_design.md) / [`110_combat_spec.md`](110_combat_spec.md)）への移行時には別系統で書き直し前提だが、現行プロトの実装を把握するための地図として残す。詳細は [`220_prototype_roadmap.md`](220_prototype_roadmap.md) を参照。

### 8.1 プロト戦闘実行ハーネス（`Assets/Scripts/Core/Prototype/`）

- **`Stage3BattleRunner`** — `Stage3BattleRunner.Run(allies, enemies, maxTurns, rng) → Stage3BattleReport` で1戦闘を完走させる静的ランナー。`BattleManager` / `ActionExecutor` / `StatusEffectProcessor` を組み立て、全イベントを購読してテキストログ（`report.Log`）と構造化イベント列（`report.Events`）の両方を記録する。NUnit テストとサンドボックスの両方からこのランナーを呼ぶことで挙動の一意性を保証する。
- **`Stage3RoundManager`** — 段階3 ローグライト・ミニループ（H3）の1ラウンドを解決する。複数戦線への手駒配置 → 戦線ごとの戦闘解決 → ラウンド結果集約。`StatusEffectApplied` 系の購読は配下の `Stage3BattleRunner` 経由。
- **`Stage3CampaignState`** — 段階3 キャンペーンの状態保持（ラウンド進行・本拠地HP・手駒・戦線状態）。段階2 の `CampaignState` とは別系統。

### 8.2 構造化バトルイベント（観戦ビュー駆動素材）

- **`Stage3BattleEvent`** — 1イベントの構造体（`Kind` / `Turn` / `Actor` / `Target` / `Targets` / `WazaName` / `Damage` / `HealAmount` / `TargetHPAfter` / `Result` / `SkipReason` / `EffectType`）。
- **`Stage3BattleEventKind`** — `TurnStart` / `ActionDeclared` / `HitLanded` / `Healed` / `Evaded` / `Died` / `BurnTick` / `ActionSkipped` / `StatusEffectApplied` / `StatusEffectExpired` / `BattleEnd`。
- **設計**：`Stage3BattleRunner` が戦闘実行中に各イベントを発火順で `report.Events` に追加し、観戦ビューがそれを時間軸で再生する「録画→再生」方式。倍速・スキップ・自動進行はビュー側の再生制御で実装される（戦闘ロジックは再生速度を意識しない）。
- **戦闘前付与の扱い**：Run 時点で既に付与されている効果（Cover タグ持ちユニットの永続 Cover 等）は Turn=0 の `StatusEffectApplied` として `report.Events` 冒頭に記録される。観戦ビューが Initialize 直後の状態に正しく巻き戻せる前提。

### 8.3 観戦ビュー（`Stage3BattleSpectatorView`）

- `Initialize(report, title)` → `Tick(deltaTime)` で時間蓄積 → `ApplyEvent` で1イベントずつ反映、という再生機構。`SecondsPerEvent` 設定で倍速、`StepOne()` で1イベントずつ手動送り、`SkipToEnd()` で残全消化。
- 状態追跡用 Dictionary：
  - `_hp: Dictionary<RuntimeUnit, int>` — 現在HPバー（HitLanded/Healed/BurnTick で更新）
  - `_alive: Dictionary<RuntimeUnit, bool>` — 生存フラグ（Died で false）
  - `_displayHp: Dictionary<RuntimeUnit, float>` — HPバー黄バー補間用の表示値（時間ベース追従）
  - `_activeEffects: Dictionary<RuntimeUnit, List<StatusEffectType>>` — バフ/デバフ/状態異常の表示リスト（`StatusEffectApplied`/`StatusEffectExpired` で更新・付与順を維持）
- これらは「`RuntimeUnit` の現在状態を直接参照すると戦闘終了時のスナップショットしか見えない」問題を回避するためのビュー専用の時系列追跡。

### 8.4 戦闘可視化バッジ（`Assets/Scripts/UnityView/Stage3*Overlay.cs`）

- **`Stage3UnitBadgeOverlay.Draw(rect, unit)`** — 射程3種＋かばう＋杖（回復役）の永続役割バッジ。配置画面・観戦画面の両方で同じ見た目を出すための共通描画ユーティリティ。
- **`Stage3StatusEffectOverlay.Draw(rect, IReadOnlyList<StatusEffectType>)`** — バフ/デバフ/状態異常の動的バッジ。観戦ビューが時系列追跡した結果（`_activeEffects` の List）を直接受け取る。`runtime.ActiveEffects` を直接参照する旧シグネチャは Phase 1.5-b（2026-06-04）で廃止済。
- いずれもアイコン親矩形の短辺に対する比率（`BadgeSizeRatio = 18f/96f`）でバッジサイズを算出し、画面解像度に追従する（UI スケーリング相対化）。

### 8.5 検証シーン

- `EcholosProto_Main.unity` — 本番フロー（Build Settings 登録対象）
- `Debug_BattleLogSandbox.unity`（`Stage3SandboxGUI`）— 編成自由・戦闘結果テキストログだけ表示するハーネス
- `Debug_BattleSpectatorSandbox.unity`（`Stage3BattleSpectatorSandboxGUI`）— 編成自由・観戦ビューで戦闘可視化を確認するハーネス（A10 アイコン素材差し替えや戦闘演出検証用）
- 命名規則と退避ルールは [`900_development_rules.md` §4.1/§4.2](900_development_rules.md) 参照。

---

## 9. データ駆動設計（Unit/Waza Catalog）

> **設計経緯**：戦闘プロト時代に `Stage3Roster.cs` で「C# ファクトリ関数群」としてユニット定義をハードコードしたが、これは技術的負債。本来 Unity プロジェクトではユニットや技の定義は **ScriptableObject** で外部データとして持つのが定石。本章は VSプロト Phase 0（データ駆動リアーキ）で導入する仕組みを定義する。
>
> **影響範囲**：戦闘プロトは捨て扱い（コンパイル単位から `_old/` に退避）。VSプロトと将来の本番版（[100_game_design.md](100_game_design.md) / [110_combat_spec.md](110_combat_spec.md)）は本章の仕組みに乗る。

### 9.1 設計原則

**3層構成**：

```
ScriptableObject (UnitDefinitionSO / WazaDefinitionSO)
       ↓ ロード（Unity 起動時 or テスト時に POCO 構築）
POCO     (UnitDefinition / WazaDefinition)
       ↓ Build（UnitCatalog.Get で実体化）
Domain   (Unit / Waza)  ← 既存のクラスは無変更（接続点だけ変える）
```

- **SO 層**：Unity Inspector で編集可能・Asset として保存・Addressables 連携可能
- **POCO 層**：純 C#・Editor テストで直接構築可能・SO に依存しない
- **Domain 層**：既存 `Unit` / `Waza`（[§2.3](#23) / [§3.1](#31)）と互換維持

### 9.2 なぜ中間 POCO を挟むか

- **テスト独立性**：Editor テストで `ScriptableObject.CreateInstance` を呼ばずに POCO を直接組み立て可能（実行速度・依存性最小化）
- **将来の JSON 移行点**：SO → POCO の変換層を独立させることで、将来 JSON / CSV 化する際の差し替え点を 1 箇所に集約できる
- **シリアライズの分離**：SO のシリアライズ可能型制約（`Func<...>` 不可・継承禁止）を、データ層と振る舞い層の責務分離で解決

### 9.3 Unit 定義（UnitDefinition）

ユーザーシート（2026-06-08 確定）に基づき、以下の軸を持つ：

| プロパティ | 型 | 説明 |
|---|---|---|
| `Id` | string | 一意識別子（既存 Unit.Id と同じ） |
| `Name` | string | 表示名 |
| `Element` | enum | 属性（プロト範囲では装飾的） |
| `PlacementHint` | enum: Front/Back/Any | 推奨配置（UI ヒント・システム制約なし） |
| `Range` | enum: Melee/Mid/Ranged | 射程（既存 AttackRange と同じ） |
| `CombatRoles` | List\<enum: Tank/Attacker/Support/Healer\> | 戦術的役割（複数可・例：騎士は Tank+Attacker） |
| `PrimaryAttackKind` | enum: Physical/Magical/None | 主たる攻撃種別（UI 用・Waza.IsPhysical とは別軸） |
| `AbilityLabels` | List\<string\> | UI 表示用ラベル（多段攻撃・範囲攻撃・暗殺・専守・かばう等） |
| `TargetTags` | List\<string\> | 特効判定用タグ（「Mage」等・[§3.1](#31) Waza の MageHunter 判定に使う） |
| `MaxHP / BaseATK / PDEF / MDEF / BaseSPD / BaseEvasion` | int | 基礎ステータス |
| `ImmuneToStatusAilments` | bool | 状態異常無効 |
| `Tags` | List\<string\> | 内部処理タグ（NoNormalAttack / RowCover / MageHunter 等） |
| `WazaIds` | List\<string\> | 所有 Waza の ID リスト（WazaCatalog から引く） |
| `AuraEffect` | AuraEffectData（POCO） | 置物オーラ（型・倍率・スタック上限） |
| `Enhancement*PerLevel` | int 群 | 兵種強化（既存と同じ） |

旧 `Unit.Roles`（特効判定用 List\<string\>）は本構造では `TargetTags` に名称統一する（意図を明示）。`BattleManager.MageRole` 等の文字列定数は維持。

### 9.4 Waza 定義（WazaDefinition）

既存 `Waza` クラスの `CalculateBaseDamage`（`Func<RuntimeUnit, RuntimeUnit, int>`）はデータシリアライズ不可なので、ID 化する：

| プロパティ | 型 | 説明 |
|---|---|---|
| `Id` | string | 一意識別子 |
| `Name` | string | 表示名 |
| `Category` | enum | Attack / Heal / Buff / Debuff |
| `IsPhysical` | bool | 物理/魔法 |
| `Element` | enum | 属性 |
| `SPD / Cooldown / InitialCooldown / HitCount` | int | 既存と同じ |
| `MaxUsesPerBattle` | int | 戦闘中の使用回数上限 |
| `IsSureHit / IgnoresCover / IsForcedWhenReady` | bool | 既存と同じ |
| `TargetingType` | enum | 単体/前列範囲/全体等 |
| `DefenseIgnoreRatio` | float | 防御無視率 |
| **`DamageFormulaId`** | string | DamageFormulaRegistry の ID（例：`"standard_physical"`） |
| **`DamageFormulaParams`** | Dictionary\<string, float\> | 数式パラメタ（例：`{"mult": 1.0, "hits": 2}`） |
| **`TriggerConditionId`** | string | TriggerConditionRegistry の ID（例：`"self_hp_below_50"`） |
| **`TriggerConditionParams`** | Dictionary\<string, float\> | 条件パラメタ |
| `AppliedEffectIds` | List\<StatusEffectData\> | 付帯状態効果（POCO・複製テンプレ） |
| `CleansesStatusAilments / DispelsDebuffs` | bool | 既存と同じ |

### 9.5 DamageFormulaRegistry

ダメージ計算式を「ID → 実装」で引くレジストリ。

```csharp
public static class DamageFormulaRegistry
{
    public delegate int DamageFormula(
        RuntimeUnit attacker, RuntimeUnit target,
        IReadOnlyDictionary<string, float> p);

    private static readonly Dictionary<string, DamageFormula> _map = new()
    {
        ["standard_physical"] = (a, t, p) =>
            (int)(a.EffectiveATK * p["mult"]),
        ["standard_magical"] = (a, t, p) =>
            (int)(a.EffectiveATK * p["mult"]),
        ["multi_hit_physical"] = (a, t, p) =>
            (int)(a.EffectiveATK * p["mult"]),  // hits は HitCount で別途処理
        ["heal_flat"] = (a, t, p) =>
            (int)(p["amount"] + a.BaseUnit.EnhancementHealPerLevel * a.BaseUnit.EnhancementLevel),
        ["buff_only"] = (a, t, p) => 0,
        // 追加は本番版で
    };

    public static DamageFormula Get(string id) => _map[id];
}
```

**標準セット**（プロト範囲）：
- `standard_physical(mult)` — 物理通常攻撃
- `standard_magical(mult)` — 魔法通常攻撃
- `multi_hit_physical(mult)` — 多段攻撃（双剣士）。hits は HitCount フィールドで指定
- `heal_flat(amount)` — 固定値回復（兵種強化反映込み）
- `buff_only` — 攻撃せず（バフ専門技用）

**本番版で追加見込み**：割合ダメージ、最大HP参照、距離減衰、属性相性込み等。

### 9.6 TriggerConditionRegistry

技の発動条件（`Waza.TargetingCondition`）を ID 化する同様のレジストリ。

```csharp
public static class TriggerConditionRegistry
{
    public delegate bool TriggerCondition(
        RuntimeUnit subject, IReadOnlyDictionary<string, float> p);

    private static readonly Dictionary<string, TriggerCondition> _map = new()
    {
        ["self_hp_below_ratio"] = (subject, p) =>
            subject.CurrentHP * 100 <= subject.MaxHP * p["ratio"],
        ["always_true"] = (s, p) => true,
        // 追加は本番版で
    };
}
```

**標準セット**：
- `self_hp_below_ratio(ratio)` — 自HP割合以下（騎士の50%以下発動）
- `always_true` — 無条件

### 9.7 UnitCatalog / WazaCatalog

POCO 定義から実体（`Unit` / `Waza`）を組み立てるエントリポイント。

```csharp
public static class UnitCatalog
{
    public static Unit Get(string unitId);          // 新規 Unit インスタンス生成
    public static IEnumerable<string> GetAllIds();  // 全ID列挙
}

public static class WazaCatalog
{
    public static Waza Get(string wazaId);
}
```

- `Get` は呼ぶたびに新しいインスタンスを返す（同じ兵種を複数体採用しても独立した実体）
- 内部で `UnitDefinition` をロードして `Unit` に変換、`WazaIds` を解決して `BaseWazas` を組み立てる
- `Waza.CalculateBaseDamage` は `DamageFormulaRegistry.Get(definition.DamageFormulaId)` を解決してデリゲートを差し込む

### 9.8 ディレクトリ構造（Phase 0-3 完了時点の実装）

```
Assets/
├── Resources/                                ← Resources.LoadAll の対象
│   └── Data/
│       ├── Units/
│       │   ├── unit_s3_princess.asset       (UnitDefinitionSO・17味方 + 3敵専用 = 20体)
│       │   └── ...
│       └── Wazas/
│           ├── waza_princess_slash.asset    (WazaDefinitionSO・21個・杖打ち3個除外)
│           └── ...
│
└── Scripts/
    ├── Core/
    │   ├── Models/
    │   │   ├── Unit.cs                       (PlacementHint/CombatRoles/PrimaryAttackKind/AbilityLabels/TargetTags 拡張済)
    │   │   ├── StatusEffect.cs               (public field 形式・[Serializable] 化済)
    │   │   └── Waza.cs                       (Phase 0-4 で Calculate*** → Id+Params に置換予定)
    │   ├── Data/                             (ProjectCitadel.Core.asmdef・noEngineReferences=true)
    │   │   ├── UnitDefinition.cs             (POCO)
    │   │   ├── WazaDefinition.cs             (POCO + FormulaParam struct)
    │   │   ├── DamageFormulaRegistry.cs      (ID → 計算式・標準セット5種)
    │   │   └── TriggerConditionRegistry.cs   (ID → 発動条件・標準セット2種)
    │   └── ...
    │
    └── Data/                                 (ProjectCitadel.Data.asmdef・noEngineReferences=false)
        ├── UnitDefinitionSO.cs               (SO ラッパー・[CreateAssetMenu] 付き)
        ├── WazaDefinitionSO.cs               (同上)
        ├── UnitCatalog.cs                    (Resources.LoadAll lazy init・UnitId → Unit 構築)
        ├── WazaCatalog.cs                    (同上・WazaId → Waza 構築)
        └── Editor/                           (ProjectCitadel.Data.Editor.asmdef・includePlatforms=[Editor])
            └── Stage3RosterToSoConverter.cs  (Phase 0-3 一括変換ツール・後述 §9.11)
```

**設計上の決定**：
- POCO（UnitDefinition / WazaDefinition / DamageFormulaRegistry / TriggerConditionRegistry）は **Core.asmdef（noEngineReferences=true）** に置く。純度温存。
- SO ラッパー＋Catalog は **Data.asmdef（noEngineReferences=false）** に分離。UnityEngine 依存（ScriptableObject / Resources.LoadAll）が必要なため。
- Editor ツールは **Data.Editor.asmdef（includePlatforms=[Editor]）** に隔離。本番ビルドに含まれない。
- StatusEffectData / AuraEffectData は不要と判明（既存 StatusEffect を `[Serializable]` 化 + field 形式化で対応可能）。

### 9.9 テスト戦略

- **Editor テスト**：POCO（`UnitDefinition` / `WazaDefinition`）を**直接構築**してテストする（SO 介さず・実行速度・依存性最小化）
- **SO ロードテスト**：別途「全 SO アセットが正しくロード可能か」を網羅するテストを追加（兵種抜けの構造的検出）
- **DamageFormulaRegistry 単体テスト**：各 Formula ID の数式単独テスト（クロージャに埋まっていた式を独立して検証）
- **TriggerConditionRegistry 単体テスト**：発動条件の境界値テスト

### 9.10 戦闘プロトとの関係

- `Stage3Roster.cs` 等の戦闘プロト固有ファクトリ群は **`Assets/Scripts/Core/Prototype/_old/`** に退避（[`900_development_rules.md §4.2`](900_development_rules.md) ルール）
- 戦闘プロトのテスト（Stage3* 系・10ファイル超）も `_old/` 退避＝コンパイル単位から外す
- 戦闘プロトのシーン（`Debug_BattleLogSandbox` 等）は壊れた状態で残るが動作保証しない（VSプロト USP 実証が最優先）
- 戦闘プロトの仕様書（[`210_prototype_spec.md`](210_prototype_spec.md) 等 200番台）は凍結のまま触らない

### 9.11 データ移行ツール（Stage3RosterToSoConverter）

**Phase 0-3 で実装した一括変換ツール**（[`Assets/Scripts/Data/Editor/Stage3RosterToSoConverter.cs`](../Assets/Scripts/Data/Editor/Stage3RosterToSoConverter.cs)）。

**実行方法**：Unity Editor メニュー `ProjectCitadel > Data > Stage3Roster を SO に変換`

**処理フロー**：
1. Stage3Roster の全 17 味方兵種＋3 敵専用兵種の `Unit` インスタンスを生成
2. 各 Unit のステータス／タグ／AuraEffect／Enhancement* を `UnitDefinition` POCO に転記
3. 内蔵マップから分類軸（PlacementHint / CombatRoles / PrimaryAttackKind / AbilityLabels）を付与
4. 杖打ち3個（heal_staff / med_staff / buf_staff）を除外＋NoNormalAttack タグ追加（攻撃しない化）
5. 各 Waza を `WazaDefinition` POCO に転記、`DamageFormulaId` + Params を内蔵マップから付与
6. SerializedObject 経由で SO アセットに上書き保存（冪等）

**内蔵マップの位置付け**：
- `UnitMetadataMap`：シート由来のメタ情報（PlacementHint 等）。Stage3Roster には無い情報なのでコンバータ内で定義
- `WazaFormulaMap`：DamageFormulaId と Params。Stage3Roster のクロージャ（`Phys/Magic/HealW/BuffW` ヘルパー）からは式・mult を抽出不可なので明示記述
- これらは「Phase 0-3 を冪等に再実行可能」にするためにコンバータ内に保持

**Phase 0-5 完了後の扱い**：
- 本ツールは Stage3Roster.cs に依存しているため、Phase 0-5 で Stage3Roster を `_old/` 退避するタイミングで**本ツール自体も `_old/` 退避**する。
- 以降は SO アセット直接編集（Inspector）か、新規データ追加用の別ツール（本実装フェーズで再設計）で対応する。
