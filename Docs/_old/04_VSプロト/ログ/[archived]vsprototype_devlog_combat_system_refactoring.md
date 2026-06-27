# Echolos VSプロト 戦闘システム再設計 devlog 〔凍結〕

> **【アーカイブ通知】2026-06-16**
> VSプロトの戦闘システム大改修時のClaude作業計画とログ
> 記録として残すが参照はしないこと。

> **2026-06-15 凍結**：Phase R-5-1 完了 = 戦闘システムリファクタリング完了に伴い、本書は凍結。
> Phase R-4（UI 再設計）／Phase R-6（バランス調整）／試遊フィードバック対応など以降のセッション作業ログは
> [912_vsprototype_devlog_2.md](912_vsprototype_devlog_2.md) に書く。本書は履歴参照専用。
>
> ---
>
> 本書は VSプロト戦闘システム再設計の **実装計画・作業ログ・設計判断メモ**。
> Claude 用の作業ノート（900 番台＝ [900 §8.7](900_development_rules.md) で「経緯ログ自由・仕様書一括修正対象外」と明文化）。
> 仕様の SSoT は [320_vsprototype_combat_spec.md](320_vsprototype_combat_spec.md)。本書は実装側のメモ。
>
> **2026-06-13**：R-0 現実装調査完了。本書 §2 〜 §6 は実コード基準で更新済（想定ベースの記述は §9 想定 vs 実コードの差分に集約）。

---

## 1. 経緯

### 1.1 きっかけ（2026-06-13 セッション）

バランス調整（大槌兵・巫女・軍師のアッパー、司祭の下方、忍者の HP 調整）を進めていく中で、以下のパターンに気づいた：

- ユニット A が強い → アンチユニット B を追加 → B が出ないラウンドは A 一強 → 結局「引けたか引けなかったか」
- 6v6 と 6v6 BackTank で同じユニットの勝率が大きく揺れる（サムライ +1.8pt → +31.4pt）→ 編成構造で勝率が決まり、ユニット単体の強さは二の次
- 軍師の「状況依存技 2 個 + オーラ」はバフ調整しても本質的に機能しない（敵バフ・味方デバフが発生しない盤面が多い）

つまり **「カウンター構造」は本質的にローグライト×ドラフトと相性が悪い**。「対策ユニットを引けない＝詰む」設計で、しかも複雑なルールを把握しないとプレイヤーは負けた理由がわからない。

### 1.2 再設計議論（A-E）

A: 属性 / B: 配置体数 / C: 配置 ATK 補正 / D: 環境項（地形）/ E: 反撃 の 5 軸を順に議論して固めた。

**主要な決定**：

| 軸 | 決定 |
|---|---|
| A 属性 | 火（瞬発攻撃）/水（持久全体）/補助の 3 属性、各 6 体構成、2/4/6 段階発動 |
| B 配置体数 | VSプロト R 別配置体数（毎 R +2）維持。6 体一列・配置数 < 6 は詰めて一列 |
| C 配置 ATK 補正 | 近接：100/100/95/85/75/65、遠隔：最後尾からの距離で対称 |
| D 環境項 | 列地形 × 層別強度・ランごとランダム・敵タンクのみ追従で編成設計簡単化 |
| E 反撃 | 近接限定・同時解決・チェインなし・共通フォールバック・各 hit ごと反撃 |

仕様詳細は [320_vsprototype_combat_spec.md](320_vsprototype_combat_spec.md) を SSoT とする。

### 1.3 引き返し判断の根拠

- **個人完遂優先**（[feedback_no_technical_debt] / [feedback_realistic_ambition]）：技術的負債は許容しない、コンテストより個人完遂が上位
- **プロト段階だから引き返せる**：本番実装に入る前なので、捨てるコードの量が許容範囲
- **バランス調整地獄に半年突っ込んで「結局カウンター構造が悪い」と気づくよりは、今ここで方針転換した方が総工数が小さい**

---

## 2. 全体スコープ（R-0 調査反映済）

### 2.1 捨てる対象

| 区分 | 対象 | 備考 |
|---|---|---|
| Domain Tag 全廃 | `RowCoverTag` / `MageHunterTag` / `InfiltratorTag` / `AntiHealPassiveTag` / `MageRole` / `LonerTag` | BattleManager の const として定義済 |
| Domain Waza 旧フラグ全廃 | `IsPhysical` / `WazaElement` / `DefenseIgnoreRatio` / `IgnoresCover` / `IgnoresFrontRowGuard` / `SameRowSplashMultiplier` / `DispelsBuffs` / `DispelsDebuffs` / `TargetingCondition` | Waza クラス内に集中。**`CleansesStatusAilments` は維持**（状態異常解除の唯一の経路に） |
| 属性連動経路全廃 | `HitOutcome.DamageElement` プロパティ／`StatusEffectProcessor.HandleHitLanded` 内の属性連動解除（火→凍結解除、水/氷→燃焼解除） | 属性は「シナジー × 地形補正」の 2 役割だけに純化。状態異常解除は Cleanse 系 Waza のみ |
| 召喚ユニット概念 | `RuntimeUnit.IsSummoned` / `BattleContext.GetAliveOriginalAllies` / `GetAliveOriginalEnemies` / `AllyKillCount` / `AllyDeathCount` の召喚除外ロジック | 旧戦闘で発火イベントなし＝集計枠だけ。本番で必要になったら Skill `TriggerType.OnDeath` 等で再導入可能。今は削除して BattleContext 簡素化 |
| 負傷状態 | `Unit.InjuryStatus` / `InjuryState` enum / 戦闘終了後の Active→Injured→Resting→Active 遷移 | フル版では治療コスト（お金）で代替予定のため、プロトで負傷システムを残す投資価値が低い |
| Domain StatusEffect 1 種廃止 | `Cover`（かばう） | 他の状態異常（Burn/Freeze/Paralysis/Curse/ReviveInvalid）は維持可能 |
| ConditionalBuffProcessor 具体派生 4 種 | `LonerWolfConditionalProcessor` / `PrinceDarkAuraConditionalProcessor` / `PendantConditionalProcessor` / `OffenseDefenseLinkConditionalProcessor` | 基底クラスは流用（§2.3 参照） |
| Unit ステータス分離 | `Unit.PDEF` / `Unit.MDEF` / `Unit.EnhancementPDEFPerLevel` / `Unit.EnhancementMDEFPerLevel` | `Unit.DEF` / `Unit.EnhancementDEFPerLevel` に統合 |
| 射程 enum | `AttackRange`（Melee/Mid/Ranged 統合形式） | `AttackKind`（Melee/Ranged）+ `TargetingDirection`（FromFront/FromBack）の 2 軸に分解 |
| Roster | `AlliesRoster` / `EnemiesRoster` / `BossesRoster` の旧 28 体 | 新 17 体で置換 |
| Data SO | 旧 Unit SO / Waza SO / DraftPool SO | 新規生成 |
| Tests | `MatchupTests` / `AssassinTests` / `VSProto_InfiltratorTests` / `VSProto_RowSplashTests` / `VSProto_Pendant*` / `VSProto_OffenseDefenseLink*` / `VSProto_LonerWolf*` / `VSProto_Mercenary*` / `VSProto_Bridget*` / `VSProto_Prince*` / `Step1-5_*` / `Prototype_*` 等 約 20 件 | §5 で詳細仕分け |
| Editor 拡張 | 検証ツール 4 種（Weak/Mid/SixVsSix/SixVsSixBackTank） + BalanceReportTool 定数置き場 | Phase R-5 で新規作成 |
| TargetEvaluator 旧機能 | かばう判定（`ResolveCoverTarget`）/ 列単位保護（`IgnoresFrontRowGuard` 経路）/ 特効判定（`GetDamageMultiplier`） | `TargetEvaluator` クラス自体は残す（§2.3）、内部の旧機能だけ廃止 |
| 仕様書 | `320_vsprototype_unit_lineup.md` | 後日 `_old/` アーカイブ |

### 2.2 残す対象

| 区分 | 対象 | 備考 |
|---|---|---|
| Domain ラン進行 | 領地マップ / `MapNode` / 自領陥落 / 本拠地連続防衛（[310 §1](310_vsprototype_spec.md)） | 無関係 |
| Domain 戦闘フロー骨格 | `BattleRunner.Run()` シグネチャ / `BattleReport` / `BattleEvent` / `BattleEventKind` / `HitOutcome` 構造 | UseCase / Presentation が広く参照 |
| `BattleContext` | `ReactionStack` キュー / `AllyKillCount` / `AllyDeathCount` / `IsSummoned` 集計 / `IsAdvantageousVictoryCondition` 判定 | **反撃枠・引き分け非対称評価がほぼ既存実装で対応可能** |
| `BattleResolver` デリゲート | UseCase 側からの差し替え機構 | **新戦闘は実装差し替えだけで UseCase 側無改修** |
| `BattleManager` 骨格 | `InitializeBattle` / `ProcessTurn` / `ProcessReactionStack` / イベント発火（`OnStartPhase` / `OnActionStart` / `OnActionExecuting` / `OnActionSkipped` / `OnActionEnd` / `OnEndPhase`）/ `ComputeBaseDamage`（単一経路） | クラスは残し、内部ロジックを差し替え |
| `TargetEvaluator` 骨格 | `DeclareAction` の forced / support / self-guard 経路 | **完全削除でなく改修**（旧ターゲット選定だけ差し替え） |
| `ActionExecutor` 骨格 | `OnHitLanded` / `OnHitEvaded` / `OnUnitDied` / `OnHealed` / `OnActionResolved` イベント | 反撃は `OnHitLanded` で `ReactionStack` に積む |
| `StatusEffectProcessor` 骨格 | 燃焼・凍結・麻痺・呪い・アンデッド復活処理 | 物理/魔法分離はそもそも無し（既に統合済） |
| `ConditionalBuffProcessor` フレームワーク | 基底クラス（Hook 種別宣言・`DispatchRefresh` 再帰ガード・`Refresh` メソッド） | **新シナジー実装の格好の基盤** |
| `Skill` クラス | パッシブ実装基盤（`TriggerType` / `Condition` / `Effect`） | 反撃の特殊効果実装に流用可能 |
| `StatusEffect` + `BuffCategory` | Triggered / Persistent / Conditional / StatusAilment / ActionGuard の 5 分類 | Conditional がシナジー実装に最適 |
| `BattleResult` enum | PerfectVictory / AdvantageousVictory / MarginalDefeat / CrushingDefeat の 4 段階 | 判定式の変更のみで引き分け非対称評価対応可 |
| UseCase | 内政（召集・自動加入・兵種強化）・メタ通貨・メタ強化・StoryProgress | 戦闘外 |
| Presentation | MapGUI / InteriorGUI / メタ拠点 UI / `VSPrototypeBattleGUI`（BattleReport.Events 再生） | 戦闘 UI は再設計だが BattleEvent 構造は流用 |
| Data | StoryScene SO / MetaUpgrade SO / MetaRewardFormula SO | 戦闘外 |
| アセット | IconRegistry / BackgroundRegistry / スチル投入機構 | 戦闘外 |
| 仕様書 | 310 / 330 / 500 / 900 / 920 / 390（バランスメモはリセット） | 戦闘部分のみ追従 |

### 2.3 新規実装対象

| 区分 | 対象 | 備考 |
|---|---|---|
| ステータス再定義 | `Unit.DEF` 統合 + 属性プロパティ拡張（`Element` enum は既存・単独属性のまま使用） | §4 Step 2-1 |
| 射程 enum 分解 | `AttackKind`（Melee/Ranged）+ `TargetingDirection`（FromFront/FromBack）の 2 軸新設 / 旧 `AttackRange` 廃止 | §4 Step 2-1 |
| ダメージ式 | `(ATK × 倍率 + 定数) × √HP / (DEF + 定数 + 環境項)` | `BattleManager.ComputeBaseDamage` を差し替え |
| 配置 ATK 補正 | AttackKind=Melee は slot 番号 / AttackKind=Ranged は最後尾基準の補正カーブ | ダメージ式に乗算組込 |
| 反撃 Waza | `Unit.CounterWaza` プロパティ + 共通フォールバック `Waza.DefaultCounter` + `WazaCategory.Counter` enum 値追加・攻撃側＆被弾側両方 AttackKind=Melee 限定発動 | `ReactionStack` 流用 |
| 属性シナジー | `FireSynergyProcessor` / `WaterSynergyProcessor` / `SubSynergyProcessor` 派生クラス・3/5 体は線形補間 | `ConditionalBuffProcessor` 継承 |
| シールド | `StatusEffectType.Shield` 新設 + Conditional カテゴリで表現・WaterSynergyProcessor が動的管理 | ActionExecutor の被弾処理に Shield 消費分岐追加 |
| 地形システム | `TerrainKind` enum + `BattleContext.Terrain` + `MapNode.Terrain` | ダメージ式の環境項 |
| 引き分け非対称評価 | `BattleContext.IsAttackingSide` フラグ追加・「攻め側 0 撃破=負け / 守り側 0 被撃破=勝ち」判定式に変更 | 既存集計流用 / SPD タイブレークでも IsAttackingSide を共用 |
| SPD タイブレーク | 陣営間：IsAttackingSide 優先 / 同一陣営内：slot 番号昇順 | 完全決定論・ランダム性排除 |

---

## 3. 着手順（推奨フェーズ分け）

| Phase | 内容 | 完了基準 |
|---|---|---|
| **Phase R-1** | 退避先準備＋責務分離スケルトン（並走方式） | 退避先 asmdef（`Echolos.Domain.OldBattle`）準備済・旧戦闘コードは並走稼働継続・`BattleLogFormatter` / `BattleEventRecorder` / `BattleAssembly` 作成済 |
| **Phase R-2** | 新戦闘基盤実装（Step 2-1 〜 2-7） | 仮ユニット 2-3 体で「殴り合い＋反撃＋シナジー＋地形」が動く最小構成 |
| **Phase R-3** | 新ユニット実装（火/水/補助/固有） | 17 体程度の Roster と Waza が揃う |
| **Phase R-4** | UI / 表示系再設計 | 戦闘 UI / 配置 UI / マップ地形バッジが動く |
| **Phase R-5** | 検証ツール新設＋本流統合 | シナジー / マッチアップ / 地形バイアス各検証ツール・旧戦闘コードを `_old/` 物理移動・`Refactored` namespace 削除＋本流統合・500 §13.2 追従 |
| **Phase R-6** | バランス調整・仕様書追従 | 390 リセット・310 §1.4 改修・110 本番版反映・500 §3.6 / §4.4.5 追従 |

### 3.1 並行作業の余地

- **Phase R-3（ユニット仕様策定）は R-2 と並行可**：仕様策定（数値設計）はコード作業を待たない
- **Phase R-4（UI）は R-2 完了後すぐ着手可**
- **Phase R-5（検証ツール）は R-3 完了後**

### 3.2 並走方式（R-0 で確証）

旧戦闘システムを `_old/` 退避してから新戦闘を書く方式ではなく、**旧 BattleManager を残しつつ新 `BattleManagerV2` を別ファイルで並行実装**する方式が現実的。理由：

- `BattleRunner.Run` 自体が 411 行と大きい。最小スタブで動作させるよりは「実コードの新版を別ファイルで作る」方が安全
- UseCase 側は `BattleResolver` デリゲート経由で抽象化されているため、`BattleRunner.Refactored.BattleRunner.Run` のような並走実装を用意して、テスト後に旧 `Run` を差し替えるだけで切り替え可能
- 旧テストを動かしながら新テストを追加できる

→ Phase R-1 の「解体」は **旧コードを `_old/` に移動するのは Phase R-2 完了後**（新戦闘が完成してから）に後ろ倒しが安全。

---

## 4. 各 Step 詳細（R-0 反映済）

### Phase R-1：解体・並走骨格作成

#### R-1-1：新規 asmdef 作成（退避先準備）

| 対象 | 操作 |
|---|---|
| `Assets/Scripts/Domain/Battle/_old/` 新規ディレクトリ | `Echolos.Domain.OldBattle` asmdef を新設、defineConstraints: `["ECHOLOS_OLD_BATTLE"]`、autoReferenced: false、noEngineReferences: true |
| `Assets/Tests/Editor/Domain/_old/` 既存 | `Echolos.Tests.Domain.Old` の references に `Echolos.Domain.OldBattle` 追加（旧戦闘テスト退避用） |

#### R-1-2：新 Domain.Battle の場所準備（別 namespace 方式）

| 対象 | 操作 |
|---|---|
| `Assets/Scripts/Domain/Battle/Refactored/` 新規ディレクトリ | 新コードを `Echolos.Domain.Battle.Refactored` namespace で書く。クラス名は `BattleManager` / `TargetEvaluator` / `ActionExecutor` / `StatusEffectProcessor` / `BattleRunner` のまま（namespace で旧と区別） |
| 旧 `Echolos.Domain.Battle` 配下 | 触らない。旧コードは旧 namespace のまま動作継続 |
| 並走中の呼び出し側 | 旧 `BattleRunner.Run`（旧 namespace）を `VSPrototypeRoundManager` から呼び続ける。新 `Refactored.BattleRunner.Run` は検証ツール経由でテスト |
| Phase R-5 完了時 | namespace `.Refactored` を一括削除（機械的置換）＋旧コードを `Battle/_old/` 物理移動＋呼び出し側を新 namespace に統一 |

**並走対象の 5 クラス**：BattleRunner / BattleManager / TargetEvaluator / ActionExecutor / StatusEffectProcessor

**`ConditionalBuffProcessor` 基底クラス**：旧 namespace のまま流用（基底は変えない）。新シナジー Processor は `Echolos.Domain.Battle.Refactored.Conditional` namespace で書く。

**旧 Conditional 派生 4 種**（LonerWolf / PrinceDarkAura / Pendant / OffenseDefenseLink）：並走させず最初から `_old/` 退避候補。ただし旧 BattleRunner.Run が結線するため、旧 BattleRunner.Run を最後に退避するまで残す。

#### R-1-3：ログ・イベント記録の責務分離骨格作成（負債解消）

旧 `BattleRunner.Run` は 411 行のうち約 2/3（260 行）がログ生成・イベント記録の混在。新戦闘ではこれを分離する：

| 新規クラス | 責務 | 切り出し元 |
|---|---|---|
| `Assets/Scripts/Domain/Battle/Replay/BattleLogFormatter.cs` 新設 | テキストログ生成（FormatSingleOutcome / FormatGroup / AppendOutcomesLog / SurvivorSummary / LineupSummary / ResultLabel / Name ヘルパ / FormatEffectWithSource） | 旧 BattleRunner.Run 内ローカル関数 約 150 行 |
| `Assets/Scripts/Domain/Battle/Replay/BattleEventRecorder.cs` 新設 | 構造化イベント列（`BattleEvent`）の生成。BattleManager / ActionExecutor / StatusEffectProcessor / RuntimeUnit のイベントを購読して BattleReport.Events に記録 | 旧 BattleRunner.Run 内リスナー登録 約 110 行 |
| `Assets/Scripts/Domain/Battle/Replay/BattleAssembly.cs` 新設（任意） | BattleManager + TargetEvaluator + ActionExecutor + StatusEffectProcessor + ConditionalBuffProcessor 群の結線 | 旧 BattleRunner.Run 内組み立て 約 30 行 |

**Refactored.BattleRunner.Run 本体の目標サイズ**：80-100 行（戦闘実行ループ＋ Assembly / Formatter / Recorder の利用）

**設計判断**：
- 旧 `BattleRunner.Run` は触らない（並走方式）。新 `Echolos.Domain.Battle.Refactored.BattleRunner.Run` で責務分離した実装を行う
- 旧ローカル関数のロジックは新クラスに「再現」する（コピー＋テスト整備で品質保証）
- `BattleLogFormatter` は **戦闘ロジック非依存**（HitOutcome / RuntimeUnit / Element を受け取って文字列を返すだけ）→ 単体テスト可
- `BattleEventRecorder` も **戦闘ロジック非依存**（BattleManager / ActionExecutor を受け取って購読＋ BattleReport に書き出すだけ）→ 単体テスト可
- 後方互換：BattleReport.Events / Log の構造は維持。Presentation 側（VSPrototypeBattleGUI）は無改修で動く

**着手順**：

1. `BattleLogFormatter` / `BattleEventRecorder` の **インタフェースだけ先に作る**（中身は空 or 旧コードからのコピー）
2. 単体テスト整備（旧ログ文字列との一致を検証）
3. `BattleRunner.Refactored.BattleRunner.Run` の骨格を作る（Assembly + Formatter + Recorder を利用する 100 行未満の実装）
4. Phase R-2 の各 Step で新戦闘ロジックを Refactored.BattleRunner.Run に組み込む

#### R-1-4：仕様書側の追従

- 310 §1.4.1 旧プール仕様は新戦闘に合わせて改修（地形ベース）→ Phase R-6
- 390 のバランスログは「旧戦闘期の記録」としてアーカイブ表記＋新戦闘用にリセット → Phase R-6

### Phase R-2：新戦闘基盤

#### Step 2-1：ステータス再定義

| ファイル | 改修内容 |
|---|---|
| `Unit.cs` | `PDEF` / `MDEF` 削除、`DEF` プロパティ追加。`EnhancementPDEFPerLevel` / `EnhancementMDEFPerLevel` を `EnhancementDEFPerLevel` に統合。`Range` プロパティ（AttackRange）を `AttackKind` + `TargetingDirection` の 2 プロパティに分解。`Element UnitElement` は単一属性のまま使用 |
| `RuntimeUnit.cs` | `EffectivePDEF` / `EffectiveMDEF` を `EffectiveDEF` に統合 |
| `Enums.cs` | `StatusEffectType.Cover` を削除（または非推奨マーク）+ `StatusEffectType.Shield` 新設（水属性シナジー用）。`AttackRange` enum 廃止 + `AttackKind`（Melee/Ranged）/ `TargetingDirection`（FromFront/FromBack）2 enum 新設。物理/魔法分離は **StatusEffect レベルでは既に統合済（DefenseUp/DefenseDown 1 種）** で改修不要 |
| `Waza.cs` | `IsPhysical` / `WazaElement` 削除（物理/魔法・属性の区別撤廃）。`CleansesStatusAilments` は維持（状態異常解除の唯一の経路に） |
| `HitOutcome.cs` | `DamageElement` プロパティ削除（属性ログ廃止） |
| `StatusEffectProcessor.cs` | `HandleHitLanded` 内の属性連動解除処理（火→凍結解除、水/氷→燃焼解除）を削除。Cleanse 系 Waza 経由でのみ状態異常解除 |
| `Unit SO` / `Waza SO` | フィールド変更に追従。`SoAssetGenerator` 改修で再生成 |

**依存**：なし
**コミット粒度**：(1) Enums.cs（StatusEffectType.Cover 削除）(2) Unit/RuntimeUnit（DEF 統合）(3) Waza（IsPhysical 削除）(4) SO 再生成 の 4 段階

#### Step 2-2：ダメージ計算式実装

```
ダメージ = (ATK × 倍率 + 定数A) × √HP / (DEF + 定数B + 環境項)
```

| ファイル | 改修内容 |
|---|---|
| `BattleManager.ComputeBaseDamage` | 新ダメージ式に差し替え。**単一経路化済みなので 1 箇所変更で評価フェーズ＋実行フェーズ両方に反映** |
| `Domain.Formula` 配下の `DamageFormula` 群 | 旧 mult ベースの計算式群は新計算式の補助関数として再利用 or 廃止 |

**依存**：Step 2-1
**コミット粒度**：基本式実装 + 単体テストで 1 コミット

#### Step 2-3：配置 ATK 補正

| ファイル | 改修内容 |
|---|---|
| `Domain.Battle/PositionAtkCorrection.cs` 新設 | 近接：`slot` 番号で固定カーブ／遠隔：最後尾からの距離 |
| `BattleManager.ComputeBaseDamage` | ATK 部分に補正を乗算 |

**依存**：Step 2-2
**コミット粒度**：補正カーブ実装 + テスト（近接・遠隔・少人数の各ケース）で 1 コミット

#### Step 2-4：反撃システム

| ファイル | 改修内容 |
|---|---|
| `Unit.cs` | `CounterWaza` プロパティ追加（null = 共通フォールバック） |
| `Waza.cs` | `WazaCategory.Counter` enum 値追加・`Waza.DefaultCounter` 静的定義（倍率 1.0・属性なし・riders なし） |
| `ActionExecutor.cs` | 既存 `OnHitLanded` イベントで `ReactionStack` に反撃 Action を積む（**枠は既存**：コメントに「トゲ反射」と記載）。反撃 Action は攻撃側＆被弾側両方 Melee のみ発火・死亡時は発火しない・チェインなし |
| `BattleRunner.Run` または ログ集約 | 反撃の同時解決をログ 1 行で表示（「A の攻撃 → B に xx ダメージ／B の反撃で A に xx ダメージ」） |
| `BattleEventKind` | 反撃ログ用に新規 enum 値検討（`CounterAttack`）or `ActionResolved.Outcomes` に counter 情報を含める |

**依存**：Step 2-2 / 2-3
**コミット粒度**：(1) Unit.CounterWaza + フォールバック (2) ActionExecutor で ReactionStack 積み (3) ログ集約改修 の 3 段階

**注意**：
- 死亡時反撃なし（順序：ダメージ計算 → HP 減少 → 生存判定 → 生存ならカウンター）
- シールド吸収でも反撃発動（攻撃された事実は変わらない）
- 多段攻撃の各 hit ごとに反撃発動

#### Step 2-5：属性シナジー（**大** 評価・Phase A/B/C 分割）

属性シナジーは「バフ二系統ルール」（基礎ステータス加算 vs 最終出力割合・[320 §1.4](320_vsprototype_combat_spec.md) / §6.15）を実装で具現化する Step。最終ダメージ計算順序（与ダメ → 被ダメ → クリ）と防御側キャップ 80% は仕様 320 §1.4.1 に明記。Phase A/B/C に分割して進める。

##### Phase A：シナジー共通基盤＋火属性

| ファイル | 改修内容 |
|---|---|
| `Enums.cs` | `StatusEffectType.OutgoingDamageUp` 追加（Magnitude=% 値・Stacks 比例の加算スタック） |
| `Refactored.Synergy.SynergyConstants.cs` 新規 | 火 5/10/20/35/50%（2/3/4/5/6 体段階）＋バフ対象数 1/1/2/2/2＋水 DEF 10/15/20/25/30＋水 Shield 0/0/1/2/3＋ AuraSourceId / SourceAbilityName 定数を集約 |
| `Refactored.Synergy.ElementCounter.cs` 新規 | 純関数：陣営の Element 別生存体数カウント |
| `Refactored.DamageModifier.cs` 新規 | 純関数：`ApplyOutgoingMultiplier(rawDamage, attacker)` = Σ(Magnitude × Stacks) / 100 を加算スタックで `1 + bonus` 乗算（被ダメ%・クリは後送り） |
| `Refactored.Synergy.FireSynergyProcessor.cs` 新規 | `ConditionalBuffProcessor` 継承。Hooks: BattleStart / UnitDied / BuffApplied / BuffRemoved。両陣営独立カウント → ATK 降順 × SlotIndex 昇順タイブレーク → 上位 N 体に OutgoingDamageUp 付与・AuraSourceId 一致で差分判定（剥奪→再付与） |

##### Phase B：水属性＋シールド機構

| ファイル | 改修内容 |
|---|---|
| `Refactored.Synergy.WaterSynergyProcessor.cs` 新規 | 水属性体数 → 全員に DefenseUp（既存 enum 流用・加算固定値）と Shield（既存 enum 流用・Stacks に残数）を動的付与。AuraSourceId 2 系統（def_buff / shield）で差分判定 |
| `Refactored.ShieldConsumer.cs` 新規 | 純関数：被弾時に Shield 残数があれば Stacks -1 して Damage=0、なければ素通し。`(int absorbedDamage, bool shieldConsumed)` 構造体で返す。**反撃発動は呼び出し元責務**（Shield 吸収でも反撃するため） |

##### Phase C：補助属性 Processor は Phase R-3 へ委譲

補助属性 `SubSynergyProcessor` は **Phase R-3 で数値仕様確定と同時に実装＋テストを 1 セット**で作成する。Step 2-5 段階で空クラス＋ TODO コメントのプレースホルダを置く方式は [900 §7.9]（未来計画コメント禁止）／[feedback_no_technical_debt]（空クラスは負債）と矛盾するため採用しない。Step 2-5 は Phase A / Phase B で実質完了。

##### 全 Phase 共通の後送り

- Refactored.BattleRunner.Run / Refactored.ActionExecutor との結線（`conditionalProcessors` 配列追加・ShieldConsumer 配線）：Phase R-2 後半 Refactored.BattleManager 組み立て時に対応
- `StatusEffectType.IncomingDamageDown` の enum 追加：呼び出し元 Processor が出るまで保留（YAGNI）
- 被ダメ% キャップ 80% / クリティカル乗算：使用箇所が出た時点で `DamageModifier` シグネチャ拡張

**依存**：Step 2-1 / 2-2
**コミット粒度**：Phase A / B / C ごとに独立コミット推奨。各 Phase でユーザー Test Runner 依頼サイクル

**注意**：
- 6 体染め時のバフ対象は 2 体のまま（数値ジャンプで差別化）
- 補助属性は単体ピックしやすい性能（具体仕様は Phase R-3 で詰める）
- `DispatchRefresh` の再帰ガードは基底クラスが提供（派生クラスは差分判定だけ気にすれば良い）
- シールド残数は **Stacks** に持たせる（`RuntimeUnit.ShieldStacks` が既存で Stacks 合計を返す方式）

#### Step 2-6：地形システム（**中** 評価）

地形補正（環境項）の純関数群と DamageFormula 分母安全クランプ。属性軸は「シナジー × 地形」の 2 役割のみ（仕様 [320 §1.1](320_vsprototype_combat_spec.md) / §5）。Refactored 純関数で完結する範囲と、BattleContext / MapNode / UseCase の結線が必要な範囲を分割。

##### Refactored 純関数（Step 2-6 内で実装）

| ファイル | 改修内容 |
|---|---|
| `Refactored.Terrain.TerrainKind.cs` 新規 | enum: Neutral / FireAdvantage / WaterAdvantage |
| `Refactored.Terrain.TerrainStrength.cs` 新規 | enum: Light=0（自領）/ Medium=1（敵領）/ Heavy=2（敵拠点） |
| `Refactored.Terrain.TerrainConstants.cs` 新規 | 強度別 α 値テーブル 5/10/15（暫定・インデックス = TerrainStrength 値）。バランス調整で再調整 |
| `Refactored.Terrain.TerrainBonusCalculator.cs` 新規 | 純関数：`GetTerrainBonus(Element unitElement, TerrainKind terrain, TerrainStrength strength)` → 自属性 +α / 逆属性 -α / 補助属性および中立地形は 0 |
| `Refactored.DamageFormula.cs` 改修 | **分母 1 クランプ追加**：`denominator < 1.0 → 1.0`。負の terrainBonus + DEF=0 で分母 0 以下になる除算事故を防止（Step 2-2 で見落としていた弱点・Step 2-6 で初めて顕在化） |

##### 結線（Refactored.BattleManager 組み立て時 / Phase R-3 に後送り）

| 区分 | 内容 |
|---|---|
| `BattleContext.Terrain` / `Strength` プロパティ追加 | Refactored.BattleManager 組み立て時 |
| `BattleRunner.Run` シグネチャ拡張（terrain / strength 引数） | 同上・デフォルト引数で後方互換 |
| `MapNode.Terrain` プロパティ追加 | UseCase 側で MapNode に持たせる時に対応 |
| `VSPrototypeRoundManager` ラン開始時地形ランダム化 | 全 3 属性 1 列ずつ保証する制約パターンで割り振り |
| 敵編成の地形追従 | Phase R-3 ユニット仕様策定と連動 |

**依存**：Step 2-2 / 2-5
**コミット粒度**：純関数 4 個＋ DamageFormula 修正＋テスト 13 件を 1 コミット

#### Step 2-7：引き分け非対称評価＋ SPD タイブレーク（**小** 評価）

引き分け非対称評価と SPD タイブレークを Refactored 純関数で実装。BattleContext / UseCase の結線は後送り。

##### Refactored 純関数（Step 2-7 内で実装）

| ファイル | 改修内容 |
|---|---|
| `Refactored.VictoryEvaluator.cs` 新規 | 純関数：`IsAdvantageousVictory(allyKillCount, allyDeathCount, isAttackingSide)`。攻め=1 体撃破で優勢勝利／守り=0 被撃破で優勢勝利 |
| `Refactored.SpdOrderResolver.cs` 新規 | 純関数：`OrderByTurnPriority(allies, enemies, isAlliesAttackingSide, getSpd=null)`。SPD 降順 → 陣営間タイブレーク → 同陣営内 slot 昇順。getSpd 省略時 BaseSPD、凍結等の Waza/状態異常依存ロジックは呼び出し側責務 |

##### 結線（Refactored.BattleManager 組み立て時に後送り）

| 区分 | 内容 |
|---|---|
| `BattleContext.IsAttackingSide` プロパティ追加 | 引き分け評価＋ SPD タイブレーク共用 |
| `BattleContext.IsAdvantageousVictoryCondition` 改修 | 既存「AllyKillCount > AllyDeathCount」を `VictoryEvaluator.IsAdvantageousVictory` 呼び出しに差し替え |
| Refactored.BattleManager の SPD 順決定ロジック | `SpdOrderResolver.OrderByTurnPriority` 呼び出し＋ Waza.SPD / 凍結等を加味した getSpd 関数を渡す |
| `VSPrototypeRoundManager` | `MapNode` 種別から攻め/守りを判定して `BattleContext.IsAttackingSide` にセット |

**依存**：なし（独立ロジック）
**コミット粒度**：純関数 2 個＋テスト 15 件を 1 コミット

### Phase R-3：新ユニット実装

#### R-3-1：仕様策定（数値設計）

- 火属性 6 体の HP/ATK/DEF/SPD・技の倍率・反撃 Waza の特殊効果
- 水属性 6 体の同上
- 補助属性 3 体の単体性能（メイン 2 体染め以下の強度）
- 王女・ブリジット の新仕様（属性割り当て・シナジー組み込み）

#### R-3-2：実装

- 新 `AlliesRoster` / `EnemiesRoster` の作成
- `Unit SO` / `Waza SO` の生成（`SoAssetGenerator` 拡張）
- `IconRegistry` の更新（必要なら新アイコン生成依頼）

### Phase R-4：UI / 表示系

#### R-4-1：戦闘 UI 再設計

- 6 体一列の擬似 3D 構図（現状の前列 3 + 後列 3 構図を変更）
- 攻撃方向（近接=最前 / 遠隔=最後尾）の視覚化（矢印 or アニメーション）
- 反撃の同時解決を視覚で表現（A→B 攻撃と B→A 反撃を 1 ステップで再生）

#### R-4-2：配置 UI 再設計

- 6 スロット詰めて一列の表現
- 配置 ATK 補正バッジ（slot ごとに % 表示）

#### R-4-3：マップ画面の地形バッジ

- 列ごとに地形アイコン
- 層別強度（自領 = 軽微 / 敵領 = 中 / 敵拠点 = 重）の視覚化（濃度 or 数値）

### Phase R-5：検証ツール新設＋本流統合

- シナジー段階検証ツール（火 2/4/6 体 × 水 2/4/6 体）
- マッチアップ検証ツール（染め vs 染め / 染め vs 混合）
- 地形バイアス検証ツール（列地形 × 編成属性のマッチング勝率）
- 反撃損益検証ツール（近接 vs 遠隔の DPS 差）
- **本流統合**：旧 BattleRunner / BattleManager / TargetEvaluator / ActionExecutor / StatusEffectProcessor を `Domain/Battle/_old/` 物理移動＋ `Echolos.Domain.Battle.Refactored` namespace を一括削除（機械的置換）して本流昇格＋ UseCase 側（`VSPrototypeRoundManager` 等）の `resolver` フォールバックを新 BattleRunner.Run に切り替え
- **500 仕様書追従**：§13.2「継続使用する共通資産」相当の表に「Refactored namespace 統合」行（旧 namespace → 新 namespace の対応）を追加

### Phase R-6：バランス調整・仕様書追従

- 390 のリセット＋新検証ログの開始
- 310 §1.4 敵編成プール仕様の地形ベース改修
- 110 本番版仕様への反映タイミング判断（プロト確定後にまとめて反映）
- **500 仕様書追従**（110 本番版反映と合わせて実施）：
  - §3.6 戦闘システム仕様要素 → 実装観点マッピング表：「列単位保護 × かばう」「6 段階支援行動優先度」「戦況ゲージ × 4 段階評価」「ターゲット評価 D/H スコア」等の旧戦闘前提行を新戦闘（属性シナジー / 反撃 / 地形 / 引き分け非対称評価 / 配置 ATK 補正等）に書き換え
  - §4.4.5 ConditionalBuffProcessor 実装例：旧 4 種（LonerWolf / PrinceDarkAura / Pendant / OffenseDefenseLink）を退避済みとして除去・新 3 種（FireSynergy / WaterSynergy / SubSynergy）を実装例として記載

---

## 5. 解体タスクの仕分け（R-0 で実コード確認済）

| ファイル / 区分 | 仕分け | 備考 |
|---|---|---|
| `BattleRunner.cs` | **改修**（並走→差し替え + 責務分離） | 旧 `Run`（411 行・責務混在）はそのまま残し、新 `Refactored.BattleRunner.Run`（80-100 行・責務分離済）を並走実装。Phase R-5 完了後に旧 `Run` を `_old/` へ。`InfiltratorTag` 関連の `AppendInfiltratorDeclarations` は新版で削除。詳細は §4 Phase R-1 R-1-3 |
| `BattleLogFormatter.cs`（新規） | **新設** | テキストログ生成を切り出し（FormatSingleOutcome / FormatGroup / AppendOutcomesLog / SurvivorSummary / LineupSummary / ResultLabel / Name ヘルパ）。戦闘ロジック非依存・単体テスト可 |
| `BattleEventRecorder.cs`（新規） | **新設** | 構造化イベント列（BattleEvent）の生成を切り出し。BattleManager / ActionExecutor / StatusEffectProcessor / RuntimeUnit のイベント購読 |
| `BattleAssembly.cs`（新規・任意） | **新設** | BattleManager + サブシステム結線を切り出し。Refactored.BattleRunner.Run を薄く保つため |
| `BattleManager.cs` | **改修** | const タグ群（RowCoverTag/MageHunterTag/InfiltratorTag/AntiHealPassiveTag/MageRole）削除。`GetDamageMultiplier`（魔導士特攻）削除。`ComputeBaseDamage` を新ダメージ式に差し替え。クラス骨格・イベント機構・`ProcessReactionStack` は維持 |
| `TargetEvaluator.cs` | **改修**（完全削除ではない） | かばう判定削除・列単位保護削除・特効削除。`DeclareAction` の forced / support / self-guard 経路は維持。新ターゲティング（近接=最前敵 / 遠隔=最後尾敵）を別メソッドで追加 |
| `ActionExecutor.cs` | **改修** | `OnHitLanded` で反撃を `ReactionStack` に積む経路を追加。かばう移動（`ResolveCoverTarget`）削除。`OnActionResolved` 集約は維持 |
| `StatusEffectProcessor.cs` | **改修**（最小） | クラス骨格は維持。`Cover` 関連処理だけ削除。燃焼/凍結/麻痺/呪い/アンデッド復活は新戦闘でも維持 |
| `Conditional/ConditionalBuffHook.cs` | **残置** | 基底フレームワーク・新シナジー実装で流用 |
| `Conditional/ConditionalBuffProcessor.cs`（基底） | **残置** | 基底クラス・再帰ガード機構は新シナジーで活用 |
| `Conditional/LonerWolfConditionalProcessor.cs` | **_old/ 退避** | 旧ユニット用 |
| `Conditional/PrinceDarkAuraConditionalProcessor.cs` | **_old/ 退避** | 旧ボス用 |
| `Conditional/PendantConditionalProcessor.cs` | **_old/ 退避** | 旧ブリジット用 |
| `Conditional/OffenseDefenseLinkConditionalProcessor.cs` | **_old/ 退避** | 旧ブリジット用 |
| `Domain/Prototype/AlliesRoster.cs` / `EnemiesRoster.cs` / `BossesRoster.cs` | **_old/ 退避** | 新 Roster で置換 |
| `Domain/Prototype/RosterHelpers.cs` | **改修** | 新ステータス系（DEF 統合）に追従 |
| Editor 検証ツール 5 種 | **_old/ 退避 or 削除** | Phase R-5 で新規作成 |
| `MatchupTests.cs` | **削除** | 旧戦闘前提・新検証ツールで代替 |
| `AssassinTests.cs` | **削除** | 暗殺者廃止 |
| `VSProto_InfiltratorTests.cs` | **削除** | Infiltrator タグ廃止 |
| `VSProto_RowSplashTests.cs` | **削除** | 同列スプラッシュ廃止 |
| `VSProto_PendantConditionalProcessorTests.cs` | **削除** | 該当 Processor 退避 |
| `VSProto_OffenseDefenseLinkConditionalProcessorTests.cs` | **削除** | 同上 |
| `VSProto_LonerWolfConditionalProcessorTests.cs` | **削除** | 同上 |
| `VSProto_MercenaryTests.cs` | **削除** | 旧傭兵 LonerWolf 用 |
| `VSProto_BridgetTests.cs` | **削除** | 旧 Pendant + OffenseDefenseLink 用 |
| `VSProto_PrinceTests.cs` | **削除** | 旧皇太子用 |
| `Step1-5_*.cs` / `Prototype_*.cs` | **削除 or _old/** | 旧プロト系・既存 _old/ に追加 |
| `VSProto_ActionGuardTests.cs` | **残置** | ActionGuard は新戦闘でも使用 |
| `VSProto_StatusEffectConsistencyTests.cs` | **残置** | StatusEffect 整合性 |
| `VSProto_SourceAbilityNameTests.cs` | **残置** | SourceAbilityName |
| `VSProto_ActionResolvedTests.cs` | **残置** | ActionResolved イベント |
| `VSProto_RuntimeUnitTests.cs` | **残置** | RuntimeUnit |
| `BattleEventTests.cs` | **残置** | BattleEvent |
| `VSProto_EnemyPatternsTests.cs` | **改修 or 削除** | 旧パターン前提・新戦闘の地形ベースに合わせ書き直し |
| `VSProto_DamageFormulaRegistryTests.cs` | **改修** | 新ダメージ式に追従 |
| `VSProto_TriggerConditionRegistryTests.cs` | **改修 or 削除** | 使用状況に応じて判断 |
| `VSProto_UnitCatalogIntegrationTests.cs` / `VSProto_WazaCatalogIntegrationTests.cs` | **改修** | 新ユニット / 新 Waza で書き直し |
| `VSProto_RoundManagerTests.cs` / `VSProto_RunFlowTests.cs` | **残置（戦闘呼び出し部分は Stub 化）** | ラン進行ロジック検証 |
| `VSProto_PrincessTests.cs` | **改修** | 新王女仕様に追従 |
| その他戦闘以外のテスト | **残置** | StoryProgress / DefeatScene / EndingResolver / RoundStartEvent / MapState / MapNode / DraftService / DraftPoolCatalog / MetaProgress / SaveStore / Interior / DebugBattlePresets / StorySceneCatalog 等 |

---

## 6. リスク・注意点（R-0 で更新）

### 6.1 BattleRunner.Run() インタフェース維持

**【R-0 で判明：リスクは想定より小さい】**

UseCase 側（`VSPrototypeRoundManager`）は `BattleResolver` デリゲート経由で抽象化されており、`resolver = resolver ?? BattleRunner.Run` のフォールバックパターン。

**方針**：新戦闘は `BattleRunner.Refactored.BattleRunner.Run` として並走実装。テスト完了後、`VSPrototypeRoundManager` のデフォルト解決を `Refactored.BattleRunner.Run` に差し替えるだけで切り替え可能。シグネチャ拡張（地形引数追加）はデフォルト引数で後方互換維持。

### 6.2 シナジーの実装パターン

**【R-0 で判明：既存フレームワークが最適】**

`ConditionalBuffProcessor` 基底クラスが既に：

- Hook 種別の宣言（`BattleStart` / `TurnStart` / `UnitDied` / `BuffApplied` / `BuffRemoved`）
- `DispatchRefresh` の再帰ガード
- 差分判定パターン（`AuraSourceId` で旧効果を識別剥奪 → 新強度で再付与）

を備えている。**新シナジー（FireSynergy / WaterSynergy / SubSynergy）はこれを継承するだけで実装できる**。

旧 `LonerWolfConditionalProcessor` が「陣営生存数連動の動的バフ」を実装していて、火属性の「最 ATK ユニットへのバフ」の参考になる。

### 6.3 反撃の実装基盤

**【R-0 で判明：既存枠で実装可能】**

`BattleContext.ReactionStack`（割り込み行動キュー）が既に存在し、コメントに「トゲ（反射）などのパッシブスキルがここで ReactionStack に積む」と明記されている。`ActionExecutor.OnHitLanded` イベントもあり、被弾時のフックも完備。

**方針**：新戦闘の反撃も `OnHitLanded` で `ReactionStack` に積む方式で実装する。同時解決のログ集約は `BattleRunner.Run` のログフォーマット改修で対応。

### 6.4 引き分け非対称評価

**【R-0 で判明：既存集計を流用可能】**

`BattleContext.AllyKillCount` / `AllyDeathCount` / `IsAdvantageousVictoryCondition` が既に実装されている。

**方針**：判定式を「攻め側なら撃破数 > 0、守り側なら被撃破数 == 0」に変更するだけ。`BattleContext` に攻め/守り判別のフラグを追加し、`VSPrototypeRoundManager` で MapNode 種別から判定して渡す。

### 6.5 段階移行（並走方式）

**【R-0 で方針確定：並走→差し替え】**

旧コードを一気に `_old/` 退避してから新コード書く方式は、`BattleRunner.Run` の規模（411 行・Conditional 結線含む）を考えると無理。

**方針**：
1. Phase R-1 で `_old/` 用 asmdef を準備するが、旧コードはまだ移動しない
2. Phase R-2 で新コードを別ファイル（V2 命名）で並走実装
3. Phase R-3 / R-4 で動作確認
4. Phase R-5 完了後、旧 BattleManager / BattleRunner.Run を `_old/` に物理移動・新版を本流に昇格

### 6.6 既存テストの取り扱い

**【R-0 で仕分け確定】**

§5 で個別に仕分け済。要点：
- 戦闘呼び出しがあるテストは `BattleResolver` デリゲート差し替えで Stub 化可能
- ラン進行・ドラフト・メタ系のテストは戦闘 Stub で残せる

### 6.7 召喚ユニット概念

**【削除確定】**

`RuntimeUnit.IsSummoned` / `BattleContext.GetAliveOriginalAllies` / `GetAliveOriginalEnemies` / 撃破数集計の召喚除外ロジックは旧戦闘で発火イベントなし＝**集計枠だけ存在して何も実装されていない**状態だった。

**方針**：今は削除して `BattleContext` を簡素化。本番で召喚が必要になった場合は既存のイベント駆動設計（Skill `TriggerType.OnDeath` / `OnTurnStart` 等 + `BattleManager.OnSummonRequested` 新規イベント + `BattleContext.AddSummonedAlly` 新規 API）で 1-2 日の作業で追加できる。今のうちに削除してコード量を減らす方が筋。

### 6.8 Skill クラスとパッシブ

**【R-0 で新規発見】**

`Domain.Skills.Skill` クラス（パッシブスキル定義）が存在。TriggerType（OnActionStart / OnPerHit / OnDeath 等）+ Condition + Effect で実装。

**方針**：新戦闘でパッシブが必要なら（補助属性のキャラ特性など）これを流用可。今のところ Waza ベースで反撃を実装する方針なので、Skill は補助的な位置。

### 6.9 射程の 2 軸分解（Mid 廃止）

**【確定】**：旧 `AttackRange` enum（Melee/Mid/Ranged）を廃止し、**AttackKind**（Melee/Ranged）＋ **TargetingDirection**（FromFront/FromBack）の 2 軸に分解。

| AttackKind × TargetingDirection | 戦術的位置付け |
|---|---|
| Melee + FromFront | 普通の前衛アタッカー（反撃あり） |
| Ranged + FromBack | 普通の弓兵（反撃なし） |
| Ranged + FromFront | 前列焼き（旧 Mid・反撃なし） |
| Melee + FromBack | 暗殺者（反撃あり・敵後衛狙い） |

旧 Mid の「位置を問わず最前を狙う」フレーバーは「Ranged + FromFront」で再現される。さらに「Melee + FromBack」が新設計枠として暗殺者ポジションを明確化。

**実装影響**：
- `Unit.cs` に `AttackKind` / `TargetingDirection` プロパティ追加（旧 `Range` 削除）
- `BattleManager` のターゲット選定は `TargetingDirection` のみ参照
- 配置 ATK 補正・反撃判定は `AttackKind` のみ参照
- 完全に直交した 2 軸として実装

### 6.11 クリティカル

**【方針：将来実装・今は無し】**

ATK バフだけだとスケーリング表現が頭打ちになるため、将来的にクリティカルは必要。ただし、ダメージ式に「式を経由しない倍率」を後から差し込む作業は難易度低い。

**方針**：プロト段階では実装しない。本番版で必要になったら以下を追加：
- `Unit.CriticalRate` プロパティ（%）
- `Unit.CriticalDamageMultiplier`（例：1.5）
- `BattleManager.ComputeBaseDamage` に「クリ判定→倍率乗算」分岐追加
- キャップ：CriticalRate 上限（例：50%）

→ Phase R-2 では一切実装せず、ダメージ式は「クリなし」前提で書く。

### 6.12 負傷状態（InjuryState）の削除確定

**【削除】**

旧仕様：HP0 になったユニットは Active → Injured（戦闘終了直後）→ Resting（次ラウンド配置不可）→ Active（その次のラウンド）と遷移。

旧仕様の存在理由：「死亡 = ロスト」だと厳しすぎる、しかしノーペナルティだと緊張感がない、という妥協案。

**削除の根拠**：フル版では内政要素として **HP 治療コスト（お金）** を導入予定。お金が負傷の代替コストになるため、プロトで負傷システムを残す投資価値が低い。**「フル版でなくす可能性の高いものに開発コストを使うのはプロトとしておかしい」**。

**削除範囲**：
- `Unit.InjuryStatus` プロパティ
- `InjuryState` enum
- 戦闘終了後の `Active→Injured` 遷移処理
- `Resting → Active` の戦闘前遷移
- 内政の配置不可ロジック（旧 `Stage3InteriorService` の Resting 排除）
- 関連テスト

### 6.13 パッシブスキル（Skill クラス）の維持

**【基本維持】**

`Domain.Skills.Skill` クラス（パッシブスキル定義・`TriggerType` で 7 種の発動タイミング）は新戦闘でも基本維持。

**使い道**：
- 補助属性のキャラ特性（Phase R-3 で詰める）
- 反撃の特殊効果は `CounterWaza` で実装方針なので Skill 経由ではない
- 召喚を本番で再導入する場合の発火経路（OnDeath / OnTurnStart 等）
- その他汎用パッシブ

**改修**：
- 旧戦闘で使われていた具体パッシブ（特定ユニット固有）は `_old/` 退避
- 基底クラス + TriggerType enum は維持

### 6.14 配置 ATK 補正の戦闘中欠け動的再計算

**【動的再計算で確定】**

戦闘中にユニットが死亡（または将来召喚で増加）した際の配置補正・ターゲティングは以下の分離設計で対応：

| レイヤー | 動作 |
|---|---|
| **戦闘画面（見た目）** | 戦闘開始時の GUI 並びで固定。死亡ユニットはグレーアウト・位置は動かない |
| **内部スロット** | 生存ユニットだけで詰め直す。死亡で欠けた slot を生存ユニットに再割当 |
| **配置 ATK 補正** | 内部スロットで再計算（特に遠隔の「最後尾からの距離」が動的に変化） |
| **ターゲティング**（最前/最後尾敵） | 内部スロットで再計算 |

**実装**：`BattleContext` に「内部スロット解決ヘルパ」（生存ユニットを SlotIndex 順にソートして内部 index を返す）を追加。各死亡時イベント（`ActionExecutor.OnUnitDied`）で再計算をトリガー。

**配置 UI（プレイヤー操作）の仕様確認**：
- プレイヤーは GUI 上の 6 枠に好きな順番で配置可能
- 空きを作っても良い（強制的に詰めない）
- 戦闘画面でも GUI 上の並びがそのまま固定表示
- 内部計算だけが暗黙的に空きを詰める

→ **UI 自由度・見た目固定性・内部計算の単純さの 3 つを 3 レイヤー分離で両立**。詳細は [320 §2.1.1](320_vsprototype_combat_spec.md)。

### 6.15 バフ二系統ルール（基礎加算 / 最終出力割合）

**【2026-06-15 セッション Phase R-2 Step 2-5 着手時に確定】**

属性シナジー設計で「火属性は ATK 割合バフ」と書いていた仕様 320 §1.4 と、現実装 `RuntimeUnit.EffectiveATK = BaseATK + Σ(Magnitude × Stacks)` の **加算式** が不整合だった。設計議論の結果、以下のルールで確定：

| 規約 | 内容 |
|---|---|
| 基礎ステータス（ATK / DEF / HP / SPD）の強化 | **加算のみ・乗算禁止** |
| ダメージ式自体 | 変更禁止（加算強化後の実効ステータスで計算） |
| 乗算が許される場所 | **最終ダメージ出力に対してのみ**（与ダメ%増加 / 被ダメ%カット / クリティカル） |
| 最終出力計算順序 | 与ダメ% → 被ダメ%（キャップ 80%）→ クリティカル（最後に乗算） |
| 与ダメ% / 被ダメ% のスタッキング | 加算スタック（`1 + a% + b% + c%`）。乗算スタック禁止 |

**根拠**：

- 基礎ステータスが乗算で増えると **計算序盤に大きなレバレッジ**がかかり、係数のコントロール可能性を失う。低 ATK ユニットへの加算は割合より相対的に強く、終盤の高 ATK ユニットには控えめ＝**自然なソフトキャップ**として機能
- 最終出力にのみ乗算を許すことで「気持ちよさ（クリティカル爆発）」と「予測可能性（加算スタック）」の折衷
- 防御側にのみキャップ（暫定 80%）を設けることで「ダメージが通らない退化戦略」を構造的に防ぐ。攻撃側は青天井

**実装影響**：

- 火属性シナジーは「ATK バフ」ではなく「与ダメ% 増加バフ」（`StatusEffectType.OutgoingDamageUp` 新設）
- 水属性シナジーの DEF バフは既存 `DefenseUp`（加算）流用で OK
- DamageFormula 純関数は触らず、その外側で `DamageModifier` 純関数が最終出力に乗算を適用

### 6.10 ログ・イベント記録の責務分離（負債解消）

**【R-0 で判明：旧 `BattleRunner.Run` は責務混在】**

411 行のうち約 2/3（ログ生成 150 行 + イベント記録 110 行）が「とりあえず Run に詰め込まれている」状態。設計判断として責務混在を選んだわけではなく、機能追加のたびに「ローカル関数で済ませる方が早い」を選び続けた結果、分離タイミングを逃して固定化した。

**方針**：[feedback_no_technical_debt]（技術的負債は許容しない・本実装に持っていける設計を優先）方針に従い、新戦闘 `Refactored.BattleRunner.Run` では責務分離する。

- `BattleLogFormatter`・`BattleEventRecorder`・（任意）`BattleAssembly` に切り出し
- `Refactored.BattleRunner.Run` 本体は 80-100 行に圧縮
- 詳細は §4 Phase R-1 R-1-3

**注意**：
- 旧 `BattleReport.Events` / `BattleReport.Log` の構造は維持（Presentation 側 `VSPrototypeBattleGUI` の後方互換）
- 旧ログ文字列フォーマットも極力維持（Formatter の単体テストで旧フォーマットと一致確認）
- 「分離による実装コスト増」は半日〜1日程度の上乗せだが、本番版実装時にそのまま使える基盤になる

---

## 7. 作業ログ（時系列）

### 2026-06-13 セッション

- 戦闘システム再設計の方針確定（A-E 議論）
- [320_vsprototype_combat_spec.md](320_vsprototype_combat_spec.md) 新設（仕様書）
- 本書（911）新設（実装計画・想定ベース初版）
- メモリ更新：戦闘システム再設計タスクへ移行
- **R-0 現実装調査（同日）**：6 Step で Domain.Battle / Data 層 / テスト / UseCase / Presentation / `_old/` 退避前例を確認
- R-0 結果反映で本書 §2 〜 §6 を実コード基準に更新（想定ベース部分は §9 想定 vs 実コードの差分に集約）
- **責務分離方針確定（同日）**：旧 `BattleRunner.Run` 411 行のうち約 2/3 がログ・イベント記録の混在と判明。新 `Refactored.BattleRunner.Run` では `BattleLogFormatter` / `BattleEventRecorder` / （任意）`BattleAssembly` に切り出し、本体 80-100 行に圧縮する方針。詳細は §4 Phase R-1 R-1-3 / §6.10
- **保留項目の確定（同日）**：
  - 射程：旧 `AttackRange` 廃止、`AttackKind` + `TargetingDirection` の 2 軸分解（Mid 廃止・暗殺者枠を新設）。詳細は §6.9
  - 状態異常：Burn / Freeze / Paralysis / Curse / ReviveInvalid 全て新戦闘でも維持
  - 回避判定：維持（クリティカルと同様に式を経由しないためキャップは別途要）
  - シールド実装：`StatusEffect` の Conditional カテゴリ + `StatusEffectType.Shield` 新設・WaterSynergyProcessor で動的管理
  - シナジー段階補完：3/5 体は線形補間（火水確定・将来別属性は個別判断）
  - SPD タイブレーク：陣営間は `BattleContext.IsAttackingSide` 優先、同一陣営内は slot 番号昇順
- **属性経路の純化（同日）**：属性は「シナジー × 地形補正」の 2 役割だけに純化。`Waza.WazaElement` / `HitOutcome.DamageElement` / 属性連動状態異常解除（火→凍結解除、水/氷→燃焼解除）を全廃。状態異常解除は `Waza.CleansesStatusAilments` 経由のみに統一
- **召喚ユニット概念削除（同日）**：旧戦闘で実装されていなかった集計除外枠を削除。本番で再導入する場合は Skill + イベント駆動で 1-2 日の作業で追加可能
- **残保留項目の確定（同日）**：
  - クリティカル：将来必要だが今は実装なし（後から差し込み可能）。詳細は §6.11
  - 負傷状態（InjuryState）：削除確定。フル版で治療コスト（お金）が代替。詳細は §6.12
  - パッシブ Skill：基本維持。詳細は §6.13
  - 配置 ATK 補正の戦闘中欠け：内部スロット動的再計算。配置 UI ×戦闘画面表示×内部計算の 3 レイヤー分離（[320 §2.1.1](320_vsprototype_combat_spec.md)）。詳細は §6.14
  - 並走実装命名：`Echolos.Domain.Battle.Refactored` 別 namespace 方式（V2 サフィックス回避）。Phase R-5 完了時に namespace 一括削除＋旧 `_old/` 物理移動で本流統合

### 2026-06-14 セッション（Phase R-1 完了）

- **R-1-1**：`Assets/Scripts/Domain/Battle/_old/Echolos.Domain.OldBattle.asmdef` 新設（`defineConstraints: ECHOLOS_OLD_BATTLE` / `autoReferenced: false` / `noEngineReferences: true`）。物理移動なし＝旧戦闘は並走稼働継続。
- **R-1-3a**：`Assets/Scripts/Domain/Battle/Replay/BattleLogFormatter.cs` 新設。旧 BattleRunner.Run 内の `FormatSingleOutcome` / `FormatGroup` / `AppendOutcomesLog` / `SurvivorSummary` / `LineupSummary` / `ResultLabel` / `FormatEffectWithSource` を純関数群として切り出し。`CreateNameResolver` で味方/敵 prefix 付き表示名 Resolver を生成。
- **R-1-3b**：`Assets/Tests/Editor/Domain/BattleLogFormatterTests.cs` 新設（テストケース 21 件）。各種 Outcome / 多段集約 / 複数ターゲット連結 / 全分岐 ResultLabel / SourceAbilityName あり/なし を網羅。
- **R-1-3c**：`BattleEventRecorder` + `BattleAssembly` 新設。BattleEventRecorder は BattleManager / ActionExecutor / StatusEffectProcessor / RuntimeUnit のイベントを購読し BattleReport.Log と BattleReport.Events に書き出す（文字列整形は BattleLogFormatter に委譲）。BattleAssembly は BattleContext + 4 サブシステムの生成と結線をカプセル化（`ConditionalBuffProcessor` リストは呼び出し側が渡す方式に統一・旧の暗黙固定 4 種を直書きしない）。
- **R-1-4**：本書 §3 完了基準を並走方式に追従更新、§7 に本セッション記録を追加。仕様書（320 / 310 / 390）の追従は Phase R-6 でまとめて実施。
- **テスト確認**：ユーザー Test Runner で新規 21 件 PASS 確認（既存テストも回帰なし前提）。
- **500 追従**（コミット `ad640f0`）：§2.1 `_old/` 退避用 asmdef 表に `Echolos.Domain.OldBattle` 追加（3→4 個化）、§2.2 ディレクトリ構造に `Domain/Battle/_old/` 行追加、§13.1 退避先表に同行追加＋ defineConstraints 説明を併記形式化（`ECHOLOS_OLD_PROTO` / `ECHOLOS_OLD_BATTLE` 等）。§3.6 / §4.4.5 は Phase R-6 で 110 と合わせて追従（§8.4 二重ドリフト防止）。
- **本書 R-5 / R-6 への追従漏れ防止追記**（コミット `ad640f0`）：§3 Phase 表 / §4 Phase R-5 / R-6 詳細に 500 §13.2 / §3.6 / §4.4.5 の追従タスクを明記。本流統合（旧コード `_old/` 物理移動・`Refactored` namespace 一括削除・UseCase 切替）も R-5 に明記。
- **Unity .meta 整合性復元**（コミット `14892e9`）：Test Runner 実行時に Unity が生成した新規ファイルの .meta を Git 管理下に追加。

### 工数感サマリ（Phase R-2 〜 R-6・大中小ざっくり評価）

ユーザー要望で各 Step を「大・中・小」評価。**大**=設計検討含めて 1 セッション以上、**中**=実装＋テストで 1 セッション内、**小**=数コミット〜半セッション、の体感。

| Phase / Step | 工数 | ボトルネック |
|---|---|---|
| R-2-1 ステータス再定義 | 大 | Unit/RuntimeUnit/Waza/HitOutcome/StatusEffectProcessor＋ SO 再生成＋既存テスト棚卸し |
| R-2-2 ダメージ式 | 小 | 単一経路化済（ComputeBaseDamage 1 箇所） |
| R-2-3 配置 ATK 補正 | 中 | 3 レイヤー分離と戦闘中欠けの動的再計算 |
| R-2-4 反撃 | 中 | 死亡時/Shield 吸収/多段 hit/ログ集約の条件分岐 |
| R-2-5 属性シナジー | 大 | 3 Processor＋ Shield 新設＋段階発動＋線形補間 |
| R-2-6 地形 | 中 | ランダム化制約（全 3 属性 1 列ずつ保証）と UseCase 連動 |
| R-2-7 引き分け非対称＋SPD | 小 | 既存集計流用・判定式変更のみ |
| R-3-1 ユニット仕様策定 | 大 | 17 体分の数値設計・バランス検証で書き直し前提 |
| R-3-2 ユニット実装 | 中 | Roster コード＋ SO 生成＋アイコン依頼 |
| R-4-1 戦闘 UI 再設計 | 中〜大 | 6 体一列＋擬似 3D へ前後 3+3 構図から全面組み直し |
| R-4-2 配置 UI 再設計 | 中 | 3 レイヤー分離の実装が肝 |
| R-4-3 マップ地形バッジ | 小〜中 | アイコン依頼の有無で揺れる |
| R-5 検証ツール＋本流統合 | 大 | 検証ツール 4 種＋ namespace 一括削除＋ UseCase 切替 |
| R-6 バランス調整＋仕様書 | 大 | 試遊サイクルが本質的に長丁場・310/110/500 追従もここ |

**サマリ**：大 5・中 6・小 3。**ボトルネック予想**：
- 2-1 は最初の山（影響範囲がプロト全体）、ここを超えれば 2-2〜2-7 は流れる
- 3-1 と R-6 は本質的にバランス調整サイクルで時間軸が読みにくい（試遊待ち）
- 並走方式の利点で R-2 着手中も VSプロトは旧戦闘で稼働継続＝ユーザー試遊と独立して進められる

### 2026-06-14 セッション（Phase R-2 Step 2-1 完了）

【方針判断（β 案採用）】
Step 2-1 着手にあたり、Domain.Models / Skills / HitOutcome の破壊的変更は旧戦闘コードを巻き込んで壊れるため、Phase R-1 で想定した並走方式（旧コード稼働継続）が成立しないことを確認。[feedback_no_technical_debt] と整合する **「旧戦闘一斉退避→ Models 改修」（β 案）** を採用。VSプロト本流の試遊は Phase R-2 完了まで停止。

【Step 2-1A：旧戦闘・旧Roster・旧テスト・旧 DevTools の一斉退避】（コミット `d3aac06`）

- Domain/Battle/_old/：BattleManager / TargetEvaluator / ActionExecutor / StatusEffectProcessor / BattleRunner / BattleEventRecorder / BattleAssembly / Conditional 派生 4 種（11 ファイル）
- Domain/Prototype/_old/：AlliesRoster / EnemiesRoster / BossesRoster / RosterHelpers（4 ファイル）
- Data/Editor/_old/：SoAssetGenerator
- Presentation/DevTools/Editor/_old/：BalanceReportTool / Weak / Mid / SixVsSix / SixVsSixBackTank（5 ファイル）
- Presentation/DevTools/_old/：DebugBattleSandboxBootstrap
- Tests/Editor/Domain/_old/：旧戦闘前提テスト 25 件

【残置】BattleContext / ActionDeclaration / HitOutcome / Replay/{BattleResolver / BattleReport / BattleEvent / BattleLogFormatter} / Conditional/{ConditionalBuffHook / ConditionalBuffProcessor} / BattleLogFormatterTests（21 件・純関数）+ 戦闘外テスト全件

【新規 asmdef 3 個 + 既存 1 個改修】
- `Echolos.Data.OldEditor`（Editor 限定・ECHOLOS_OLD_BATTLE+OLD_PROTO AND）
- `Echolos.Presentation.DevTools.OldEditor`（Editor 限定・同 AND）
- `Echolos.Presentation.DevTools.Old`（Runtime・ECHOLOS_OLD_BATTLE）
- `Echolos.Tests.Domain.Old`：references に Echolos.Domain.OldBattle 追加、defineConstraints に ECHOLOS_OLD_BATTLE 追加

【UseCase スタブ化】
- `VSPrototypeRoundManager.ResolveAllBattles`：`resolver ?? BattleRunner.Run` → `resolver` null チェックで ArgumentNullException
- `VSPrototypeBootstrap.ResolveCurrentRound`：resolver: null のまま（試遊で例外＝Phase R-2 完了まで本流停止と整合）

【500 追従】（コミット `7ca3a4a`）

- §2.1 退避用 asmdef 表：4 個→7 個に拡張、新規 asmdef 3 行追加、Echolos.Domain.OldBattle の役割記述を「再設計の退避先・並走中」から「旧戦闘システム本体（具体クラス列挙）」に更新
- §2.2 ディレクトリ構造：Battle/Conditional/ 明示、Battle/ の責務記述から「かばう判定」削除、Battle/Replay/ から「イベント記録の責務分離」削除、Data/Editor/_old/ / Presentation/DevTools/_old/ / Presentation/DevTools/Editor/_old/ 行追加
- §13.1 退避先表：5 行→8 行に拡張、末尾段落から「Step 5-9 で 4 ファイル分割」削除し「Phase R-3 で新 Roster 再構築」に置換
- §3.6 戦闘システム仕様要素マッピング表 / §4.4.5 ConditionalBuffProcessor 実装例は 110 本番版仕様の更新と合わせて Phase R-6 でまとめて追従（§8.4 二重ドリフト防止）

【Step 2-1B：Models / Skills / HitOutcome 一括改修】（コミット `437ca29`）

破壊的変更を 1 コミットにまとめる（Enums / Unit / RuntimeUnit / Waza / HitOutcome に相互依存があるため）。

- Enums.cs：AttackRange enum 削除 / 新 AttackKind enum（Melee/Ranged）/ 新 TargetingDirection enum（FromFront/FromBack）/ 旧 AttackKind enum（None/Physical/Magical）削除 / StatusEffectType.Cover 削除 / StatusEffectType.Shield 追加 / InjuryState enum 削除 / WazaCategory.Counter 追加 / TargetingType の Front/BackRow 系 3 値削除
- Unit.cs：PDEF/MDEF → DEF 統合 / Range → AttackKind+TargetingDirection 分解 / PrimaryAttackKind 削除（ユーザー承認）/ InjuryStatus 削除 / TargetTags 削除 / CounterWaza プロパティ新設
- RuntimeUnit.cs：EffectivePDEF/EffectiveMDEF → EffectiveDEF 統合 / IsCovering 削除 / IsSummoned 削除 / CurrentShield/CanCarryOverShield 削除 / ShieldStacks プロパティ新設（StatusEffectType.Shield スタック合計）/ IsFrontRow/IsBackRow 削除
- Skills/Waza.cs：IsPhysical / WazaElement / IgnoresCover / IgnoresFrontRowGuard / SameRowSplashMultiplier / DispelsBuffs / DispelsDebuffs / DefenseIgnoreRatio 削除 / Waza.DefaultCounter 静的フィールド新設
- Skills/RuntimeWaza.cs：旧プロパティの Proxy 削除
- Battle/HitOutcome.cs：DamageElement 削除（属性経路をシナジー×地形補正のみに純化）
- Battle/BattleContext.cs：GetAliveOriginalAllies/Enemies 削除（GetAliveAllies/Enemies に統合・召喚概念廃止）
- Battle/Replay/BattleLogFormatter.cs：LineupSummary の「前/後」表記を「slot N」に変更

【Data 層追従】
- Definitions/UnitDefinition.cs / WazaDefinition.cs：旧フィールド削除・新フィールド追加
- UnitCatalog.cs / WazaCatalog.cs：BuildUnit/BuildWaza で旧 def フィールド参照削除・新フィールド対応・CounterWazaId 解決

【Presentation 層追従】
- UnitCardView.cs：「前衛/後衛」→「slotN」
- UnitBadgeOverlay.cs：AttackRange 3 種（Melee/Mid/Ranged）→ AttackKind 2 種、Cover バッジ描画削除
- GuideContent.cs：RangeLabel → AttackKindLabel、TargetingDirectionLabel 新設、TargetingLabel の Row 系分岐削除、WazaOneLineSummary から IsPhysical/WazaElement 削除
- VSPrototypeMapGUI.cs / VSPrototypeInteriorGUI.cs：unit.Range → unit.AttackKind、PDEF/MDEF → DEF

【コメント追従】
- StatusEffect.cs：DispelsBuffs/DispelsDebuffs 参照を一般化記述に
- DamageFormulaRegistry.cs：MDEF 言及を DEF に、standard_attack 新設＋旧 3 ID 暫定互換
- VSProto_UnitCatalogIntegrationTests.cs：先頭コメントから TargetTags 削除

【Step 2-1C：新規単体テスト追加】（コミット `d008bd5`）

`Step2_1_StatusRedefinitionTests.cs`（22 件）：
- DEF 統合（6 件）：基礎値そのまま／兵種強化加算／DefenseUp/Down 加減算／負値クランプ／スタック比例
- AttackKind × TargetingDirection（6 件）：デフォルト Melee/FromFront、4 通り組み合わせ
- CounterWaza（2 件）：デフォルト null、個別設定
- Waza.DefaultCounter（3 件）：Counter カテゴリ／付帯効果なし／倍率 1.0
- ShieldStacks（3 件）：デフォルト 0、Stacks 反映、複数 Shield 合計
- BattleLogFormatterTests 既存テスト 1 件追従（Lineup 期待値「slot0:..., slot3:...」）

**【Step 2-1D：コンパイル復旧追補】**（コミット `bb8fd89`）

退避コミットで `Echolos.Domain.Prototype` namespace 参照点を見落とし、UseCase / Presentation / Tests に CS0234 が広範囲発生。**`Domain/Prototype/` 直下に空 Unit スタブ Roster 3 個（AlliesRoster 16 関数 / EnemiesRoster 11 関数 / BossesRoster 4 関数）を再生成**して暫定対応。各ファクトリは Id と Name だけ設定した空 Unit を返す（実ステータス・Waza・スキルは Phase R-3 で新ユニットセットと一緒に正式実装で置換）。VSPrototypeEnemyPatterns も空 List 返却スタブ化。

**【⚠ Phase R-3 で必ず置換すべき暫定スタブ】**：[feedback_no_technical_debt] と整合させるため、Phase R-3 ユニット実装時にスタブ Roster と VSPrototypeEnemyPatterns を新 Roster で完全に置換すること。空 Unit を扱う限り戦闘外テストは進むがステータス前提のアサート（RuntimeUnit / Princess / Bridget 等）は fail する想定。

**【Step 2-1E：AttackKind enum 追従漏れ修正】**（コミット `bb05fb0`）

`UnitDisplayLabels.AttackKindLabel` の switch 分岐が旧 AttackKind（None/Physical/Magical）参照のままだった。新 AttackKind（Melee/Ranged）に追従修正。

**テスト確認**：ユーザー Test Runner で新規 22 件 PASS 確認済。

【Step 2-1 で未着手・後続 Step に持ち越し】
- 旧 SO アセット（Resources/Data/*.asset）：旧フィールド名のまま残置（Unity が旧フィールドを無視・新フィールドはデフォルト値）。SO 再生成は Phase R-3 で新ユニットセットと同時実施
- StatusEffectProcessor / BattleEventRecorder / BattleAssembly：退避済み・Phase R-2 後半でシナジー / 地形 / 引き分け非対称評価実装と同時に Refactored namespace で再構築
- Refactored.BattleRunner.Run：Phase R-2 後半で新ダメージ式・反撃・シナジーを組み込んで構築

### 2026-06-15 セッション（Phase R-2 Step 2-2 完了）

【設計判断】

§4 Phase R-2 Step 2-2 では「`BattleManager.ComputeBaseDamage` を新ダメージ式に差し替え」と書いていたが、Step 2-1 で旧 BattleManager は `_old/` 退避済・Refactored.BattleManager 未作成のため、**`Echolos.Domain.Battle.Refactored.DamageFormula` 純関数静的クラス** として単独実装する形に変更。

設計効果：
- 配置 ATK 補正（Step 2-3）と環境項（Step 2-6）を引数化（デフォルト 1.0 / 0）→ 後続 Step は API 拡張なしで結線可能
- 単体テスト容易（RuntimeUnit/Waza 依存なし）
- Phase R-2 後半で Refactored.BattleManager を組む時、`ComputeBaseDamage` は `DamageFormula.ComputeBaseDamage(...)` 呼び出しで完結

【Step 2-2：新ダメージ式 純関数実装】（コミット `ede81d1`）

- 新規 `Assets/Scripts/Domain/Battle/Refactored/DamageFormula.cs`：純関数 `ComputeBaseDamage(attackerAtk, wazaMultiplier, defenderCurrentHp, defenderDef, positionAtkCorrection=1.0, terrainBonus=0.0)` を提供
- 暫定定数 `ConstA=0` / `ConstB=10`（Phase R-6 バランス調整で再確定予定）
- `System.Math.Sqrt` / `System.Math.Round`（Domain は noEngineReferences=true・Mathf 不可）
- 負値 HP/DEF は 0 クランプで分母安全（`ConstB` により分母最低 10 保証・DEF 0 のユニットでも除算事故なし）
- 新規 `Assets/Tests/Editor/Domain/Step2_2_DamageFormulaTests.cs`：9 件

  | # | 検証内容 | 期待値 |
  |---|---|---|
  | 1 | 基本式 ATK50/mult1/HP100/DEF0 | 50 |
  | 2 | HP 4 倍でダメージ 2 倍（√HP 反映） | 100 |
  | 3 | DEF=10 で分母倍化・半減 | 25 |
  | 4 | mult=2 で 2 倍 | 100 |
  | 5 | 配置 ATK 補正 0.85（Step 2-3 引数の事前確認） | 43 |
  | 6 | 環境項 10（Step 2-6 引数の事前確認） | 25 |
  | 7 | ATK=0 はダメージ 0 | 0 |
  | 8 | 負値 DEF クランプ | 50 |
  | 9 | 負値 HP クランプでダメージ 0 | 0 |

【Step 2-2 で未着手・後続 Step に持ち越し】
- 配置 ATK 補正カーブ：Step 2-3 で `Domain.Battle.Refactored.PositionAtkCorrection` を新設し、AttackKind 別カーブ（近接=slot 番号 / 遠隔=最後尾からの距離）を実装
- 環境項の値割り当て：Step 2-6 で `TerrainKind` × ユニット属性 × 強度（自領/敵領/敵拠点）の組み合わせ表として実装
- DamageFormula と既存 `Domain.Formula.DamageFormulaRegistry` の関係整理：旧 Registry は旧戦闘（_old/）から参照されないため事実上未配線・Phase R-5 本流統合時に廃止判断（あるいは Phase R-3 で新 Waza が固定倍率派なら役割消滅）

### 2026-06-15 セッション 第 2 段（Phase R-2 Step 2-3 完了）

【設計判断】

§4 Phase R-2 Step 2-3 では「`Domain.Battle/PositionAtkCorrection.cs` 新設」のみ書かれていたが、実装着手時に **配置 UI（SlotIndex 0..5 空き含む）/ 戦闘画面（SlotIndex 固定表示）/ 内部スロット（生存者を 0 ベース連番）の 3 レイヤー分離（[320 §2.1.1](320_vsprototype_combat_spec.md) / §6.14）を実体化する「内部スロット解決ヘルパ」が独立して必要** と判断し、`InternalSlotResolver` を分離新設。

設計効果：
- 後続 Step 2-4 反撃（被弾側 AttackKind 判定）・Step 2-5 シナジー（属性体数カウント）・Step 2-7 ターゲティング（最前/最後尾敵）すべてで「内部 slot index」の同一概念を再利用できる
- 純関数のため副作用テスト不要

【Step 2-3：配置 ATK 補正＋内部スロット解決ヘルパ実装】（コミット `085210f`）

- 新規 `Assets/Scripts/Domain/Battle/Refactored/PositionAtkCorrection.cs`
  - 距離 0..5 のカーブ `[1.0, 1.0, 0.95, 0.85, 0.75, 0.65]`
  - `GetCorrection(internalSlotIndex, aliveCountOnSide, AttackKind)`：近接=internalSlotIndex / 遠隔=`(aliveCount-1) - internalSlotIndex` で距離算出
  - `GetCorrectionByDistance(distance)`：負値は先頭値、6+ は末尾値にクランプ（例外なし）
  - 少人数編成での自然な「補正消失」（3 体編成で最大 -5%）は距離算出ロジックで自動成立（特別な分岐不要）
- 新規 `Assets/Scripts/Domain/Battle/Refactored/InternalSlotResolver.cs`
  - `GetInternalSlotIndex(IList<RuntimeUnit>, RuntimeUnit)`：生存ユニットを SlotIndex 順にソート→ target の 0 ベース内部 index を返す。死亡 / 自陣にいない / null → -1
  - `GetAliveCount(IList<RuntimeUnit>)`：null 安全
- 単体テスト 2 ファイル 16 件：
  - `Step2_3_PositionAtkCorrectionTests`：近接 6 体 6 件（TestCase 利用）／遠隔 6 体 2 件／少人数 3 体 4 件（近接 + 遠隔×最前/最後尾）／クランプ 2 件＝**11 件**
  - `Step2_3_InternalSlotResolverTests`：空き SlotIndex 詰め込み／中央死亡で詰め直し／自陣外 target / 全員死亡時カウント 0 / 部分死亡時の生存数 / null 安全＝**6 件**
  - 合計 17 件（事前見積もり 18 件と整合）
- 既存 .meta 追加（Step 2-2 で生成された `Refactored.meta` / `DamageFormula.cs.meta` / `Step2_2_DamageFormulaTests.cs.meta`）

【Step 2-3 で未着手・後続 Step に持ち越し】
- DamageFormula との結線：Phase R-2 後半 `Refactored.BattleManager` 組み立て時に `positionAtkCorrection` 引数経由で配線（純関数同士の組み合わせなのでテスト容易）
- ターゲティング解決（FromFront=最前敵 / FromBack=最後尾敵）：Step 2-7 で `InternalSlotResolver` を再利用して別ヘルパ実装
- BattleContext との関係：戦闘中欠けの動的再計算は `BattleContext.GetAliveAllies/Enemies` を `InternalSlotResolver` に渡すだけで成立。`BattleContext` 側にメソッド追加は **不要**（純関数を呼ぶだけ）

### 2026-06-15 セッション 第 3 段（Phase R-2 Step 2-4 完了）

【仕様確定：反撃発動条件】

320 §3.2 の「反撃の発動者：AttackKind=Melee のユニットのみ」とリード文「近接攻撃を受けた者は即座に反撃する」の整合性をユーザー確認した結果、**解釈 2（攻撃側＆被弾側ともに Melee の AND 条件）が正確** と確定：

- 弓兵が近接タンクを撃った時：反撃なし（タンクが Melee でも、攻撃側が Ranged なので反撃なし）
- 近接タンクが弓兵を殴った時：反撃なし（攻撃側 Melee でも、被弾側 Ranged なので反撃なし）
- 近接同士の局面のみ反撃発生：「近接 vs 近接で殴り合う時だけ反撃」というシンプルな視覚的ルール

戦術的帰結：
- 遠隔ユニット（弓兵・魔導士・暗殺者の被弾側）は反撃を授受しない＝遠隔の優位性
- タンクが「ATK 0 でも近接反撃源として機能」するのは近接 vs 近接の局面のみ
- 暗殺者（Melee + FromBack）が敵後衛（Ranged）を殴った時は反撃なし＝後衛狩りが機能

仕様書追従：
- 320 §1.1（AttackKind 行）／§3.2（反撃条件表・「攻撃側 Melee かつ 被弾側 Melee」を明示）／§3.4（slot 1 の最適位置説明をより厳密に）／§8.3（行動行）
- 911 §2.3（反撃 Waza 行）／§5（ActionExecutor.cs 行）

【Step 2-4：反撃判定純関数の実装】（コミット `fcf50b4`）

- 新規 `Assets/Scripts/Domain/Battle/Refactored/CounterAttackResolver.cs`
  - `CanCounterAttack(attacker, defender)`：attacker.IsAlive && defender.IsAlive && attacker.AttackKind == Melee && defender.AttackKind == Melee の AND
  - `ResolveCounterWaza(defender)`：defender.CounterWaza ?? Waza.DefaultCounter（null 安全）
  - シールド吸収は HP 減らず defender.IsAlive で自動成立（攻撃された事実で反撃する）
- 新規 `Assets/Tests/Editor/Domain/Step2_4_CounterAttackResolverTests.cs`：11 件

  | # | 検証内容 |
  |---|---|
  | 1 | 攻撃側 Melee × 被弾側 Melee 双方生存→反撃発動 |
  | 2 | 攻撃側 Ranged × 被弾側 Melee→反撃しない（遠隔は反撃を受けない設計） |
  | 3 | 攻撃側 Melee × 被弾側 Ranged→反撃しない（発動者条件） |
  | 4 | 攻撃側 Ranged × 被弾側 Ranged→反撃しない |
  | 5 | 被弾側死亡→反撃しない |
  | 6 | 攻撃側死亡→反撃しない |
  | 7 | null 入力安全 |
  | 8 | CounterWaza 未設定→ DefaultCounter |
  | 9 | CounterWaza 設定済→ 個別 Waza |
  | 10 | ResolveCounterWaza(null) → DefaultCounter（null 安全） |
  | 11 | DefaultCounter.Category == Counter（反撃経路で再確認） |
- Step 2-3 で生成された `.meta` を同コミットに取り込み

【Step 2-4 で未着手・後続 Step に持ち越し】
- 多段攻撃ループ内での「死亡したら以降の hit 打ち止め」：呼び出し側責務として Step 2-7 BattleManager 組み立て時に hit ループ内で `attacker.IsAlive` 確認
- ReactionStack への反撃 Action 積み込み経路：Step 2-7 で BattleManager の OnHitLanded 経由で結線
- 反撃ダメージ計算経路：`DamageFormula.ComputeBaseDamage(attackerAtk=defender.EffectiveATK, wazaMultiplier=counterWaza.倍率, defenderCurrentHp=attacker.CurrentHP, ...)` で純関数連結（攻撃者と被弾者が反撃時に逆転する点だけ呼び出し側で配線）
- ログ集約「A の攻撃 → B に xx ダメージ／B の反撃で A に xx ダメージ」1 行表示：Step 2-7 で BattleLogFormatter に反撃結合フォーマッタを追加

### 2026-06-15 セッション 第 4 段（Phase R-2 Step 2-5 Phase A 完了）

【設計確定：バフ二系統ルール】

§4 Step 2-5 着手時に **仕様 320 §1.4「割合バフ」と現実装 EffectiveATK 加算式の不整合**が顕在化。ユーザー判断で以下を確定（§6.15 詳細）：

- 基礎ステータス（ATK/DEF/HP/SPD）の強化は **加算のみ・乗算禁止**
- 乗算が許されるのは **最終ダメージ出力に対してのみ**（与ダメ%・被ダメ%・クリティカル）
- 最終出力順序：与ダメ% → 被ダメ%（キャップ 80%）→ クリティカル（最後に乗算）
- 与ダメ% / 被ダメ% は加算スタック

これにより、火属性シナジーは「ATK バフ」ではなく「**与ダメ% 増加バフ**」として実装。`StatusEffectType.OutgoingDamageUp` を新設し、`DamageModifier` 純関数で最終出力に加算スタック乗算する設計に切り替え。

【Phase A 分割と後送り判断】

Step 2-5 は工数感サマリで「大」評価のため A/B/C に分割：

- **Phase A**：シナジー共通基盤（SynergyConstants / ElementCounter）＋火属性 Processor ＋与ダメ% 機構（DamageModifier）
- **Phase B**：水属性 Processor ＋シールド消費純関数
- **Phase C**：補助属性プレースホルダ

被ダメ% (`IncomingDamageDown`) / キャップ 80% / クリティカルは **呼び出し元 Processor が存在しないため後送り**（YAGNI / [feedback_no_technical_debt] と整合）。仕様 320 §1.4.1 には完全な式と順序を残し、将来実装者が `DamageModifier` シグネチャを破壊的拡張すれば追加できる設計。

【数値暫定値】（Phase R-6 で再調整）

- 火属性：5/10/20/35/50%（2/3/4/5/6 体段階）＋バフ対象数 1/1/2/2/2。低段階保守的・5/6 でジャンプ
- 水属性：DEF 加算 10/15/20/25/30＋ Shield 0/0/1/2/3
- フレーバー名：「炎の共鳴」「水の共鳴」（Phase R-3 で再考可能）

【Step 2-5 Phase A 実装】（コミット `f2b2931`）

- `Enums.cs`：`StatusEffectType.OutgoingDamageUp` 1 行追加
- 新規 `Refactored/DamageModifier.cs`：純関数 `ApplyOutgoingMultiplier` ＋ `GetOutgoingBonusRate`
- 新規 `Refactored/Synergy/SynergyConstants.cs`：火・水暫定数値＋ AuraSourceId / SourceAbilityName 定数
- 新規 `Refactored/Synergy/ElementCounter.cs`：陣営別 Element 体数カウント純関数
- 新規 `Refactored/Synergy/FireSynergyProcessor.cs`：`ConditionalBuffProcessor` 継承・両陣営独立カウント・ATK 上位 N 体に OutgoingDamageUp 付与・AuraSourceId 一致で差分判定

【テスト 21 件】

  | ファイル | 件数 | 主な検証 |
  |---|---|---|
  | `Step2_5A_ElementCounterTests` | 4 | 火3水2分離／死亡除外／null 安全／空リスト |
  | `Step2_5A_DamageModifierTests` | 6 | バフ無し素通し／20%→120／加算スタック 10+20=130／Stacks 2 倍カウント／生ダメ 0／null 安全 |
  | `Step2_5A_FireSynergyTests` | 11 | 0〜6 体カーブ／陣営分離／ATK 同値時の SlotIndex タイブレーク／最 ATK 死亡で再評価／3 連続 Refresh の冪等性 |

【Step 2-5 Phase A で未着手・Phase B/C に持ち越し】

- WaterSynergyProcessor / ShieldConsumer：Phase B
- SubSynergyProcessor プレースホルダ：Phase C
- Refactored.BattleRunner / ActionExecutor への結線：Phase R-2 後半 Refactored.BattleManager 組み立て時

### 2026-06-15 セッション 第 5 段（Phase R-2 Step 2-5 Phase B 完了）

【Step 2-5 Phase B 実装】（コミット `6a42ed2`）

水属性シナジー Processor とシールド消費純関数を新設。仕様 320 §4.1 水属性表の DEF
固定値加算（既存 `DefenseUp` 流用）と Shield Stacks 動的管理（既存 `StatusEffectType.Shield`
＋ `RuntimeUnit.ShieldStacks` 流用）で実装。

- 新規 `Refactored/Synergy/WaterSynergyProcessor.cs`
  - `ConditionalBuffProcessor` 継承・Hooks: BattleStart / UnitDied / BuffApplied / BuffRemoved
  - 両陣営独立カウント・体数 → DEF 加算 10/15/20/25/30 ＋ Shield 0/0/1/2/3 をテーブル参照
  - 自陣営の全生存ユニット（属性問わず）に付与（水属性メンバー以外も恩恵）
  - 2 系統 AuraSourceId（def_buff / shield）で剥奪→再付与の差分判定
  - Shield 付与は `CreateConditional` 後に Stacks を残数で上書き（ファクトリの Stacks=1 既定を直書き調整）
- 新規 `Refactored/ShieldConsumer.cs`
  - 純関数 `Consume(incomingDamage, defender)` で `Result(FinalDamage, ShieldConsumed)` を返す
  - Shield Stacks 1 以上なら Stacks -1 で Damage=0 化・Stacks=0 で剥奪
  - 反撃発動は呼び出し元責務（Shield 吸収でも「攻撃された事実」は変わらない）

【テスト 17 件】

  | ファイル | 件数 | 主な検証 |
  |---|---|---|
  | `Step2_5B_WaterSynergyTests` | 11 | 0〜6 体カーブ（DEF 加算＋ Shield Stacks）／水属性以外も恩恵享受／陣営分離／3→2 体減で再評価／3 連続 Refresh の冪等性（DefenseUp / Shield 各 1 件のみ） |
  | `Step2_5B_ShieldConsumerTests` | 6 | Shield 無し素通し／Shield 1 で吸収後剥奪／Shield 3 で吸収後 Stacks 2 残存／4 ヒット目素通し（Shield 全消費後）／生ダメ 0 で消費なし／null defender 素通し |

【Step 2-5 Phase B で未着手・Phase C / Refactored.BattleManager に持ち越し】

- SubSynergyProcessor プレースホルダ：Phase C
- WaterSynergy / FireSynergy の結線（`conditionalProcessors` 配列追加）：Phase R-2 後半 Refactored.BattleManager 組み立て時
- ShieldConsumer の結線（ActionExecutor 被弾処理）：同上。被弾ループで `var r = ShieldConsumer.Consume(damage, defender); if (r.ShieldConsumed) emitShieldAbsorbedEvent(); else damage = r.FinalDamage;` のように呼び出す

【仕様書修正なし】

仕様 320 §4.1 水属性表は Step 2-5 Phase A コミット `8e54d4c` で既に新方式（DEF 加算＋
Shield Stacks 残数）に書き換え済のため、Phase B では実装側のみ追従。911 §4 Step 2-5
の Phase B 表も Phase A コミットで先に書き直し済み。

### 2026-06-15 セッション 第 6 段（Phase R-2 Step 2-5 完了・Phase C 委譲判断）

【設計判断：Phase C スキップ＆委譲】

§4 Step 2-5 で Phase C として予定していた `SubSynergyProcessor` プレースホルダ（空 Refresh
＋ Phase R-3 詳細仕様待ちの 1 行コメント）は、以下と矛盾するため**実装をスキップ**し
Phase R-3 へ委譲する：

- [900 §7.9] コード内コメントから「将来〜」「Phase R-3 で実装」等の未来計画コメント禁止
- [feedback_no_technical_debt] 空クラス＋ TODO コメントは負債そのもの
- 「Don't add features... Don't design for hypothetical future requirements」（CLAUDE.md）

【方針確定】

補助属性 Processor は Phase R-3 で補助属性数値仕様の確定と同時に実装＋テストを 1 セット
で作成する。Step 2-5 は **Phase A / Phase B で実質完了**。911 §4 Step 2-5 の Phase C
記述も「Phase R-3 へ委譲」に書き換え済。

【未着手項目（Phase R-2 残）】

- Refactored.BattleManager の組み立て（`conditionalProcessors` 配列・OnHitLanded での
  ShieldConsumer 配線・DamageModifier の最終出力乗算配線）：Phase R-2 後半 Refactored.BattleRunner
  の構築時に対応
- Step 2-6 地形システム（`TerrainKind` enum＋ `BattleContext.Terrain`＋ MapNode 追従＋
  ダメージ式の環境項配線）
- Step 2-7 引き分け非対称評価＋ SPD タイブレーク

### 2026-06-15 セッション 第 7 段（Phase R-2 Step 2-6 完了）

【Step 2-6：地形システム純関数＋ダメージ式分母クランプ】（コミット `eb119e8`）

Refactored 純関数で地形補正の算出を実装し、Step 2-2 で見落としていた DamageFormula
の分母 0 以下事故を同時に修正。BattleContext / MapNode / BattleRunner.Run シグネチャ /
UseCase ランダム化は Refactored.BattleManager 組み立て時か Phase R-3 へ後送り。

- 新規 `Refactored/Terrain/TerrainKind.cs`：enum Neutral / FireAdvantage / WaterAdvantage
- 新規 `Refactored/Terrain/TerrainStrength.cs`：enum Light=0 / Medium=1 / Heavy=2
- 新規 `Refactored/Terrain/TerrainConstants.cs`：強度別 α 値テーブル 5/10/15
- 新規 `Refactored/Terrain/TerrainBonusCalculator.cs`：純関数 `GetTerrainBonus`
  - 自属性 +α / 逆属性 -α / 補助属性（None / Light / Dark 等）および中立地形は 0
- 改修 `Refactored/DamageFormula.cs`：分母 1 クランプ追加
  - 負の terrainBonus + DEF=0 の組み合わせで分母が 0 以下になる除算事故を構造防止
  - 最終的に被ダメ青天井を許容しつつ除算事故だけは構造的に防ぐ設計

【テスト 13 件】

  | ファイル | 件数 | 主な検証 |
  |---|---|---|
  | `Step2_6_TerrainBonusCalculatorTests` | 11 | 中立地形 2／自属性 +α 3（Light/Medium/Heavy）／逆属性 -α 3／補助属性 None/Light/Dark 補正なし 3 |
  | `Step2_2_DamageFormulaTests` 追加分 | 2 | terrainBonus=-15 + DEF=0 で分母 1 クランプ・極端値 terrainBonus=-1000 でも同じく 1 クランプ |

【仕様書修正】

- 320 §5.1：暫定 α 値テーブル（Light 5/ Medium 10/ Heavy 15）＋補助属性は地形補正なし明示
- 320 §5.3：分母 1 クランプ仕様を追記（除算事故防止・被ダメ青天井は許容）
- 320 §8.3：地形 行を `TerrainBonusCalculator` 純関数の責務に書き換え＋ 分母 1 クランプ行追加
- 911 §4 Step 2-6：純関数スコープ／結線後送りに分割

【Step 2-6 で未着手・後送り】

- `BattleContext.Terrain` / `Strength` プロパティ追加：Refactored.BattleManager 組み立て時
- `BattleRunner.Run` シグネチャ拡張（terrain / strength 引数・デフォルト引数で後方互換）
- `MapNode.Terrain` プロパティ追加：UseCase 側で MapNode に持たせる時
- ラン開始時の地形ランダム化（VSPrototypeRoundManager）：全 3 属性 1 列ずつ保証の制約パターン
- 敵編成の地形追従：Phase R-3 ユニット仕様策定と連動

### 2026-06-15 セッション 第 8 段（Phase R-2 Step 2-7 完了・Phase R-2 純関数群完了）

【Step 2-7：引き分け非対称評価＋ SPD タイブレーク 純関数】（コミット `7f306fd`）

ターン制限到達時の引き分け非対称評価と、行動順決定の完全決定論タイブレークを
Refactored 純関数として実装。BattleContext の改修は後送り。

- 新規 `Refactored/VictoryEvaluator.cs`：純関数 `IsAdvantageousVictory(allyKillCount, allyDeathCount, isAttackingSide)`
  - 攻め側：1 体以上撃破していれば勝利／0 撃破は時間切れ負け（持久タイムアウト逃げ防止）
  - 守り側：1 体も被撃破されていなければ勝利（防衛の価値明確化）
- 新規 `Refactored/SpdOrderResolver.cs`：純関数 `OrderByTurnPriority`
  - SPD 降順 → 陣営間タイブレーク（攻め側優先）→ 同陣営内 slot 昇順の 3 段階決定論
  - `getSpd` 省略時は `BaseUnit.BaseSPD`、凍結等の実効 SPD 計算は呼び出し側責務（責務分離）
  - 死亡ユニット自動除外・null 入力安全

【テスト 15 件】

  | ファイル | 件数 | 主な検証 |
  |---|---|---|
  | `Step2_7_VictoryEvaluatorTests` | 7 | 攻め側 4（1撃破勝利／複数撃破被撃破でも勝利／0撃破負け／0撃破1被撃破負け）／守り側 3（0被撃破勝利／撃破あり0被撃破勝利／1被撃破負け） |
  | `Step2_7_SpdOrderResolverTests` | 8 | SPD 降順／同 SPD で味方攻め時 vs 敵攻め時の陣営優先逆転／同 SPD 同陣営は slot 昇順／死亡除外／getSpd 差し替え（凍結相当）／null 安全／完全決定論 |

【Step 2-7 で未着手・後送り】

- `BattleContext.IsAttackingSide` プロパティ追加：Refactored.BattleManager 組み立て時
- `BattleContext.IsAdvantageousVictoryCondition` 改修（VictoryEvaluator 呼び出しに差し替え）：同上
- Refactored.BattleManager の SPD 順決定ロジック：SpdOrderResolver 呼び出し＋ Waza.SPD / 凍結等加味した getSpd 関数を渡す
- VSPrototypeRoundManager：MapNode 種別から攻め/守り判定＋ BattleContext.IsAttackingSide セット

【Phase R-2 純関数群 完了】

Step 2-1 〜 2-7 で Refactored 配下に純関数群が揃った。残るは **Refactored.BattleManager**
本体の組み立て（Phase R-1-3 で骨格作成済の `BattleAssembly` を新版で再構築＋
本セッション群で実装した純関数を結線）。

| 純関数 / クラス | 責務 |
|---|---|
| `DamageFormula` | ダメージ算出（分母 1 クランプ済） |
| `DamageModifier` | 与ダメ% 加算スタック乗算 |
| `PositionAtkCorrection` / `InternalSlotResolver` | 配置 ATK 補正＋内部スロット解決 |
| `CounterAttackResolver` | 反撃発動判定＋反撃 Waza 解決 |
| `FireSynergyProcessor` / `WaterSynergyProcessor` | 火属性 / 水属性シナジー Processor |
| `ShieldConsumer` | Shield 消費判定 |
| `Terrain.TerrainBonusCalculator` | 地形補正（環境項） |
| `VictoryEvaluator` | 引き分け非対称評価 |
| `SpdOrderResolver` | 行動順決定（SPD タイブレーク） |

### 着手済み Phase

Phase R-1 完了（2026-06-14）
Phase R-2 Step 2-1 完了（2026-06-14）
Phase R-2 Step 2-2 完了（2026-06-15）
Phase R-2 Step 2-3 完了（2026-06-15）
Phase R-2 Step 2-4 完了（2026-06-15）
Phase R-2 Step 2-5 完了（2026-06-15・Phase A/B 実装・Phase C は Phase R-3 へ委譲）
Phase R-2 Step 2-6 完了（2026-06-15・Refactored 純関数・結線は後送り）
Phase R-2 Step 2-7 完了（2026-06-15・Refactored 純関数・結線は後送り）
**Phase R-2 純関数群 完了**（残：Refactored.BattleManager 組み立て＋結線）

### 2026-06-15 セッション第 9 段（Phase R-3 Phase A/B/C 完了）

ユーザーから 17 体ユニット案（ブリジット／双炎剣士／焦熱魔導士／火矢弓兵／焔影刺客／火槍盾兵／焔鼓舞師／霧刃剣士／水穿弓兵／水盾守護兵／幻水盾兵／水護術士／水癒巫女／王女／聖盾騎士／聖光司祭／聖輝導師）が提示され、未定だった戦闘メカ仕様を Q1〜Q15 で全確定後、Phase R-3 に着手。

**320 仕様確定追記**（コミット `3fc1c89`・212 行追加）：

- §1.4 バフ・デバフ式を 3 系統に整理（基礎加算 / 最終出力割合 / 確率パラメタ）
- §1.4.1 クリ基本値（基本クリ率 0% / 基本倍率 ×1.5）
- §1.4.2 イベイド仕様（Magnitude=% 加算スタック・キャップ 50%・プロト範囲遠隔限定）
- §1.4.3 状態異常付与の暫定既定値（3T / キャップなし / 付与確率 100%）
- §1.4.4 Burn 蓄積モデル（自然治癒なし・スタック上限なし・cleanse 専用）
- §3.2 範囲攻撃の反撃ルール追記
- §3.5 防御フォールバック新節
- §4.2 補助属性を光属性 4 体構成に書き換え（ターン終了時 HP% 全体回復・2/3/5%）
- §4.3 ユニット構成 17 体に更新
- §4.4 マトリクス光属性追加
- §4.7 解除経路 3 系統と 5 分類
- §4.8 ユニットラインナップ 17 体（役割・属性・主スキル）
- §8.1 Burn 仕様差し替え
- §8.3 新規実装対象（多段・範囲・状態異常 enum・解除経路・イベイド・専守メカ）

**Phase R-3 Phase A**（基盤拡張・コミット `544de70`）：

- `StatusEffectType` に 5 種追加：IncomingDamageDown / CriticalRateUp / HealReceivedDown / CounterDamageUp / SilencedCounter
- `DamageModifier.ApplyIncomingMultiplier` 追加（被ダメ% 加算スタック・キャップ 80%）
- テスト 10 件追加（Step3A_IncomingDamageDownTests）

**Phase R-3 Phase B**（光属性シナジー・コミット `a2cd5b4`）：

- `StatusEffectType.HealOverTime` 新設（ターン終了時 HP% 回復・実際の処理は Phase D の StatusEffectProcessor で実装する分離設計）
- `SynergyConstants.SubHealOverTimePercentByCount = [0, 0, 2, 3, 5, 5, 5]`
- `SubSynergyProcessor` 新規（ConditionalBuffProcessor 派生・既存パターン完全踏襲）
- テスト 11 件追加（Step3B_SubSynergyTests）
- 設計判断：「ターン終了時の HP 操作」を Processor に直書きせず、`StatusEffectType.HealOverTime` として StatusEffect で表現し、StatusEffectProcessor で Burn と対称処理することで責務分離

**Phase R-3 Phase C-1**（IActionEffect 基盤・コミット `c91744d` / `017921b` / `bc8f189`）：

- 新規ファイル：`IActionEffect` / `IActionContext` / `HealEffect` / `ApplyStatusEffectEffect` / `DispelBuffsEffect` / `DispelDebuffsEffect` / `CleanseStatusAilmentsEffect` / `StatusEffectStacker`
- StatusEffectStacker は旧 _old ActionExecutor.ApplyEffectWithStacking の Refactored 純関数化
- C-1a は当初 DamageEffect として実装したが、C-4 着手前に**再設計**（後述）

**Phase R-3 Phase C-2**（Waza・コミット `43ce60c`）：

- `RefactoredWaza` / `RefactoredRuntimeWaza` 新規（Effects: IList<IActionEffect> 一本化）
- `TargetingType.DirectionalEnemies` 追加（範囲攻撃用）
- 旧 WazaCategory / CalculateBaseDamage Func / AppliedEffects / DispelsBuffs フラグ等を全て Effects に統合（責務一本化）
- `DefaultCounter` 静的 Waza（共通フォールバック反撃）
- テスト 14 件追加

**Phase R-3 Phase C-3**（TargetEvaluator・コミット `1bf8657` / `fcaf6aa` / `a2b6888` / `35e377d`）：

- `RefactoredActionDeclaration` / `RefactoredTargetEvaluator`
- GetValidTargets：Self / SingleEnemy / SingleAlly / AllEnemies / AllAllies / DirectionalEnemies に対応
- `TargetSelection` enum 追加（Default / LowestHpRatio / HighestAtk / HighestDef）→ Waza 別の対象選定戦略を表現
- DeclareAction：行動不能判定 → IsForcedWhenReady 優先発動 → 通常評価（BattleWazas 順）→ def_guard フォールバック
- 重大設計判断：**AI スコア評価・支援優先順位判断は実装しない**（[[project-combat-randomness-policy]] 推測容易性の原則）。〇ターンおきの技は `IsForcedWhenReady + Cooldown` で表現可能と判明
- def_guard は TargetEvaluator 内 private static で内製
- テスト 41 件追加（C-3a 12 + C-3b 13 + C-3c 16）

**Phase R-3 Phase C-4**（ActionExecutor + AttackEffect 再設計・コミット `9622582` / `33609cb` / `d8d307e` / `bac5e25` / `ff38a5e` / `43c3ff2` / `594648b` / `aa5e7db`）：

- C-4a 着手後、命中判定・クリ・付帯効果・反撃の**依存連鎖を Effect 並列モデルでは表現できない**と判明
- **案 X AttackEffect 採用**：攻撃チェーンを 1 つの大型 Effect に凝集（命中→配置/地形動的補正→ DamageFormula →与/被ダメ%→クリ→ Shield → ダメ適用→ onHitRiders →反撃）
- 旧 DamageEffect 廃止、Phase C-1a テストも書き直し（手戻り受容）
- Rider パターン：onHitRiders として既存 Effect（ApplyStatusEffectEffect 等）を渡せる → 「攻撃+Burn付与」「攻撃+ATKデバフ」等を自由表現
- HitOutcome に `IsCritical` プロパティ追加
- `BattleContext` に `Terrain` / `TerrainStrength` プロパティ追加
- `DamageModifier.ApplyCritical` / `ApplyCounterMultiplier` 追加（クリ倍率 1.5 / CounterDamageUp）
- 配置 ATK 補正・地形補正は AttackEffect 内で BattleContext から動的算出
- 反撃発動：CounterAttackResolver.CanCounterAttack → 同クラスの内部コンストラクタで isCounterAttack=true で再呼び出し（反撃の反撃なし）
- Waza.IsSureHit を廃止し AttackEffect コンストラクタに移管（責務凝集）
- 確率方針：[[project-combat-randomness-policy]] 過剰なランダム性は排除（順序系決定論）／クリ率・回避率の確率要素は許容／Func<int> Random0To99 注入方式継続
- 320 §3.1「ランダム性は排除」を「過剰なランダム性は排除」に微修正
- テスト 41 件追加（C-4a 13 + C-4b 14 + C-4c 14）
- テスト失敗対応：Step3C1a / Step3C4a の Make ヘルパーを AttackKind=Ranged に変更（反撃発動で Outcome 数が変動する問題）／Step3C4c の丸め方式を AwayFromZero に統一

**Phase R-3 Phase C-5**（SilencedCounter・コミット `2e2c176`）：

- `CounterAttackResolver.CanCounterAttack` に `HasSilencedCounter(defender)` チェック追加
- 水盾守護兵「専守」のパッシブ：Persistent で SilencedCounter 付与＋ IncomingDamageDown Magnitude=30 で「反撃なし＋被ダメ -30%」を表現
- テスト 5 件追加

**Phase R-3 Phase A/B/C 累計**：

- コミット 22 件
- テスト 141 件
- 重大設計判断 3 件：
  1. AttackEffect 案 X 採用（攻撃チェーン凝集）
  2. AI スコア評価・支援優先順位判断は実装しない（推測容易性原則）
  3. HealOverTime を StatusEffect 化して Burn と対称処理（責務分離）

### 2026-06-15 セッション 第 10 段（属性シナジー再設計：データ駆動 + 静的 Applier）

【背景】

Phase R-3 Phase A/B/C で実装した属性シナジー Processor 群（Fire/Water/Sub）が
`ConditionalBuffProcessor` 派生で 4 Hook（BattleStart/UnitDied/BuffApplied/
BuffRemoved）を購読し、`RemoveEffectsWhere → 再付与` の差分判定を行う設計に
なっていた。これは「属性シナジーは戦闘中に動的変動する」前提だったが、ユーザー
確認で **「戦闘開始時に確定して戦闘終了まで永続・解除されない」仕様** に確定。
動的再評価機構はまるごと過剰設計と判明し、ゼロから再設計に踏み切った。

【再設計（コミット `c7c7cfc` / `322059b` / `9845acf`）】

データ駆動 + 静的 Applier への置換：

| 新規ファイル | 責務 |
|---|---|
| `Refactored/Synergy/SynergyBuff.cs` | 単一効果 POCO（Type / Magnitude / InitialStacks） |
| `Refactored/Synergy/SynergyTier.cs` | 体数別段階 POCO（Buffs[] / TargetCount / SortBy）。TargetCount=-1 で全員 |
| `Refactored/Synergy/SynergyDefinition.cs` | 1 属性の完全定義 POCO（TriggerElement / SourceAbilityName / Tiers[]） |
| `Refactored/Synergy/SynergyDefinitions.cs` | 静的データ（Fire / Water / Light / All） |
| `Refactored/Synergy/SynergyApplier.cs` | 静的純関数 `ApplyAll(BattleContext, IEnumerable<SynergyDefinition>)`（BattleStart で 1 回） |

削除：
- FireSynergyProcessor / WaterSynergyProcessor / SubSynergyProcessor
- SynergyConstants（数値は SynergyDefinitions に吸収・AuraSourceId 系は不要に）
- Step2_5A_FireSynergyTests / Step2_5B_WaterSynergyTests / Step3B_SubSynergyTests
  → Step2_5_SynergyApplierTests 20 件に統合（共通3 + 火6 + 水5 + 光3 + 両陣営1 + 死亡除外1 + 静的維持1）

付与方式：`StatusEffect.CreatePersistent`（`IsUndispellable=true`・永続・SourceAbilityName で識別）。

`SortBy` は既存 `TargetSelection` enum 流用（HighestAtk 等）。「全員」は TargetCount=-1。

【ConditionalBuffProcessor 基底フレームワークは残置】

ユーザー判断で「いずれ動的バフが必要なユニットを作るので、仕組み自体は残しておく」方針。
`ConditionalBuffProcessor` / `ConditionalBuffHook` / `BuffCategory.Conditional` /
`StatusEffect.CreateConditional` / `AuraSourceId` プロパティはすべて残置。
Refactored の派生クラスは 0 だが、過去の旧戦闘期の派生 4 種（_old/Conditional/）が
参考実装として隔離済。

【影響箇所の追従】

- `Step3C1a_AttackEffectTests` の Shield 生成箇所を `CreateConditional` →
  `CreatePersistent` に書換（属性シナジーが Persistent 化したため整合）
- 320 §4.7 5 分類表 Conditional 行を「プロト範囲では未使用・将来用フレームワークのみ残置」に書換
- 320 §4.1 水属性「シールド実装方式」を Conditional→Persistent / 動的→静的 Applier に書換
- 320 §8.3 「属性シナジー」「シールド」行を新方式に書換
- 500 §4.4.5 ConditionalBuffProcessor 実装例から Refactored.Synergy.* を削除し、
  「現在 Refactored に派生なし・基底のみ残置」と注記
- 500 §4.5.5 新設「属性シナジー（静的 Applier・データ駆動）」

【設計効果】

- 新属性追加 = `SynergyDefinitions` に 1 要素追加するだけ
- 体数別効果値変更 = テーブル書き換えのみ
- 推測容易性の原則と整合（戦闘中の体数変動でバフ量が動かない）
- 拡張余地：将来「特定属性のみ動的にしたい」が来たら、`SynergyDefinition` に
  `IsDynamic` フラグ＋該当 Definition のみ `ConditionalBuffProcessor` 派生を別途
  作るパターンで疎結合に拡張可能

【次セッション起点】

属性シナジー再設計完了。残るは Phase R-2 仕上げ（Refactored.BattleManager 組み立て
＋結線）。サブステップ 2X-1〜2X-6 で進める想定：
- 2X-1: BattleContext 改修（Terrain / Strength / IsAttackingSide / VictoryEvaluator 結線）
- 2X-2: StatusEffectProcessor 新設（OnEndPhase の Burn 蓄積＋ HealOverTime 回復＋時限減算）
- 2X-3: ActionExecutor 新設（Effects 順次 Apply・OnHitLanded で ReactionStack 積み）
- 2X-4: BattleManager 新設（SPD 順 / DeclareAction 呼出 / ProcessReactionStack）
- 2X-5: BattleAssembly + BattleEventRecorder 新設（**SynergyApplier.ApplyAll を BattleStart で呼ぶ結線もここ**）
- 2X-6: BattleRunner.Run 新設（複数ターン進行＋終了判定＋地形/攻め守り引数）

### 2026-06-15 セッション 第 11 段（Phase R-2 仕上げ 2X-1〜2X-6 完了）

【サブステップ実装】

| Sub | 内容 | コミット |
|---|---|---|
| 2X-1 | BattleContext に IsAttackingSide 追加 / IsAdvantageousVictoryCondition を VictoryEvaluator 委譲化 | `aaa4ce9` |
| 2X-2 | Refactored.StatusEffectProcessor 新設（OnEndPhase で Burn → HealOverTime → 凍結 -1 → 時限 -1） | `2665589` |
| 2X-3 | RefactoredActionExecutor にイベント発火追加（OnActionResolved + OnUnitDied） | `810c67f` |
| 2X-4 | RefactoredBattleManager 新設（旧 BattleManager の重い責務を全廃したシン・コーディネーター） | `c3d3b9f` |
| バグ修正 | 敵視点ターゲット選定（BuildDeclarationsFor を ownSide/opponentSide 引数に） | `9dbc8dd` |
| 2X-5a | RefactoredBattleAssembly 新設（生成＋ WireBattleLogic で 5 経路結線） | `94dd467` |
| 2X-5b | RefactoredBattleEventRecorder 新設（BattleReport 書き出し・OnHealOverTimeTick 購読追加） | `cdaa43b` |
| 2X-6 | RefactoredBattleRunner.Run 新設（約 90 行・1 戦闘完結） | `5a0fb85` |

【設計判断のハイライト】

- **Shield 強化方向の調整**：Burn（DOT）は Shield 貫通で HP 直撃に変更。Shield は攻撃ダメージのみ吸収する仕様に純化（仕様 320 §1.4.4 / §4.1 / §8.3 追記）
- **HealOverTime 順序**：Burn → HealOverTime → 凍結 -1 → 時限 -1。先に Burn 致死を確定させてから回復処理（仕様 320 §4.2 追記）
- **OnHitLanded 属性連動状態異常解除を完全削除**（仕様 320 §8.1 で属性連動廃止確定）
- **個別 OnHitLanded / OnHitEvaded / OnHealed イベントを設けない**：HitOutcome が
  Damage / HealAmount / WasEvaded / ResultedInDeath / AppliedEffects を保持するため
  Recorder 側で抽出可能（責務集中）
- **反撃は AttackEffect 内部完結・ReactionStack 経路廃止**
- **属性シナジー結線は OnBattleStart で 1 回呼ぶ**（SynergyApplier.ApplyAll）
- **敵視点のターゲット選定**：BuildDeclarationsFor で ownSide / opponentSide を入れ替えて
  渡すことで、敵側ユニットが自陣営内から SingleEnemy を選ぶバグを構造防止

【Phase R-2 完了サマリ】

- コミット 8 件（2X-1〜2X-6 ＋バグ修正 1）
- テスト追加 100 件超（10/23/10/16+1/12/12/11）
- 新規ファイル：StatusEffectProcessor / RefactoredBattleManager /
  RefactoredBattleAssembly / RefactoredBattleEventRecorder / RefactoredBattleRunner
- 改修：BattleContext / RefactoredActionExecutor

旧 BattleRunner.Run 411 行 → 新 RefactoredBattleRunner.Run 約 90 行で
責務分離達成（[feedback_no_technical_debt] / 911 §6.10 で予告した目標サイズ達成）。

【次セッション起点】

Phase R-3 に着手：
- R-3-1：17 体ユニットの数値仕様策定（HP/ATK/DEF/SPD・各 Waza 倍率・反撃 Waza 特殊効果）
- R-3-2：新 AlliesRoster / EnemiesRoster 実装＋ Unit SO / Waza SO 生成（SoAssetGenerator 更新）
- R-3-3：UseCase 試遊復旧（VSPrototypeRoundManager.ResolveAllBattles の resolver
  を RefactoredBattleRunner.Run に差し替え）

---

## 8. 関連ドキュメント

- [320_vsprototype_combat_spec.md](320_vsprototype_combat_spec.md) 戦闘システム仕様（SSoT）
- [310_vsprototype_spec.md](310_vsprototype_spec.md) ラン進行・領地マップ
- [390_vsprototype_balance_notes.md](390_vsprototype_balance_notes.md) バランスメモ（旧戦闘期記録・新戦闘で再構築予定）
- [500_architecture.md](500_architecture.md) アーキテクチャ
- [900_development_rules.md](900_development_rules.md) 開発ルール
- [910_vsprototype_devlog.md](910_vsprototype_devlog.md) Claude 雑記帳（汎用・本書は戦闘再設計専用の派生）

### 2026-06-15 セッション 第 12 段（Phase R-3 完了：新 17 体 + 新 SO 5 層 + 試遊復旧）

【方針確定】

Phase R-3 を「旧資産を `_old/` に完全退避し、新型 SO 5 層パターンで再構築」する本来の R-3 スコープで実施
（当初は最小手術案を提案したがユーザー指摘で軌道修正）。`Refactored` namespace 自体は Phase R-5 まで維持
（namespace 一括削除は R-5 範囲）。

【コミット 16 件】

| 範囲 | コミット |
|---|---|
| R-3-1：320 §4.8 に 17 体 × 数値暫定値追記（基礎ステ＋ Waza 数値＋状態異常 Magnitude の 3 表） | `0923ad3` |
| R-3-2A：CounterAttackResolver からデッドコード ResolveCounterWaza 削除 | `2d73086` |
| R-3-2B：旧 Domain.Battle.ActionDeclaration を `_old/` 退避 | `7b4dc57` |
| R-3-2D/E/F/H/I：Unit/RuntimeUnit/IWazaCatalog 新型化＋旧 Skills/Data を `_old/` 退避＋ Echolos.Data.Old asmdef 新設 | `f4d742a` |
| R-3-2J/K：旧 Domain.Formula（DamageFormulaRegistry / TriggerConditionRegistry）＋旧 SO 依存テスト 6 件を `_old/` 退避 | `34ff848` |
| R-3-2L 前半：GuideContent / UnitBadgeOverlay の新型追従 | `c5a16b0` |
| R-3-4a/b/c：新 UnitDefinition / WazaDefinition POCO ＋ SO ラッパー ＋ Catalog 実装。Unit.PersistentEffects を新設 | `861c5a1` |
| R-3-5：新 AlliesRoster / EnemiesRoster / WazaRoster 新設＋旧 Domain.Prototype Roster と Bootstrap 等の追従 | `544a10d` |
| R-3-4d：Phase3SoGenerator 新設（Editor メニュー 1 発で Unit/Waza/DraftPool SO 一括生成） | `3626180` |
| R-3-6：VSPrototypeEnemyPatterns を IUnitCatalog 注入型に＋ Bootstrap 結線 | `c0ce385` |
| R-3-7/8/9：BattleResolver delegate 拡張（isAttackingSide + TerrainStrength）＋ RoundManager 追従＋ Bootstrap の ResolveBattle ラッパー（PrepareForBattle / battleWazasByUnit 構築） | `5f9586b` |
| R-3-10/11：Phase3SoGenerator に DraftPool 生成追加 | `5a98913` |
| R-3-12：IconRegistry に新 ID → 旧 ID 流用マップ追加 | `b7fcc39` |
| 追従漏れ：CommanderData.UltimateSkill 新型化 | `08a65d5` |
| 追従漏れ：FormulaParam を独立ファイルに切り出して復活 | `68aea6a` |
| 追従漏れ：VSProto_DebugBattlePresetsTests を `_old/` 退避 | `e74fbf9` |
| R-3-5b：旧 SO アセット 45 件一括削除（Units 19＋ Wazas 25＋ DraftPool 1） | `1157906` |
| R-3-13：新型 SO 整合スモークテスト 3 件（UnitCatalog 11/WazaCatalog 9/DraftPool 6） | `809b5df` |

【新設計の核心】

1. **WazaPattern enum + パラメタ方式**：`IActionEffect` インタフェース型は Unity SO シリアライズ不可
   （Domain 層が `noEngineReferences=true` 制約で `[SerializeReference]` が使えない）。これを回避するため、
   POCO 側では `WazaPattern` enum + 個別パラメタで効果を表現し、`WazaCatalog.Get` が switch で
   `Effects: IList<IActionEffect>` を組み立てる 2 段設計。新パターン追加は enum 1 行＋ switch case 1 つ。
2. **UniqueUnitIds 定数を Domain 層へ**：Roster は `Echolos.Data.Roster`（Data 層）配置のため、
   UseCase 層から直接参照すると依存方向違反。`Echolos.Domain.Models.UniqueUnitIds` を SSoT として新設し、
   Princess / Bridget 定数をここで定義。Roster は `UniqueUnitIds.Princess` を Id に使う形で参照。
3. **個別 CounterWaza 廃止**：プロト 17 体に個別 CounterWaza 持ちユニットは存在せず、火槍盾兵の反撃強化は
   `CounterDamageUp` Persistent StatusEffect で表現できる。`Unit.CounterWaza` プロパティと
   `CounterAttackResolver.ResolveCounterWaza` メソッドを削除。AttackEffect 内 ExecuteCounterAttack は
   固定 1.0 倍率の AttackEffect を直接 new。
4. **AuraEffect 廃止 → PersistentEffects 統一**：旧「置物オーラ」と「パッシブ Persistent」を統一し、
   `Unit.PersistentEffects: List<StatusEffect>` で表現。BattleAssembly や Bootstrap が戦闘開始時に Clone して
   RuntimeUnit へ複製付与する。戦闘開始時の `PrepareForBattle` で旧戦闘状態をクリアして再付与する純化方針。
5. **Phase3SoGenerator の汎用 POCO → SO 変換**：リフレクションで POCO のフィールド名と SerializedProperty 名を
   照合し、再帰的に SO へ書き出す。新パターンに対応する SO ジェネレータ拡張は不要（POCO が serialize 可能なら
   自動で取り込まれる）。

【動作確認】

ユーザー実機 Play で起動 → 戦闘実行 → 属性シナジー（OutgoingDamageUp 等）の StatusEffectApplied 発火を確認。
バランス所感は Phase R-6 マターで別途。

【Phase R-3 残作業】

なし。次セッション以降は Phase R-4（UI / 表示系再設計：シナジー発動バッジ・地形バッジ・配置 ATK 補正の
可視化等）or 試遊フィードバック対応に進む。

### 2026-06-15 セッション 第 13 段（Phase R-5-1 完了：戦闘システムリファクタリング完遂）

【方針確定】

Phase R-5 のスコープは当初「namespace 一括削除＋本流統合＋検証ツール新設」の 3 点だったが、
旧コードの `_old/` 物理移動は Phase R-3 で前倒し済のため、R-5 の残りは namespace 統合 1 点に絞られた。
検証ツール（シナジー / マッチアップ / 地形バイアス）は Phase R-6 着手時に必要になったら作る方針で
[[feedback_no_technical_debt]] と整合。よって R-5-1 完了 = 戦闘システムリファクタリング完了。

【コミット 1 件】

| 範囲 | コミット |
|---|---|
| R-5-1：Refactored namespace 一括除去＋ファイル位置統合＋テストファイル名追従＋コメント整理 | `2e21b29` |

【作業内容】

128 ファイル変更（うち 76 ファイルが mv リネーム・47 ファイルがテキスト書き換え）：

- **namespace 統合**：`Echolos.Domain.Battle.Refactored.*` → `Echolos.Domain.Battle.*`
  （`.Skills` / `.Replay` / `.Synergy` / `.Terrain` の 4 サブ namespace も同様）。
- **クラス名から Refactored prefix 除去**：`RefactoredWaza` / `RefactoredRuntimeWaza` /
  `RefactoredBattleManager` / `RefactoredActionExecutor` / `RefactoredActionDeclaration` /
  `RefactoredTargetEvaluator` / `RefactoredBattleAssembly` / `RefactoredBattleEventRecorder` /
  `RefactoredBattleRunner` を本来名に。
- **ファイル位置**：`Battle/Refactored/{Skills,Replay,Synergy,Terrain}/` を `Battle/{同}/` 直下に統合。
  `Battle/Refactored/` 直下の純関数群（DamageFormula / CounterAttackResolver / ShieldConsumer /
  PositionAtkCorrection / InternalSlotResolver / DamageModifier / StatusEffectProcessor /
  VictoryEvaluator / SpdOrderResolver）も `Battle/` 直下に。`Refactored/` ディレクトリと .meta を完全削除。
- **テストファイル名**：`Step3C2_RefactoredWazaTests.cs` → `Step3C2_WazaTests.cs`、
  `Step3C3a_RefactoredTargetEvaluatorTests.cs` → `Step3C3a_TargetEvaluatorTests.cs`。
- **コメント整理**（[900 §7.9] 経緯コメント禁止整合）：「（Refactored 版）」「（Refactored 用）」の
  付帯テキスト削除、ActionDeclaration の切り出し経緯コメント削除、Waza.cs の旧 Skills 差分コメント削除。

【手法】

`find Assets -name "*.cs" -not -path "*/_old/*" | xargs sed -i ...` で 9 種類のリネームを一括適用後、
`git mv` でファイル位置と名前を整理。`Refactored prefix > 通常 prefix` の順で sed 置換することで
「RefactoredRuntimeWaza → RuntimeWaza」が「RefactoredWaza → Waza」より先に置換されることを保証。

【戦闘システムリファクタリング 完了】

Phase R-0 〜 R-5-1 でリファクタリングは完遂。Phase R-4（UI 表示系再設計）/ Phase R-6（バランス調整＋
仕様書追従：390 / 310 §1.4 / 110 等）/ 試遊フィードバック対応 は別タスクとして残るが、コア戦闘
システムの再設計は本セッションをもって完了。

---

## 9. 想定 vs 実コードの差分（R-0 サマリ）

R-0 で「想定で書いていた」部分と「実コードで確認した」部分の主要な差分。911 初版（想定ベース）からの修正点を一覧する。

| 911 初版の想定 | 実コードの実態 | 修正方針 |
|---|---|---|
| StatusEffect の物理/魔法分離削除が必要 | **既に統合済**（DefenseUp/DefenseDown 1 種で物理・魔法共通） | 改修不要・Unit.PDEF/MDEF の統合のみ要 |
| BattleRunner.Run() シグネチャ維持の難しさ | UseCase は `BattleResolver` デリゲート経由・差し替え容易 | リスク大幅減・並走→差し替え方式が現実的 |
| TargetEvaluator は完全削除推奨 | DeclareAction の forced/support/self-guard 経路が複雑・support 判断は新戦闘でも使える | **完全削除でなく改修**（旧ターゲティングだけ差し替え） |
| 反撃の実装基盤を新規作成 | ReactionStack キューが既存・コメントに「トゲ反射」と記載 | 既存枠流用で実装可 |
| シナジー実装パターン A 推奨（StatusEffect 流用） | `ConditionalBuffProcessor` フレームワーク（Hook + 再帰ガード）が既存 | **派生クラス作成だけで実装可・想定より良好** |
| 引き分け非対称評価は新規実装 | `BattleContext.AllyKillCount` / `AllyDeathCount` / `IsAdvantageousVictoryCondition` が既存 | 判定式変更のみ |
| 旧 StatusEffect カテゴリ全廃 | Cover のみ廃止・他（Burn/Freeze/Paralysis/Curse/ReviveInvalid）は新戦闘でも維持可 | 削除範囲を限定 |
| ConditionalBuffProcessor 群は全削除 | 基底クラス + Hook 機構は新シナジーで流用、具体派生 4 種だけ `_old/` 退避 | **削除でなく流用＋部分退避** |
| 段階移行で「中間で動かない」期間ゼロ＝最小スタブ | BattleRunner.Run は 411 行・最小スタブで全機能再現は困難 | **並走方式（旧コード維持しつつ V2 並行実装）が現実的** |
| 「召喚ユニット」概念は議論なし | `BattleContext.IsSummoned` / 撃破数除外集計が既存 | Phase R-3 で使うか判断 |
| `Skill` クラス（パッシブ）の存在は未認識 | `Domain.Skills.Skill` クラス + TriggerType 7 種が既存 | 新戦闘でパッシブが必要なら流用可 |
| `AttackRange.Mid` の扱い未検討 | Mid（位置不問で敵前列攻撃）が既存・炎魔導士で使用 | 廃止 or 維持を Phase R-3 で判断 |
| 退避用 asmdef を新規作成 | `Echolos.Domain.OldPrototype` + `Echolos.Presentation.Old` + `Echolos.Tests.Domain.Old` の 3 つ既存・パターン確立済 | `Echolos.Domain.OldBattle` を同パターンで新設するだけ |
