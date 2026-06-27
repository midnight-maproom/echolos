% Echolos VSプロト devlog (2)

> **2026-06-15** 戦闘システム再設の完了後のセッション作業ログはここに書く。
>
> 位置付けは Claude 用作業ノート（900 番台＝ [900 §8.7](900_development_rules.md) で
> 「経緯ログ自由・仕様書一括修正対象外」と明文化）。仕様の SSoT は
> [320_vsprototype_combat_spec.md](320_vsprototype_combat_spec.md) 他の 100/300 番台。本書は実装側のメモ。
>
> このページの目的：(1) 戦闘リファクタ完了後の VSプロト残タスクを 1 ページで見渡せる起点ページにする。
> (2) 以降のセッション作業ログを時系列で積む。

---

## 1. VSプロト残タスク（2026-06-19 再定義）

戦闘エンジン＋ストーリー本文＋戦略マップ拠点アイコン投入は完了。
バランス調整も皇太子戦を除いて一旦完了。以下は「人に遊んでもらえる形」マイルストーンに
向けた残作業を、2026-06-19 のユーザー判断で再定義したもの。

### 1.1 ユニット系（見栄えと識別性）

| 項目 | 内容 |
|---|---|
| ~~味方ユニット画像~~ | **完了**（2026-06-20）。属性別 15 体の画像を unitId 別ファイル名で個別投入（Allies/Generic/）／旧 14 枚を Docs/Images/ユニットアイコン/_old/ に退避／IconRegistry._legacyIdMap の味方エントリ 15 件を死蔵防止で削除 |
| ~~味方ユニット名前~~ | **完了**（2026-06-19）。命名規則「属性 + の + 役職」（非レア）／「特殊冠詞 + 役職」（レア）に統一（[310 §1.13](310_vsprototype_spec.md)） |
| ~~敵ユニット画像／Name 共用化~~ | **完了**（2026-06-19）。帝国軍 11 体に縮約＋ Name 6 種類共用＋ Element.None 化（[310 §1.13](310_vsprototype_spec.md)） |
| ~~既存ユニットの Lv 上げ対象項目調整~~ | **完了**（2026-06-19）。WazaPowerBoost 機能化＋ PersistentEffectBoost 新設＋全 17 体の Preset 再割当＋固有スキル左 Preset 順序統一（[310 §1.12.5](310_vsprototype_spec.md)） |

### 1.2 固有ユニット系

| 項目 | 内容 |
|---|---|
| ~~皇太子戦の戦闘ユニット画像（闇 ver／通常 ver）~~ | **完了**（2026-06-20）。`imperial_prince.png`（通常 ver・A-c2 浄化後）／`imperial_prince_dark.png`（闇 ver・A-c1 必敗版）を `Bosses/` に投入。あわせて帝国軍 11 体も unitId 別画像投入完了＋ `IconRegistry._legacyIdMap` 全撤廃 |
| ~~皇太子 2 ver 切替機構~~ | **完了**（2026-06-20・案 C 採用）。闇皇太子を別 UnitDefinition（`imperial_prince_dark`）として登録＋ `EnemyPatterns.CreateBossPattern` で `HasNotedPendantPower` 分岐＝画像は IconRegistry の unitId 別取得で自動切替（仕組み変更不要）。仕様書 [310 §1.6.2](310_vsprototype_spec.md) / [330 §6.4](330_vsprototype_storyplot.md) で必敗化機構（HP/DEF 9999/999 ＋闇槍の薙ぎ自己 AttackUp 永続スタック）を明文化。新規機構 `ApplyStatusEffectToActorEffect` ＋ `WazaPattern.AttackWithSelfStatusRider` も同時導入 |
| 皇太子戦バランス調整 | §1.6 試遊版調整に統合（実機 FB 待ち） |
| ~~王女・ブリジットのスキル実装~~ | **完了**（2026-06-19）。王女「王家の加護」＋ブリジット「連携（王女）」「王家のペンダント」（[320 §4.9](320_vsprototype_combat_spec.md)） |

### 1.3 戦略マップ系（クローズ・2026-06-19）

| 項目 | 内容 |
|---|---|
| ~~攻撃予告＋配置済み視認~~ | **完了**。各マス下端中央に `味方-敵` 人数バッジ＋ BattleMode × 人数比で枠色決定（[310 §1.8](310_vsprototype_spec.md)） |
| ~~戦線管理の隣接性整合~~ | **完了**。自領陥落列をその先の敵領／敵拠点とも戦線外化＝直感的に正しい隣接性で統一（[310 §1.4.2](310_vsprototype_spec.md)） |
| ~~戦線外配置の自動回収~~ | **完了**。CanAssign 純粋化＋ StartRound 末尾の `DischargeUnreachableAssignments` で配置不可マスのユニットを手持ちに戻す（[310 §1.4.2](310_vsprototype_spec.md)） |

### 1.4 コンテスト提出に向けて

| 項目 | 内容 |
|---|---|
| 安定版ブランチ＋審査員向けビルド | プロト安定板完成時点で master からブランチを切り、審査員向けビルドを作成 |
| ~~スキップモード（10 分体験版）~~ | **試遊モード実装完了**（2026-06-19・[310 §1.14](310_vsprototype_spec.md)）。動画 2-3 分＋試遊 10 分の役割分担／セーブ 1（バッドエンド）→ セーブ 2（救出 → トゥルー）／本体に IDemoFlowController 抽象＋ NullImpl で疎結合。残：試遊シーン作成（ユーザー作業）＋ B-d/B-e/A-c2/True 演出のスチル投入＋実機試遊バランス調整 |
| ドキュメント整備 | コンテスト提出前に Docs 全体の通読確認＋整備。**110 本番版・400 本番版とも再作成確定**（メンテ不可・破棄して新戦闘エンジン／VSプロト現状に整合した形で書き直し・どちらも `_old/` にアーカイブ済）。順序的には最後 |
| ~~100 縮小後の参照追従~~ | **完了**（2026-06-22）。CLAUDE.md / 000 / 310 / 330 / 900 / 500 の追従済（500 は §3 マッピング表ごと VSプロトスコープに書き直し）。300 は同日に書き直して大幅省略（126 → 28 行）。400 は同日にアーカイブ退避（フル版設計再開時に内容ごと再構成予定） |

### 1.5 本実装メモ（軍師・傭兵オーラ拡張）

§1.2 王女オーラは [320 §4.9](320_vsprototype_combat_spec.md) として実装完了。`Domain.Battle.Aura.AuraApplier` の機構は SourceUnitId 紐付けで汎用化されており、将来「軍師」「傭兵」等の追加固有ユニットが導入された際は `AuraDefinitions.All` に AuraDefinition を 1 件足すだけで対応可能。

軍師「戦術指揮」／傭兵「孤高の戦士」は VSプロト 17 体に該当ユニットが存在しないため、本実装フェーズで該当ユニットを追加する際にオーラ定義もセットで定義する。

### 1.6 試遊版の調整（実機 FB 待ち・先回り想定タスク含む）

試遊モード機構は §1.4 で実装完了。試遊シーン（EcholosProto_Demo.unity）も作成済。
試遊版を実機で動かしてからのフィードバックに基づく微調整タスクを以下に整理。
Claude が先回り想定したものを含むため、実機で動かして不要と判定したものは線引きで OK。

#### 1.6.1 バランス調整（試遊で詰むと離脱リスク・優先度高）

| 項目 | 内容 |
|---|---|
| R1 中央列防衛戦の難度 | 試遊の入口。難しすぎる／簡単すぎるを実機で判定。シナリオ 1（Save1=メタ強化なし＋初期固定構成）でプレイ |
| R6 救出戦の難度 | 手駒 12 体・Lv2 ／メタ強化最大で勝てるか。Save2 の手駒構成（DemoSaveCatalog.BuildSave2Roster）を実機 FB で調整 |
| R7 皇太子戦の難度 | 試遊版では勝利確定気味のバランスが望ましい（詰むと離脱）。詰む場合：Save2/Retry の手駒を強化 or 皇太子取り巻き弱体化（BossRoster 側調整） |

#### 1.6.2 演出・テンポ調整

| 項目 | 内容 |
|---|---|
| 自動進行ラウンド（R2-R4）のテンポ | 「プレイヤーは必死に防衛を続けた…」目的バー文の表示時間。試遊者がダレない／速すぎないラインを実機で判定 |
| 目的バー文言の最適化 | プレイ中の視線が落ちる位置か。文言は適切か（[VSPrototypeDemoObjectiveGUI](../Assets/Scripts/Presentation/VSPrototype/VSPrototypeDemoObjectiveGUI.cs)） |
| セーブ間遷移動線 | セーブ 1 Defeat → タイトル戻り → セーブ 2 ボタン押下のフロー。試遊者が迷うようなら明示的モーダル化（「次は救出ルートを体験してください」誘導） |
| 皇太子戦リトライ動線 | R7 敗北後にタイトル戻り → セーブ Retry ボタン押下。試遊者が動線を見失うなら自動モーダル化（「もう一度挑戦しますか？」） |

#### 1.6.3 スチル投入（完了）

スチル制作・投入は完了（2026-06-20）。画像未設定ページが残っているのは演出上「黒画面で OK」と判断したシーンのみ（[asset-fallback-no-substitution](../) ルール準拠で代用画像を当てない方針）。

#### 1.6.4 シーン整合確認

| 項目 | 内容 |
|---|---|
| EcholosProto_Demo.unity 内容確認 | VSPrototypeTitleGUI._showDemoModeButtons=true ／ Bootstrap GameObject に VSPrototypeDemoObjectiveGUI 追加されているか実機 Play で確認 |
| 通常版シーン副作用なし確認 | EcholosProto_VS.unity を Build Settings に戻して通常版を起動 → 試遊ボタン非表示／既存挙動完全維持の確認 |

---

## 1.X 本実装フェーズ移行時メモ（プロト対応外・将来用手順）

VSプロト範囲では着手しないが、本実装に移行する際に必要となる作業手順のメモ。
プロト提出時点では英字フォールバックのまま運用する。

### 1.X.1 シナジー発動バッジ画像差替手順

戦闘画面アリーナ左上のシナジーバッジ（[Docs/310 §1.8.2](310_vsprototype_spec.md)）は画像 → 文字フォールバックで実装済。プロト提出は英字フォールバック（F2 / W4 / L0）のままで運用。本実装で画像投入する手順：

1. 画像を以下のパスに配置（PNG・推奨 64×64 以上）：
   - `Assets/Resources/Icons/Synergy/synergy_fire.png`
   - `Assets/Resources/Icons/Synergy/synergy_water.png`
   - `Assets/Resources/Icons/Synergy/synergy_light.png`
2. Unity Editor で `Texture Type = Default` ／ `Read/Write = false` ／ `Wrap Mode = Clamp`
3. プロジェクト起動時に [`VSPrototypeBattleGUI.LoadSynergyIcon`](../Assets/Scripts/Presentation/VSPrototype/VSPrototypeBattleGUI.cs) が自動的に拾い、英字フォールバックから画像表示へ切替

### 1.X.2 状態異常バッジ画像差替手順

各ユニット画像の左上（敵は右上）に重畳する状態異常バッジ（[Docs/310 §1.8.2](310_vsprototype_spec.md)）は画像 → 文字フォールバックで実装済。プロト提出は英字フォールバック（AU/DD/Br など）のままで運用。本実装で画像投入する手順：

1. 画像を以下のパスに配置（PNG・推奨 48×48 以上・正方形）：
   - `Assets/Resources/Icons/StatusEffects/status_attackup.png`（AU）
   - `Assets/Resources/Icons/StatusEffects/status_attackdown.png`（AD）
   - `Assets/Resources/Icons/StatusEffects/status_defenseup.png`（DU）
   - `Assets/Resources/Icons/StatusEffects/status_defensedown.png`（DD）
   - `Assets/Resources/Icons/StatusEffects/status_burn.png`（Br・燃焼）
   - `Assets/Resources/Icons/StatusEffects/status_freeze.png`（Fz・凍結）
   - `Assets/Resources/Icons/StatusEffects/status_paralysis.png`（Pa・麻痺）
   - `Assets/Resources/Icons/StatusEffects/status_searingwound.png`（SW・熱傷）
   - `Assets/Resources/Icons/StatusEffects/status_shield.png`（Sh）
   - `Assets/Resources/Icons/StatusEffects/status_healovertime.png`（HT・継続回復）
   - 他、`EffectKind` の全 enum 値に対応（ファイル名は `status_{enum_value_lowercase}.png`）
2. Unity Editor で `Texture Type = Default` ／ `Read/Write = false` ／ `Wrap Mode = Clamp`
3. プロジェクト起動時に [`VSPrototypeBattleGUI.LoadStatusIcon`](../Assets/Scripts/Presentation/VSPrototype/VSPrototypeBattleGUI.cs) が自動的に拾い、英字フォールバックから画像表示へ切替

### 1.X.3 対応外と確定した項目（2026-06-19）

ユーザー判断でプロト範囲外と確定。本実装でも要否は別途判断：

- 地形バッジ（マスごと地形強度可視化）
- 配置 ATK 補正の可視化（配置画面でユニット位置の補正値オーバーレイ）
- 敵編成ホバーで Lv 表示
- 検証ツール（シナジー段階／マッチアップ／地形バイアスの勝率測定）

---

## 2. セッションログ

### 2026-06-22（後半）310 仕様書の全面再構成（10 Phase × 10 コミット）

コンテスト提出に向けた 300 番台ドキュメント整理の第一弾として、310_vsprototype_spec.md を [900 §8.1-§8.4 / §8.6](900_development_rules.md) 準拠の「責務・抽象・判断のみ」体裁に全面再構成。

#### スコープ判定軸（事前洗い出し）

[900 §8.1](900_development_rules.md) 違反となる実装混在パターンを 8 カテゴリで洗い出し：

| カテゴリ | 内容 | 検出件数 |
|---|---|---|
| A | C# コードブロック（クラス定義／enum 列挙／プロパティ列挙） | 4 件（§1.3 / §1.12.5 / §1.12.9 / §3.3） |
| B | 実装ファイルへの `(../Assets/Scripts/...)` 参照リンク | 31 件超 |
| C | プロパティ・メソッド・enum 値のインライン引用 | 100 件超 |
| D | 処理フロー・アルゴリズム詳細（Pass1/1.5/2 switch 等） | 多数 |
| E | 経緯ログ・Phase/Step マーカー（[900 §8.3](900_development_rules.md) 違反） | 40 件超 |
| F | SO アセット詳細・Resources パス・ファイル名直書き | 多数 |
| G | 他仕様書と重複（[900 §8.4](900_development_rules.md) 二重ドリフト） | §1.6 数値 / §1.12.5 Upgrade 表 / §2.4 ステ等 |
| H | 「実装：」プレフィックスでの実装ファイル橋渡し | 19 件 |

#### 10 Phase 実施結果

| Phase | コミット | 主な作業 | 行差分 |
|---|---|---|---|
| 1 | `b028f33` | §0 用語整理 ＋ §1.3 データモデル（A1 コードブロック 51 行削除） | -41 |
| 2 | `8caf986` | §1.4 配置／§1.4.1 プール／§1.4.2 戦線概念（Pass1.5 詳細圧縮・API リンク全削除） | -31 |
| 3 | `b1c051a` | §1.5 自領陥落／§1.6 R7（Boss ステ・Waza 数値詳細を 320 参照へ）／§1.7 勝敗 | -33 |
| 4 | `4a3816c` | §1.8 表示仕様（OnGUI/Step マーカー一掃・カテゴリ B/C 整理） | +16 |
| 5 | `119d705` | §1.9 実装方針削除／§1.10 ビジュアル吸収／§1.11 ラン進行（API 表 2 個全廃） | -51 |
| 6 | `f86139e` | §1.12 内政フェーズ → §1.11 繰り上げ＋全面整理（最大の節・250 行）／§1.12.10 流用判定節削除 | -93 |
| 7 | `7b9db74` | §1.12 命名規則／§1.13 試遊モード | ±0 |
| 8 | `8910c44` | §2 ブリジット／§3 メタ通貨／§4 メタ拠点 UI | -39 |
| 9 | `2e7a318` | §5 ストーリー演出／§6 シーン遷移／§7 既存資産流用方針節削除／§8 → §7 テスト戦略 | -28 |
| 10 | `0d06d72` | 最終整合確認＋外部 Doc（020/300/500）の §x.y 参照を新番号に追従 | ±0 |

#### 最終結果

- **1228 行 → 928 行**（-300 行・24% 削減）
- C# コードブロック：0 件
- `(../Assets/Scripts/...)` クラスリンク：0 件
- Step / Phase / 2026-06 経緯マーカー：0 件
- VSPrototype* / MetaProgressState / UnitUpgradeDefinition 等の実装クラス言及：0 件
- 章節構造：§0 用語／§1 領地マップ（§1.1〜§1.13）／§2 ブリジット（§2.1〜§2.4）／§3 メタ通貨（§3.1〜§3.3）／§4 メタ拠点（§4.1〜§4.4）／§5 ストーリー／§6 シーン遷移／§7 テスト戦略 の連番きれい化

#### 外部 Doc 追従

- `Docs/020_video_script.md`：「310 §1.14」→「§1.13」
- `Docs/300_vsprototype_policy.md`：「310 §1.10」(2 箇所) →「§1.9」
- `Docs/500_architecture.md`：「310 §1.11」→「§1.10」
- `Docs/912 / Docs/_old`：[900 §8.7](900_development_rules.md) に従い触らない（経緯ログとしては当時の番号で残す方が git log 整合的）

#### 設計判断ログ

- **番号繰り上げの方針**：§1.9 実装方針節削除に伴い後続が連動して繰り上がる（§1.10 → §1.9、§1.11 → §1.10、§1.12 → §1.11、§1.13 → §1.12、§1.14 → §1.13）。番号変更と内容書き換えを Phase 6/7 で同時実施＝混乱期間を最小化
- **絶対残す要素**：仕様要素そのもの（BattleMode 表・取り戻され表・配置継続性表・配置可否表・バッジ枠色表・カテゴリ色枠表・配置動線表等）は責務記述として価値があるため維持しつつ実装詳細のみ除去
- **コード SSoT への委譲**：兵種ラインナップ詳細／Upgrade ID 一覧／Preset 割り当て／Boss ステ数値／Waza 数値はすべて 320＋実装側 Roster SSoT に集約
- **910 番台は触らない**：[900 §8.7](900_development_rules.md) の「910 番台は仕様書一括修正対象外」原則を 912 セッションログにも適用＝古番号のままで git log としては当時の状況を保存

#### 次セッション TODO

300 番台ドキュメント整理の続き：

- **300_vsprototype_policy.md**（VSプロト方針書）：実装混在・経緯マーカーの洗い出し＋整理
- **320_vsprototype_combat_spec.md**（戦闘システム仕様・再設計版）：310 で SSoT として参照を集約済＝最も多くの実装詳細が乗っている可能性高い
- **330_vsprototype_storyplot.md**（ストーリープロット）：310 から SSoT として多数参照済＝こちらも整理候補

進め方は 310 と同じ：洗い出し → Phase 計画 → 段階コミット。

---

### 2026-06-22 右パネル UI ポリッシュ＋バルドゥイン拠点呼称廃止＋確認ダイアログ

試遊版仕上げの UI 改善＋表示文言整理を一気に通したセッション。

#### 戦略マップ右パネル拡張（`ac0fded` → `4060452` → `fbe2d06`）

- 右 25% パネルの余剰スペースを 2 セクション追加：
  - **戦線状況**：戦線が立っているマス（`BattleMode != None`）を本拠地→自領→敵領→敵拠点の順に列挙。`[!]/[△]/[○]` 記号＋マップバッジ枠色（`ResolveBadgeFrameColor` 流用）でマップ色バッジを文字でも再表現＝配置漏れ気付き
  - **属性シナジー Tips**：火／水／光の特性を 1 行ずつ説明。`UnitDisplayLabels.ElementColor` で色付け。文言は数値抽象（「単体 ATK 強化」等）→ 具体記述（「最も ATK の高いユニット 1 体（火 4↑で 2 体）への割合増」「水 4-6 でシールド 1-3 回」「ターン終了時の全体継続回復」）に書き換え＋ `wordWrap=true` で自動改行。
- 色制御は `_dynamicLabelStyle`（mutable）の `textColor` / `wordWrap` を都度書き換え＝スタイル数を増やさない方針。

#### 属性表記の色付け＋王国軍リスト日本語化＋ホバー説明（`4ebb52c`）

- `UnitDisplayLabels.ElementColor(Element)` を新規追加し属性色を一元管理（9 属性分・SSoT 化）。Tips の色定数もこれを参照に切替（DRY）。
- 左パネル王国軍リスト・配置モーダル内ユニット一覧：`unit.AttackKind` enum 値そのまま（"Melee"）→ `AttackKindLabel`（近接/遠隔/補助）に日本語化＋属性 1 文字を色付きで併記。
- 王国軍リスト行ホバーで `DrawRosterUnitTooltip` を新規描画（既存 `_hoveredNode` パターン踏襲）。属性＋攻撃種別＋役割タグ＋フルステ（HP/ATK/DEF/SPD）＋ Description を表示し、ドラフト時の説明文を戦略マップから何度でも参照できる。
- 召集ドラフトカード（InteriorGUI `DrawCard`）のメタ行も属性 1 文字のみ色付きラベル分割。
- 仕様書 [310 §1.8](310_vsprototype_spec.md) 追従。

#### バルドゥイン拠点呼称廃止（プレイヤー画面のみ・`2e940ef`）

- 左列敵拠点の特別性は専用アイコン（`node_balduin`）と B-d 救援演出側で十分伝わるため、マスラベル文言を他列と同じ「敵拠点 左/中/右」「敵拠点 左/中/右（制圧）」に統一。
- 変更箇所：MapGUI `BuildRosterGroups` 見出し／`NodeLocationLabel`／`NodeLabel`（列情報追加）／`DrawFrontlineStatus`／Bootstrap `BuildNodeBattleTitle`。
- 維持（意図的）：
  - 内部識別子（`IsBalduinStronghold` / `BalduinCol` / `HasRescuedBalduin` / `MarkBridgetRescued` 等）
  - XML doc / コード内コメント（プログラマ向け）
  - 物語ナレーション本文（人物名「バルドゥイン」表現）
  - 仕様書 330 物語上の地名表現（B-d 救援＝バルドゥイン拠点解放など物語の核となる固有名）
  - 仕様書 912（910 番台＝触らない原則・[900 §8.7](900_development_rules.md)）
- 仕様書 310 §1.2 / §1.6 / §1.8 のプレイヤー視点表記を追従修正。

#### 確認ダイアログ追加（`6005d4e`）

- 汎用 `ConfirmDialog` 機構（Title / Message / ConfirmLabel / OnConfirm）を MapGUI に追加し、誤クリックでの本ラン放棄や行動力残し終了を防ぐ。
- **内政終了**：行動力 > 0 のとき確認ダイアログ。0 なら即時遷移。
- **ラン放棄**：常時確認ダイアログ。
- OnGUI モーダル 3 大落とし穴対応（[[ongui-modal-implementation]] 既存パターン踏襲）：
  - ホット争奪：マスボタンを `_confirmDialog` 表示中も非描画（既存 PlacementModal と AND）
  - MouseDown+Use：モーダル外 MouseDown はキャンセル扱い＋ `Event.Use` で背面 Button へ伝播抑制
  - 状態消去：コールバック呼び出し前に `_confirmDialog=null` セット
- OnGUI 末尾の描画優先度は ConfirmDialog > PlacementModal > NodeTooltip > RosterUnitTooltip の排他。
- 仕様書追記なし（粒度的に 310 のボタン名一覧より詳細寄り）。

#### 細かい文言修正（`04bcd3c`）

- 「ラン放棄→メタ拠点」→「もとの世界へ戻る（ラン放棄）」（パラレルワールド設定整合）
- 「VSプロト 内政一体化版」→「Vertical Sliceプロト」（外部配布時の名称統一）
- 「召集（行動力 1）」→「召集（行動力 1／ラウンド中 1 回のみ）」（使用制約明示）
- 「ユニット強化（行動力 1／複数回可）」→「ユニット強化（行動力 1）」（説明過剰削減）

#### 設計判断ログ

- **バルドゥイン拠点呼称廃止スコープ**：プレイヤー画面のみ。物語固有名と内部識別子は維持。「絵で伝わるものはテキストを抽象化する」方針＝拠点画像で特別性が伝わるため文言は他列と統一可。物語側で残すのは「バルドゥインという人物の籠城地」という意味的紐付けが必要なため
- **属性色 SSoT**：MapGUI 内に直書きしていた火/水/光の RGB を `UnitDisplayLabels.ElementColor` に集約。Tips・王国軍リスト・配置モーダル・ホバーツールチップ・ドラフトカードすべて同じ色定義を参照＝今後属性追加や色調整時の二重更新を構造防止
- **`_dynamicLabelStyle` 共用パターン**：戦線状況／シナジー Tips／確認ダイアログ／ホバーツールチップが 1 つの mutable GUIStyle を `textColor` / `wordWrap` / `fontSize` / `fontStyle` 都度書き換えで共用。GUIStyle インスタンスが増えると BuildStylesIfNeeded の肥大＋ GC 圧迫の懸念があり、副作用ありの書き換えを許容する代わりに「使用直後に元に戻す」ルールで運用（DrawSynergyTips 末尾で `wordWrap=false` リセット等）

---

### 2026-06-21 AttackKind.None 追加＋ドラフトカード表記日本語化

ユーザー指摘「攻撃しないバフ役・回復役が便宜上 Melee 表記になっているのが不自然」を受け、AttackKind enum に None 値を追加して型として明示化。あわせてドラフトカード表記を漢字・日本語に統一。

#### enum 拡張
- `AttackKind.None` 追加：攻撃 Waza を一切持たないユニット用（補助・回復専門）
- 反撃の授受なし／配置 ATK 補正対象外
- 旧設計判断（320 §3.3 行 302「タンク用に None 不要」）はタンク文脈の話で、今回は補助・回復用途のため別目的＝矛盾せず

#### None 化対象（4 体・帝国コピー含めて 7 体）
- 炎の鼓舞師（fire_buffer ＋ imperial_fire_buffer）
- 水の護術師（water_buffer ＋ imperial_water_buffer）
- 癒水の巫女（water_healer）※imperial 版なし
- 光の司祭（light_priest ＋ imperial_light_priest）

#### Melee のまま（反撃発動者）
- タンク全般（炎の大盾兵／水の大盾兵／水鏡の幻盾兵）
- 攻撃 Waza 持ち全員

#### 実装
- `Enums.cs`：AttackKind.None 追加（Melee/Ranged の後ろに）
- `PositionAtkCorrection.cs`：None ケースを明示分岐（呼ばれない想定だが防御的に 1.0 倍）
- `CounterAttackResolver` / `AttackEffect`：既存ロジック（`!= Melee` / `!= Ranged` 判定）で None は自動的に弾かれる＝変更不要
- `AlliesRoster`：4 体を AttackKind.None に変更
- `EnemiesRoster`：AsImperial で AttackKind を継承＝自動連動

#### UI 表記改善（ドラフトカード）
- `UnitDisplayLabels.AttackKindLabel`：None ケース追加（"補助"）
- `UnitDisplayLabels.ElementLabel`：新設（Fire→火／Water→水／Light→光 等の漢字 1 文字）
- `VSPrototypeInteriorGUI.DrawCard`：`unit.AttackKind / unit.UnitElement` の直接 ToString を両ヘルパ経由に置換

#### 仕様書追従
- 320 §3.3 行 302：旧設計判断（タンク用に None 不要）に補助・回復用途で None を導入する旨を追記
- 320 §4.8 ユニット表：「（Waza 不所持・防御フォールバック）」を「Melee / 防御フォールバック」に統一、「バフ系 Waza」「回復系 Waza」を「None / バフ Waza」「None / 回復 Waza」に変更

### 2026-06-20 ドラフトカードにユニット説明欄追加

試遊・実機プレイで「ステータス数値だけだとどんなユニットか分からない」
というユーザーフィードバック。Description フィールド追加で対応。

#### 実装
- `UnitDefinition.Description`（string）追加＋ `[TextArea(2, 6)]` で Inspector 編集可
- `Unit.Description` プロパティ追加（Domain 層）
- `UnitCatalog.BuildUnit` で Description 転写
- `EnemiesRoster.AsImperial` で帝国コピー時に Description も継承
- `AlliesRoster` 17 体に暫定 1-2 行 Description（Claude 作成・ユーザー後修正可）
- `VSPrototypeInteriorGUI.DrawCard`：カード高さ 380 → 460／ステータス下に
  説明エリア（5 行・自動改行・y=308 から 100px）／_cardDescStyle 新設

#### 編集経路
- 推奨：`AlliesRoster.cs` を直接編集 → `Echolos/Data/SO アセットを生成` で SO 反映
  （git 履歴に残る／SSoT 維持しやすい）
- もしくは Unity Editor で SO アセットの Description を直接編集
  （Inspector で TextArea 編集可）

### 2026-06-20 動画撮影用セーブ＋シナリオ＋タイトル UI 録画ボタン追加

コンテスト審査員向け 3 分動画の撮影準備。動画台本は別途
[020_video_script.md](020_video_script.md) に集約。

#### 撮影機構の選択（ユーザー確定）
- Q1：撮影用セーブ複数（B 案）／編集ソフト前提
- Q2：R5 開始時 B-b2 は変更せず仕様維持
- Q3：字幕は動画編集ソフトで追加（ゲーム内字幕レイヤー実装なし）
- Q4：戦闘 4 倍速は既存・追加実装なし（編集側でさらに 2 倍速化）

#### 追加した撮影用セーブ（4 個）

| ID | 開始 | 用途 |
|---|---|---|
| `Rec_R5_BB2` | R5 開始・救援未済・メタ強化なし | Cut 5 B-b2 撮影 |
| `Rec_R7_AC1` | R7 開始・救援未済・HasNotedPendantPower=false | Cut 6 A-c1 必敗 → Defeat → A-b1 |
| `Rec_R6_Rescue` | R6 開始・メタ強化最大・左列敵領制圧済 | Cut 8 救出戦 → B-d |
| `Rec_R7_True` | R7 開始・救援済・ブリジット加入・HasNotedPendantPower=false | Cut 9 B-e → A-c2 → 皇太子戦勝利 → A-d |

#### 追加した撮影用シナリオ（4 個・RoundRules 空＝通常進行）

`Rec_*_scenario` 4 件を `DemoScenarioCatalog` に追加。撮影者が
任意の操作で進めて撮影。`Bootstrap.StartDemoMode(scenarioId)` 経路を流用。

#### タイトル UI 追加

試遊シーン（`_showDemoModeButtons=true`）の「試遊：救出戦を体験」
の下に録画ボタン群 4 個を緑色背景で並べる（試遊：茶／録画：緑で
視覚的に区別）。通常版シーン（`_showDemoModeButtons=false`）には
表示されない＝本番ビルドに含まれない設計。

#### 実装
- `DemoSaveCatalog`：Rec 系セーブ 4 個＋ ID 定数 4 個＋ Get の case 4 個
- `DemoScenarioCatalog`：Rec 系シナリオ 4 個＋ ID 定数 4 個＋ Get の case 4 個
- `VSPrototypeTitleGUI.DrawDemoModeButtons`：録画ボタン 4 個追加＋色分け
- `DemoFlowControllerTests`：Rec 系ロード可能性＋ ObjectiveText null テスト 2 件追加
- [020_video_script.md](020_video_script.md) 新規（カット詳細＋撮影用セーブ仕様＋編集ガイド）
- [000_index.md](000_index.md)：コンテスト用セクションに 020 追加
- [310 §1.14.1](310_vsprototype_spec.md)：録画モード同居の言及追加

### 2026-06-20 ForceDefeat 機構の死蔵削除

試遊版を R4 救出戦のみに縮約した結果、R1-R5 強制 Defeat 機構が
完全に呼ばれなくなった（§7.10 死蔵防止対象）ため一括撤去。

#### 削除した型・メソッド

- `DemoForcedOutcome` enum（None / ForceDefeat / ForceTrue）全体
- `IDemoFlowController.GetForcedOutcome(int)`
- `DemoFlowController.GetForcedOutcome` 実装
- `NullDemoFlowController.GetForcedOutcome` 実装
- `DemoRoundRule.ForcedOutcome` プロパティ＋コンストラクタ引数
- `VSPrototypeBootstrap.TriggerDemoForcedDefeat()` ／ `ReturnToTitleAfterDemoEnding()`
- `ContinueRoundInteriorPhase` 冒頭の `if (forcedOutcome == ForceDefeat)` ブロック
- `DemoFlowControllerTests` の `ForceDefeat` 関連 assert（NullImpl／DemoImpl の 2 件）
- [310 §1.14.2](310_vsprototype_spec.md) 介入機構表の「強制終端」行
- `StartDemoMode` コメントの「ForceDefeat」言及

`ForceTrue` enum 値は新設時から一度も呼ばれていなかった（§7.10
の代表事例）。`ForceDefeat` も今回の縮約で同パスを辿った。

### 2026-06-20 B-c 発火条件厳密化＋試遊版を R4 開始に変更

直前セッションで試遊版を R6 救出戦のみに縮約したが、ユーザー指摘で
発覚した本仕様の整合性問題を併せて根治：

#### 問題
- B-c「謎の少女が踵を返す」は本来「救援打ち切り＝ブリジット永続離脱」演出
- だが発火条件が `!IsBridgetRescued && !HasRescuedBalduin` だけで R6 救出戦可能なまま
- → R6 で B-c が出た後、左列敵拠点を制圧すると B-d でブリジット加入する破綻
- 試遊版 R6 開始セーブだと R5 B-b2 経由しないまま R6 B-c が発火＝救援するつもりが踵を返される逆転現象

#### 修正
- ラン中フラグ `IsBalduinSurrendered` を新設（VSPrototypeMapState）
- R5 B-b2 演出完了時に `MarkBalduinSurrendered` を呼ぶ
  - フラグ立て＋左列敵拠点を通常敵拠点化（`MapNode.MarkBalduinStrongholdCleared`）
  - 以降制圧してもブリジット加入トリガしない
- R6 B-c 発火条件に `IsBalduinSurrendered` を追加（救援打ち切り後のみ）
- 試遊版セーブ 2 を R4 開始に変更
  - R4 開始時はラウンド開始演出なし＝ R5 B-b2 / R6 B-c とも干渉しない
  - ストーリー的に「救援が間に合うラウンド」として正規ルートの中間で綺麗

#### 実装
- `MapNode.IsBalduinStronghold` を `{get; private set;}` 化＋ `MarkBalduinStrongholdCleared` 追加（不変原則の例外として明示）
- `VSPrototypeMapState.IsBalduinSurrendered` プロパティ＋ `MarkBalduinSurrendered` メソッド追加
- `VSPrototypeRoundStartEventResolver` R6 B-c case に `IsBalduinSurrendered` ガード追加
- `VSPrototypeBootstrap.TryBeginRoundStartEvent` に B-b2 分岐＋ `OnBalduinSurrenderCompleted` 追加
- `DemoSaveCatalog.Save2`：startRound 6 → 4
- `DemoScenarioCatalog.Scenario2`：R6 ルール → R4 ルール
- `RoundStartEventResolverTests`：R6 B-c テスト 2 件追加（既発火時／未発火時）
- `DemoFlowControllerTests`：R6 → R4 ObjectiveText テスト追従
- [330 §3.3 / §3.4 / §4.2 / §5](330_vsprototype_storyplot.md) 追従
- [310 §1.14](310_vsprototype_spec.md) 追従

### 2026-06-20 試遊版を救出戦のみに縮約（動画 2-3 分との役割分担）

動画（コンテスト審査員向け 2-3 分・外部成果物）でフルストーリー＝ USP の感情曲線を見せる前提に方針転換。試遊版は USP の核「救出 → ブリジット加入」体験のみに完全特化（実機 5 分以内で完走）。

#### 動画構成（ユーザー策定）

プロローグ → R1 ゲームプレイ → R2 バルドゥイン登場（B-a）→ 救援間に合わず → R6 バルドゥイン降伏（B-b2）→ バッドエンド → 周回画面＋メタ強化説明 → **R6 救出戦＋ブリジット加入（← 試遊版で切り取り）** → 皇太子戦チラ見せ → トゥルーエンドチラ見せ → 終わり（B-e 聖剣強化は省略）

#### 削除（試遊版から）

- セーブ 1（バッドエンド体験）／シナリオ 1：R1-R5 自動進行＋ B-b2 → Defeat
- セーブ Retry（皇太子戦リトライ）／シナリオ Retry
- シナリオ 2 から R7 連鎖（B-e / A-c2 / 皇太子戦）部分

#### 残した部分

- セーブ 2：R6 開始・メタ強化最大・手駒 12 体
- シナリオ 2：R6 救出戦のみ手動 → B-d ブリジット加入演出 → タイトル戻り
- タイトル UI：「試遊：救出戦を体験」1 ボタン

#### 実装

- `DemoSaveCatalog.cs`：Save1 / SaveRetry 削除（Save2 のみ残）
- `DemoScenarioCatalog.cs`：Scenario1 / ScenarioRetry 削除＋ Scenario2 から R7 ルール削除
- `VSPrototypeTitleGUI.DrawDemoModeButtons()`：3 ボタン → 1 ボタン化
- `VSPrototypeBootstrap.AdvanceToNextRoundAfterBalduinRescueEvent()`：`_demo.IsActive` 時に `CurrentPhase = Title` 直接遷移を追加
- `DemoFlowControllerTests`：Scenario1 / Retry テスト削除＋ Scenario2 R6 ベースに差し替え
- [310 §1.14](310_vsprototype_spec.md) 改定（シナリオ表 1 行化／§1.14.3 R6 → R7 連鎖節を「R6 のみ」に縮約）

#### 死蔵化した機構（次セッションで削除検討）

R1-R5 強制 Defeat 機構が呼ばれなくなったため死蔵：

- `DemoForcedOutcome.ForceDefeat` enum 値
- `VSPrototypeBootstrap.TriggerDemoForcedDefeat()` ／ `ReturnToTitleAfterDemoEnding()`
- `IDemoFlowController.GetForcedOutcome()` ／ 実装 2 種（DemoFlowController / NullDemoFlowController）
- `DemoRoundRule.ForcedOutcome` フィールド
- Bootstrap の `if (forcedOutcome == DemoForcedOutcome.ForceDefeat)` ブロック

§7.10 死蔵防止対象。本コミットでは「動く形」を優先して残置し、削除は次セッションでまとめる（呼び出し元 grep → 該当箇所一括撤去→テスト追従の流れ）。

### 2026-06-20 闇皇太子 調整：通常編成＋皇太子のみ闇置換／バフ量 +20→+15

実機 Play での見え方フィードバックを受けた調整：

- **編成変更**：単騎 → 通常版と同じ取り巻き 5 体＋ Slot 1 の皇太子だけ闇皇太子に置換
  - 理由：見栄え向上（単騎は寂しい）
  - 取り巻きを倒しても闇皇太子の AttackUp 累積で詰む構造は維持
- **AttackUp Magnitude**：20 → 15 に下げて決着まで 1 ターン延長（T3 全滅 → T4 全滅）
  - シミュレーション：T1 ATK 55 → T2 ATK 70 → T3 ATK 85 → T4 ATK 100（全員 82-97 ダメ）
  - 全滅演出にもう少し溜めを作る

実装：
- `VSPrototypeBossLineups.R7FinalBossDark()` 編成変更（通常編成＋皇太子置換）
- `WazaRoster.DarkSweep()` Magnitude 20 → 15
- `EnemyPatternsTests` 必敗版 2 件を 6 体編成に追従
- [310 §1.6.2](310_vsprototype_spec.md) ／ [330 §6.4](330_vsprototype_storyplot.md) 表とシミュレーション更新

ユーザー作業：
- Editor で `Echolos/Data/SO アセットを生成` 実行（`waza_dark_sweep` SO の Magnitude=15 反映）

### 2026-06-20 IconRegistry 修正：imperial_ 系ラスボス画像探索フォールバック

ユーザー実機報告「皇太子の画像が出ない」の調査結果、`EnumerateCampSubPaths` が
`imperial_` プレフィックスに対し `Enemies/` だけ探索＝ `Bosses/` に置いた皇太子画像が
ヒットしない潜在バグが発覚（通常皇太子も同様に従来から表示されていなかった可能性）。

修正：`imperial_` 系は `Enemies/` → `Bosses/` の順にフォールバック探索（コミット `34388b9`）。
帝国軍 11 体は Enemies/ で即ヒット／皇太子 2 体（imperial_prince / imperial_prince_dark）は Bosses/ でヒット。
一度試行した結果は `_missing` キャッシュで 2 回目以降スキップ＝実行コスト無視可能。

---

### 2026-06-20 闇皇太子（A-c1 必敗形態）実装

ユーザー指摘「必敗版の戦闘ってそもそも実装されてますか」を受けた調査で、`VSPrototypeEnemyPatterns.CreateBossPattern(bool)` が引数を受け取るだけで未使用＝必敗版が完全未実装と判明（§7.10 死蔵パターン）。仕様書 [330 §6.4](330_vsprototype_storyplot.md) には「実質無敵化」と書かれていたが実態は通常編成。

#### 設計判断

- **案 C 採用**：闇皇太子を別 UnitDefinition（`imperial_prince_dark`）として登録＋編成切替で対応
  - 副次効果：IconRegistry の unitId 別画像取得が自動的に効く＝ 912 §1.2「皇太子 2 ver 切替機構」タスクも同時クローズ
- **編成は単騎固定**：取り巻きを置くと「皇太子を倒せなくても他敵を倒して辛勝」できる懸念（ユーザー指示）
- **HP/DEF 9999/999 で実質無敵＋ ATK 超絶バフで必殺**：ユーザー方針「防御面で必敗・攻撃面は通常準拠＋ ATK 超絶バフ」
- **「闇槍の薙ぎ」1 Waza に攻撃と自己バフを統合**：旧実装の `PrinceDarkAuraConditionalProcessor` のような専用 Processor を立てず、既存 Effects チェイン機構に乗せる
- **新規機構 `ApplyStatusEffectToActorEffect`**：`ApplyStatusEffectEffect`（Targets 対象）の caster 自己版。既存 `def_guard`（TargetingType=Self）と棲み分け：他ターゲット Waza に「ついでに自己バフ」を混ぜる用途
- **新規 `WazaPattern.AttackWithSelfStatusRider`**：データ駆動で SO 表現可能。既存 `RiderEffect` フィールドを流用＝ SO 構造改変なし

#### シミュレーション（味方 HP 110 / DEF 3〜18）

| ターン | 闇皇太子 ATK | 闇槍の薙ぎダメージ | 味方の状態 |
|---|---|---|---|
| T1 | 55 | 全員 37-52 ダメ | 半数以上が瀕死 |
| T2 | 75 | 全員 57-72 ダメ | タンク以外死亡 |
| T3 | 95 | 全員 77-92 ダメ | 全滅 |

#### 実装したファイル（3 コミット）

| Phase | コミット | 内容 |
|---|---|---|
| サブ A | `5962138` | `ApplyStatusEffectToActorEffect` 新規追加＋ NUnit 6 ケース |
| サブ B | `2bad37d` | 闇皇太子データ＋ EnemyPatterns 分岐＋テスト追従（29→30 / 16→17）＋画像 .meta 取り込み |
| サブ C | （本コミット） | 仕様書追記（310 §1.6.2 闇皇太子編成 / 330 §6.4 必敗化仕様 / 912 §1.2 完了マーク） |

#### ユーザー作業

- Editor で `Echolos/Data/SO アセットを生成` 実行（`imperial_prince_dark` UnitDefinition SO ＋ `waza_dark_sweep` WazaDefinition SO 生成）
- Test Runner で All Tests 実行
- 実機 Play で A-c1 経路（救援失敗ルート）の R7 戦闘確認：
  - 闇皇太子単騎が出現するか
  - HP/DEF が削れないか（実質無敵）
  - T1/T2/T3 で全体物理＋ ATK バフが累積するか
  - T3 までに全滅するか

---

### 2026-06-19 試遊モード（コンテスト審査員向け 10 分体験版）実装

ユーザー方針確定：動画 2-3 分で USP 感情曲線を伝え、試遊 10 分で実装証明を完走させる役割分担。完全レール進行＋自動勝利＋強制 Defeat で 10 分内に「バッドエンド → セーブ 2 → 救出 → トゥルー」を体験させる。

#### 設計判断

- **本体ロジックから疎結合**：`IDemoFlowController` 抽象（UseCase/Demo）に問い合わせメソッド集約。通常モードは `NullDemoFlowController` の no-op 注入＝本体挙動完全維持
- **コードハードコード**：DemoSaveDefinition / DemoScenarioDefinition は POCO で `*Catalog` から提供。SO 化なし（プロト範囲でデータ駆動不要・本実装持ち越し対象外）
- **シナリオ = ラウンド別ルール辞書**：`DemoRoundRule`（内政スキップ／自動勝利／手動戦闘マス指定／ForceDefeat／目的バーテキスト）を `RoundRules[round]` で問い合わせる宣言的構造
- **介入は質問のみ**：Bootstrap が `_demo.ShouldSkipInteriorPhase(round)` 等を問い合わせて分岐＝介入ロジックを 1 箇所に集中
- **シーン分離**：`VSPrototypeTitleGUI._showDemoModeButtons` SerializeField で試遊シーン側だけ ON。`VSPrototypeDemoObjectiveGUI` は試遊シーン専用
- **仕様改善 B-e 発火条件改定（[330 §3.6](330_vsprototype_storyplot.md)）**：R6 中に救出した場合でも R7 開始時 B-e → A-c2 連鎖で皇太子撃破経路に到達可能（通常版にも適用＝整合性向上）

#### Phase 構成（10 コミット）

| Phase | コミット | 内容 |
|---|---|---|
| 0 | `608f06e` | IDemoFlowController + NullImpl 骨組み |
| 1 | `f114af1` | DemoSaveDefinition POCO + DemoSaveCatalog |
| 2 | `29bb2ac` | DemoScenarioDefinition + DemoFlowController 実装＋テスト 7 ケース |
| 3a | `7709915` | B-e 発火条件 R6 → R6/R7 拡張（通常版仕様改善）＋テスト 2 ケース |
| 3b | `ec41acb` | Bootstrap.StartNewRunFromDemoSave 追加（スナップショット復元経路） |
| 3c | `0b3f7f1` | セーブ 1 シナリオ詳細化＋ Bootstrap 介入点 3 つ設置＋開発エントリ |
| 4 | `44f6d49` | セーブ 2/Retry シナリオ詳細化＋ True エンディング試遊分岐 |
| 5a | `b4cedf8` | タイトル画面試遊モードボタン群＋ StartDemoMode 正式 API 化 |
| 5b | `995ba80` | 目的バー UI（VSPrototypeDemoObjectiveGUI）＋ ObjectiveText 全シナリオ埋め込み＋テスト 3 ケース |
| 6 | （本コミット） | Editor ビルドメニュー（DemoSceneBuildSetup）＋ 310 §1.14 仕様書追加＋ 912 §1.4 クローズ |

#### 実装したファイル

| 区分 | ファイル |
|---|---|
| 新規 UseCase/Demo | `IDemoFlowController` / `NullDemoFlowController` / `DemoFlowController` / `DemoSaveDefinition` / `DemoSaveCatalog` / `DemoScenarioDefinition` / `DemoScenarioCatalog` |
| 新規 Presentation | `VSPrototypeDemoObjectiveGUI` |
| 新規 Editor | `DemoSceneBuildSetup`（`Echolos/Build/通常版・試遊版 Scenes に設定`） |
| 既存修正 | `VSPrototypeBootstrap`（`_demo` 結線＋ `StartNewRunFromDemoSave` ＋ `StartDemoMode` ＋ ContinueRoundInteriorPhase 試遊分岐＋ `FinishRunAfterEndingEvent` 分岐＋ `OnSwordEmpoweredCompleted` 再判定）／`VSPrototypeRoundStartEventResolver`（R7 ケースに B-e 連鎖追加）／`VSPrototypeTitleGUI`（試遊モードボタン群） |
| 新規テスト | `DemoFlowControllerTests`（10 ケース）／`RoundStartEventResolverTests` に R7 B-e ケース 2 件追加 |
| 仕様書 | [310 §1.14](310_vsprototype_spec.md) デモモード仕様新設／[330 §3.6 / §5](330_vsprototype_storyplot.md) B-e 発火条件改定 |

#### ユーザー作業（試遊版完成までの残）

1. **EcholosProto_Demo.unity の作成**（Unity Editor 操作）：
   - `EcholosProto_VS.unity` をコピーして `EcholosProto_Demo.unity` にリネーム
   - Bootstrap GameObject の `VSPrototypeTitleGUI._showDemoModeButtons` を **true** に
   - Bootstrap GameObject に `VSPrototypeDemoObjectiveGUI` を AddComponent
2. **試遊版ビルド**：`Echolos/Build/試遊版 Scenes に設定` → File > Build Settings... → Build
3. **実機試遊バランス調整**：
   - R1 中央列防衛戦の難度（手動戦闘の入り口・編成ジレンマ体感）
   - R6 救出戦の難度（手駒 12 体・Lv2 で勝てるか）
   - R7 皇太子戦の難度（試遊で詰むと離脱リスク）
4. **スチル投入優先順**：B-d 救援成功／B-e 聖剣強化／A-c2 浄化／True エンディング（試遊で必ず通る経路）

---

### 2026-06-19 帝国軍 Name 共用化＋ Element.None 化＋未使用 4 体削除

味方ユニット 15 体の Name 一新に合わせて、敵側帝国軍 15 体も整理。通常プールに使う 10 体は **Name 6 種類に集約**（同名異 ID で行動だけ違って見える設計）、通常プール非使用のレア相当 4 体は **削除**、皇太子戦専用の魔導士 1 体は別 Name 「帝国大魔導士」で残置。

#### 設計判断

- **Name 共用化（10 体 → 6 種類）**：プレイヤーから見ると「同じ見た目のユニットが行動だけ違って見える」。例：帝国大盾兵 A は反撃強化／帝国大盾兵 B は専守＝被ダメ -30%。能力差は PersistentEffect 由来で残る
- **Element.None 化**：VSプロト範囲では地形補正は全列 Neutral 固定でシナジー発動も味方陣営限定＝敵に属性を持つ意味がない
- **レア相当 4 体削除**：imperial_fire_assassin / imperial_water_dispel_tank / imperial_water_healer / imperial_light_mage は通常プール非使用＋皇太子戦未使用＝[900 §7.10 死蔵防止](900_development_rules.md) で削除
- **皇太子戦専用 1 体（imperial_fire_mage = 帝国大魔導士）は残置**：レア相当だが皇太子戦の固定編成で使う＝「皇太子戦は通常プール共用＋専用 1 体」の構成

#### 帝国軍 Name 対応表

| Name | 構成体数 | 内訳 |
|---|---|---|
| 帝国剣士 | 2 | fire_swordsman / water_swordsman |
| 帝国弓兵 | 2 | fire_archer / water_archer |
| 帝国大盾兵 | 2 | fire_tank / water_tank |
| 帝国補助兵 | 2 | fire_buffer / water_buffer |
| 帝国騎士 | 1 | light_paladin |
| 帝国司祭 | 1 | light_priest |
| 帝国大魔導士 | 1（皇太子戦専用）| fire_mage |

#### 実装したファイル

| 区分 | ファイル |
|---|---|
| 修正 | `EnemiesRoster`：AsImperial に nameOverride 引数追加＋ Element.None 上書き＋ファクトリ 11 体化（FireAssassin / WaterDispelTank / WaterHealer / LightMage 削除）／`IconRegistry`：削除 4 体のアイコンマッピング除去／`DebugBattlePresets`：「水 6 体」「光 4 火 2」「光 4 水 2」プリセットを残存ユニットで再構成（味方「聖 4 → 光 4」リネーム含む） |
| SO 削除 | `imperial_fire_assassin.asset` ／ `imperial_water_dispel_tank.asset` ／ `imperial_water_healer.asset` ／ `imperial_light_mage.asset`（各 .meta 含む 8 ファイル） |
| 仕様書 | [310 §1.13](310_vsprototype_spec.md) ユニット命名規則に「帝国軍命名規則」サブセクション追加（Name 対応表＋ Element.None 化＋通常プール非使用ユニット削除方針） |

#### ユーザー作業

- Editor で `Echolos/Data/SO アセットを生成` 実行（残 11 体の UnitDefinition SO の Name/Element 反映）
- 実機 Play で確認：
  - 敵編成に「帝国大盾兵」が並んだとき、能力差（反撃強化／専守）がホバーで識別できるか
  - R7 皇太子戦で「帝国大魔導士」が登場するか
  - Debug シーン（Debug_BattleSandbox）の新プリセットがエラーなく起動するか

---

### 2026-06-19 Preset 順序を「固有スキル左」に統一：敵 Lv 強化に固有を乗せる

敵側 Lv 強化の調査で「敵は AvailableUpgrades 先頭から順に適用＝順序 2 の固有スキル強化は絶対に適用されない」と判明。この副作用を踏まえ、Preset 順序を **「固有スキル → 第 2 ステ → HP」** に統一して敵 Lv2 で固有スキル強化が乗るように変更。

#### 設計判断

- **順序 0 に固有スキル**：敵 Lv2 で固有スキル強化が適用される＝R7 ボス取り巻きの専守／鼓舞／庇護／回復が強化された状態で出てくる
- **順序 1 に第 2 ステ**：敵 Lv3 で +DEF（or ATK）が乗る
- **順序 2 に HP +20**：敵に乗らない＝プレイヤー選択枠としてのみ機能（HP は敵に乗せると単純強化になりすぎるので意図的に外す）
- **固有スキルなし `AttackerTankPreset`** は「ATK → DEF → HP」のまま
- **プレイヤー UI 表示も同じ順序**：固有スキルが一番左に並ぶ＝ユニットの特徴が直感的に分かるレイアウト

#### 順序ルール（敵 Lv 適用との対応）

| Preset | 順序 0（敵 Lv2）| 順序 1（敵 Lv3 で追加）| 順序 2（敵不適用）|
|---|---|---|---|
| AttackerTankPreset | ATK+5 | DEF+3 | HP+20 |
| WaterShieldPreset | 専守+10% | DEF+3 | HP+20 |
| BufferFirePreset | 鼓舞+5 | DEF+3 | HP+20 |
| BufferWaterPreset | 庇護+3 | DEF+3 | HP+20 |
| HealerWaterPreset | 浄化の癒し+1 | DEF+3 | HP+20 |
| HealerLightPreset | 中回復+1 | DEF+3 | HP+20 |
| PrincessPreset | 王家の加護+2 | ATK+5 | HP+20 |

#### 実装したファイル

| 区分 | ファイル |
|---|---|
| 修正 | `UpgradeRoster`：全 7 Preset の順序入れ替え＋順序ルールのコメント追記 |
| 仕様書 | [310 §1.12.5](310_vsprototype_spec.md) Preset 表を順序別 3 列表に変更＋順序ルール明記 |

#### バランスへの影響（試遊で要確認）

- **R7 ボス取り巻き 5 体（全 Lv3）が顕著に強化**：水盾の専守が 30%→40%、鼓舞 +5、司祭の中回復 +1、魔導士・騎士の DEF+3 が乗る
- **通常プールは Lv1 固定のまま**＝影響なし（今後 R 帯別 Lv 上昇を入れたら同様の効果が段階的に出る）

---

### 2026-06-19 味方ユニット 15 体の Name 一新＋命名規則を仕様化

ユーザー判断で味方ユニットの命名規則を **「属性 + の + 役職」（非レア）／「特殊冠詞 + 役職」（レア）** に統一。固有ユニット 2 体（王女・ブリジット）以外の 15 体を一斉改名。

#### 設計判断

- **Id は変更しない**：属性プレフィックス（fire_ / water_ / light_）でソート時の並び順は確保されている＝ ID 変更は SO アセット／IconRegistry／全コードへの波及が大きいので Name 変更のみに留める
- **属性表記統一**：「火」「焔」「炎」混在 → 「炎」に統一。水属性「水」「霧」「穿」混在 → 「水」（非レア）／特殊冠詞（レア）に統一
- **役職表記の使い分け**：支援系は「師」（鼓舞師／護術師）、戦闘者は「士／兵／者」（剣士／大盾兵／暗殺者／弓兵）
- **格上げ**：「魔導士」「導師」→「大魔導士」でレア感を出す（焦熱の大魔導士／暁光の大魔導士）

#### 対応表（15 体）

| 旧名 | 新名 | 区分 |
|---|---|---|
| 双炎剣士 | 炎の双剣士 | 非レア |
| 火矢弓兵 | 炎の弓兵 | 非レア |
| 火槍盾兵 | 炎の大盾兵 | 非レア |
| 焔鼓舞師 | 炎の鼓舞師 | 非レア |
| 霧刃剣士 | 水の剣士 | 非レア |
| 水穿弓兵 | 水の弓兵 | 非レア |
| 水盾守護兵 | 水の大盾兵 | 非レア |
| 水護術士 | 水の護術師 | 非レア |
| 聖盾騎士 | 光の騎士 | 非レア |
| 聖光司祭 | 光の司祭 | 非レア |
| 焦熱魔導士 | 焦熱の大魔導士 | レア |
| 焔影刺客 | 焔影の暗殺者 | レア |
| 幻水盾兵 | 水鏡の幻盾兵 | レア |
| 水癒巫女 | 癒水の巫女 | レア |
| 聖輝導師 | 暁光の大魔導士 | レア |

#### 実装したファイル

| 区分 | ファイル |
|---|---|
| 修正 | `AlliesRoster`（Name 15 箇所）／その他コード（コメント／Description 内のユニット名参照：UpgradeRoster / BossRoster / VSPrototypeEnemyPool / VSPrototypeFirstRunFixedRoster / CounterAttackResolver / BattleEventRecorder / AttackEffect / DamageModifiers / DamageModifier / Flags / DebugBattlePresets） |
| テスト追従 | CounterAttackResolverTests / SilencedCounterTests / EnemyPatternsTests / WazaCatalogIntegrationTests / UnitCatalogIntegrationTests（メソッド名＋アサーション文字列） |
| 仕様書 | [310 §1.13](310_vsprototype_spec.md) ユニット命名規則を新設（Name パターン表＋ Id パターン）／[320 §4.8.1](320_vsprototype_combat_spec.md) 含む全箇所のユニット名置換 |

#### ユーザー作業

- Editor で `Echolos/Data/SO アセットを生成` 実行（UnitDefinition SO の Name フィールドが新名に更新される）
- 実機 Play で UI 表示が新名になっているか確認（左パネル王国軍リスト・配置モーダル・戦闘画面ステータスリスト）

---

### 2026-06-19 Lv 強化選択肢を全 17 体個別最適化＋固有スキル強化機構を新設

ユーザー試遊で「EVA +5 Upgrade を取っても効果がない」報告。調査の結果、`Unit.BaseEvasion` / `EffectiveEvasion` / `RuntimeUnit.TotalEvasion` / `EvasionBoost` UpgradeKind が完全に死蔵状態（AttackEffect.ComputeEvasionPercent が `ActiveEffects` の EvasionModifier だけしか見ない）と判明。さらに `WazaPowerBoost` UpgradeKind も「enum と TargetWazaId フィールドだけ用意・読む側ゼロ」の死蔵だった。

回避概念を全ユニット共通の枠から外し、特定ユニット（水鏡の幻盾兵の幻惑の盾）の固有スキルだけが回避を持つ設計へ整理＝代わりに全 17 体に個別最適化した Lv 強化選択肢を割り当て直す方針に。

#### 設計判断

- **EVA +5 を全 Preset から削除**（コミット 3 で死蔵枠ごと一括削除）
- **WazaPowerBoost を機能化**：TargetWazaId 一致で HealEffect.wazaPower / ApplyStatusEffectEffect.テンプレ Magnitude に加算
- **新 UpgradeKind `PersistentEffectBoost`**：(TargetEffectKind, TargetSourceAbilityName) 一致で Bootstrap.PrepareForBattle 時の PersistentEffect.Magnitude に加算
- **AttackerPreset / TankPreset を `AttackerTankPreset` に統合**：「攻撃可能なユニットはみんな ATK+5 / HP+20 / DEF+3」に共通化。攻撃 0 の Tank/Support だけが特殊 Preset
- **固有スキル強化 Upgrade 5 種新設**：水の大盾兵「専守 +10%」／炎の鼓舞師「鼓舞 +5」／水の護術師「庇護 +3」／癒水の巫女「浄化の癒し +1」／光の司祭「中回復 +1」
- **王女専用 BalancedPreset を `PrincessPreset` にリネーム**（汎用枠ではない明示）
- **IEffect 本体は不変保持**：ApplyStatusEffectEffect で Clone してから EffectMagnitudeAccumulator で破壊的に加算する型 switch 方式（IEffect インターフェース変更を避ける小規模リファクタ）
- **IActionContext に CurrentWazaId 追加**：ActionExecutor が実行中 Waza ID を載せる経路を新設（StubContext 6 件追従）

#### 実装したファイル（コミット 1）

| 区分 | ファイル |
|---|---|
| 新規 | `Domain/Models/UpgradeMagnitudeResolver.cs`（純関数：SumWazaPowerBoost / SumPersistentEffectBoost）／`Domain/Effects/EffectMagnitudeAccumulator.cs`（型 switch で Magnitude 加算） |
| 修正 | `UpgradeKind`（PersistentEffectBoost 追加）／`UnitUpgrade` / `UnitUpgradeDefinition`（TargetSourceAbilityName / TargetEffectKind フィールド追加）／`HealEffect`（wazaPower に WazaPowerBoost 加算）／`ApplyStatusEffectEffect`（テンプレ Clone → Magnitude 加算）／`IActionContext` / `ActionExecutor` / `AttackEffect.SubContext`（CurrentWazaId 結線）／`VSPrototypeBootstrap.PrepareForBattle`（PersistentEffectBoost 加算）／`UpgradeRoster`（新 Upgrade 5 種＋新 Preset 7 種を並行追加） |
| テスト追加 | `UpgradeBoostTests` 11 ケース／既存 StubContext 6 件に CurrentWazaId 追加 |

#### 実装したファイル（コミット 2）

| 区分 | ファイル |
|---|---|
| 修正 | `AlliesRoster`：全 17 体の AvailableUpgradeIds を新 Preset に切替／水鏡の幻盾兵 BaseATK 0→12（反撃機構を機能させるため・設定ミス修正） |
| 仕様書 | [310 §1.12.5](310_vsprototype_spec.md) Upgrade 一覧と Preset 表を新仕様に置換＋ UpgradeKind 新設の設計方針追記 |

#### 残（コミット 3）

死蔵枠の一括削除：`UpgradeKind.EvasionBoost` / `UpgradeRoster.EvaPlus5` / `Unit.BaseEvasion` / `Unit.EffectiveEvasion` / `RuntimeUnit.TotalEvasion` / `UnitDefinition.BaseEvasion` / `UnitCatalog` の引き継ぎ／旧 `BalancedPreset` / `AttackerPreset` / `TankPreset` / `SupportPreset`。死蔵削除でテスト影響あれば追従。

---

### 2026-06-19 戦線外配置の自動回収：CanAssign 純粋化＋ DischargeUnreachableAssignments 新設

戦線管理隣接性厳密化の続き。ユーザー試遊で「敵拠点攻め込み敗北＋同列敵領奪還戦敗北→敵領取り戻されると敵拠点に配置済みユニットが取り残されて回収不可」のバグ報告。原因：`ApplyAttackOutcome` が「攻め込み戦敗北→配置維持」設計で、その配置が次 R で攻略順序違反になるとモーダルすら開けない＝物理的に取り出せない。

#### 設計判断

- **CanAssign を純粋化**：「これから戦闘が発生し得るマスか」だけで判定。前コミットで入れた「配置済みあり救済（`|| AssignedAllies.Count > 0`）」を廃止
- **StartRound 末尾で自動回収**：CanAssign=false なマスから配置済みユニットを `ClearAllies`＝ Roster は所持の全集合なので自然に「手持ち」に戻る
- **「配置」と「解除モーダル」は分離しない**：個別 Pass で対応するより StartRound でまとめて棚卸す方が単純で漏れない（攻略順序違反／戦線終了／自領陥落で隣接性喪失 の 3 経路をワンセットで処理）
- **戦線外配置に投資する意味がない設計**：プレイヤーが意図的に「占領後の番兵」を置いても次 R で勝手に手持ちに戻る＝戦線概念が一貫

#### 実装したファイル

| 区分 | ファイル |
|---|---|
| 修正 | [VSPrototypeRoundManager](../Assets/Scripts/UseCase/VSPrototype/VSPrototypeRoundManager.cs)：`CanAssign` の救済削除＋ `StartRound` 末尾で `DischargeUnreachableAssignments` 呼び出し＋同メソッド新設 |
| テスト追従 | [RoundManagerFrontlineTests](../Assets/Tests/Editor/Domain/VSPrototype/RoundManagerFrontlineTests.cs)：「占領済み敵拠点で配置済みあれば配置可」を反転（戦線外は常に配置不可）／新規 3 件（攻略順序違反の自動回収／戦線外の占領済み敵拠点／自領陥落で戦線外化した敵領） |

#### 仕様書

- [310 §1.4.2](310_vsprototype_spec.md) CanAssign 表を BattleMode 純粋判定に更新
- [310 §1.4.2](310_vsprototype_spec.md) 「戦線外配置の自動回収（DischargeUnreachableAssignments）」節を新設＋発生経路 3 種の表

---

### 2026-06-19 自領陥落列を戦線外化：戦線概念の隣接性厳密化

人数バッジ追加後の試遊で「自領陥落列の敵領にも `0-4` 攻め込み戦が出る」報告。調査結果、過去セッションで [CalculateAttackFrontline](../Assets/Scripts/UseCase/VSPrototype/VSPrototypeRoundManager.cs) に「自領陥落でも攻め込み継続可能（戦略の幅）」コメントを残して意図的にそうしていた。ユーザー判断で「領地を奪い合うゲームとして直感的に正しい仕様＝陥落自領で隣接性が切れる」に方針転換。

#### 設計判断

- **自領陥落列はその先の敵領／敵拠点を戦線外化**：陥落自領（取り戻し戦）のみが戦線として残る。取り返した次 R で隣接敵領にも攻め込めるようになる（戦線前進）
- **CanAssign を BattleMode 基準で厳密化**：戦線外（`BattleMode=None`）への新規配置は不可。ただし配置済みユニットがあれば解除モーダル用に許可
- **Pass 1.5 の処理順は維持**：敵領占領→同列自領防衛戦キャンセル の構造（[CancelObsoleteDefense](../Assets/Scripts/UseCase/VSPrototype/VSPrototypeRoundManager.cs)）は既に「直感的に正しい」のでそのまま
- **エッジケース「敵領占領→次 R で自領陥落」は構造的に発生しない**：敵領占領時点で同 R の自領防衛戦は Pass 1.5 でキャンセル＝自領陥落の経路がない

#### 実装したファイル

| 区分 | ファイル |
|---|---|
| 修正 | [VSPrototypeRoundManager](../Assets/Scripts/UseCase/VSPrototype/VSPrototypeRoundManager.cs)（`CalculateAttackFrontline` に自領陥落ガード追加・`CanAssign` の敵領／敵拠点を BattleMode != None または配置済みありの 2 条件に厳密化） |
| テスト追従 | [RoundManagerFrontlineTests](../Assets/Tests/Editor/Domain/VSPrototype/RoundManagerFrontlineTests.cs)：既存 3 件書き換え（陥落列の敵領も戦線外確認・別列の敵領占領は陥落自領の取り戻し戦に独立・占領済み敵領は StartRound 経由で配置可）／新規 4 件（占領済み敵拠点で配置あり可／配置なし不可／自領陥落列の敵領／占領済み敵領で配置不可） |

#### 仕様書

- [310 §1.4.2](310_vsprototype_spec.md) 列ごとの最前線判定表で自領陥落行を「敵陣最前線なし」に変更＋ Why 注記追加
- [310 §1.4.2 配置可否](310_vsprototype_spec.md) CanAssign 表を BattleMode 基準に書き換え

#### 試遊での影響

- 自領陥落した列の敵領／敵拠点でバッジ非表示＝マップ表示と「攻め込めない」が一致
- 戦線外マスへの新規配置不可で「攻め込めないマスにユニットを送ってしまう」混乱を排除
- 隣接戦線（自領防衛 → 敵領攻め込み → 敵拠点攻め込み）が直感的に揃う

---

### 2026-06-19 戦略マップに人数バッジ追加：攻撃予告＋配置済み視認

§1.3 戦略マップ系の残「攻撃予告＋配置済み視認」を実装し、戦略マップ系を一旦クローズ。

#### 設計判断

- **数字 2 つ＋枠色で省スペース表現**：マス下端中央に `味方-敵` を 50×22 px の小バッジで描画。配置上限は常時 6 なので `N/6` 表記は省略
- **枠色は BattleMode × 人数比**：
  - 防衛系（Defense / Boss）= 赤 / 黄 / 水色（強調）
  - 攻め込み（Attack）= 灰 / 橙 / 青（中庸）
  - 戦線外（None）= 灰固定
  - 「安全」判定はユーザー指示通り頭数比較（`味方 ≧ 敵`）で割り切り
- **戦線外の敵領／敵拠点はバッジ非表示**：何も起こらないマスなので情報ノイズ回避。戦線外の自領・本拠地は `0-0` でも灰枠で表示（味方は配置可能・レイアウト一定性優先）
- **本拠地戦予告は自動対応**：StartRound で陥落自領ありなら Home に BattleMode=Defense ＋ EnemyComposition がセットされる＝同じバッジ機構で赤/黄/水色が出る＝本拠地特別 UI 不要
- **絵文字回避**：環境依存リスクを避け数字のみ＋枠色で表現

#### 実装したファイル

| 区分 | ファイル |
|---|---|
| 修正 | [VSPrototypeMapGUI](../Assets/Scripts/Presentation/VSPrototype/VSPrototypeMapGUI.cs)（配色定数 7 種追加＋ `_badgeStyle` GUIStyle ＋ `DrawPersonnelBadge` / `ResolveBadgeFrameColor` 新設＋ `DrawMapNode` から呼び出し） |

#### 仕様書

[310 §1.8](310_vsprototype_spec.md) 表示仕様に「マス人数バッジ」項を新設（枠色表＋戦線外非表示ルール）。

#### ユーザー作業

実機 Play で確認：
- 各ラウンド開始時、戦線最前線マスに敵編成数が `0-N` の赤枠で出るか
- 味方を配置すると枠色が黄 → 水色に変化するか
- 敵領／敵拠点を制圧した次ラウンドで、その列の自領（戦線外化）からバッジが消えるか
- 攻め込み戦マスに未配置で `0-N` 灰枠（攻めず＝警告なし）が出るか
- 本拠地は陥落自領なし時 `M-0` 灰枠／陥落自領発生時に赤/黄/水色が出るか

---

### 2026-06-19 無音ステップ集約：LogLine 非空 Event まで 1 ステップで消化

Aura 解除（Permanent + IsUndispellable で `suppressTextLog=true`）等で LogLine=null Event が連続すると、UI のカーソル進行が「無音ステップ × 出撃人数分」になり「壊れて見える」試遊報告。

#### 設計判断

- **1 ステップの定義変更**：「LogLine 非空 Event を 1 件 Apply するまで一気に消化」が 1 ステップ
- **Auto / Step 両モード統一**：`Tick`（Auto）と `StepOne`（Step）が同じ `AdvanceOneStep` ヘルパを呼ぶ＝ Step ボタン 1 押下 = Auto モード 1 ステップで完全一致＝挙動差によるバグ見落としを防止
- **ずれ回避は構造で保証**：`ApplyEvent` は HP/Alive/snapshot 更新のみ（冪等＋順序依存）／連続適用でも各更新は順次反映／LogLine 非空で必ず止まる＝表示同期点が確定
- **安全弁**：`MaxEventsPerStep=64` で 1 ステップ消化上限を設定（LogLine=null Event 大量連続でも操作不能にならない）

#### 実装したファイル

`VSPrototypeBattleGUI`：`AdvanceOneStep` ヘルパ新設＋ `Tick` / `StepOne` を統一。

---

### 2026-06-19 オーラ解除と Died Event の順序保証：AuraTracker 遅延 flush 化

ユーザー試遊で「王女がまだ生きてる演出段階で味方全員から王家の加護バッジが先に消える」報告。
即時剥奪設計の AuraTracker が `OnEffectRemoved` を発火＝ `StatusEffectExpired` Event が
`ActionResolved`（Outcome.ResultedInDeath で UI に死亡が伝わる）より先に Events リストへ積まれ、
カーソル進行で UI が「バフ消失 → 後で戦闘不能」の逆順になっていた。応急処置でタイミング合わせる
のではなく、構造的に順序保証する設計に変更。

#### 設計判断

- **HandleUnitDied は pending キュー追加のみ**：即時解除しない
- **FlushPendingDeaths を節目で実行**：
  - `Executor.OnActionResolved` 後（攻撃由来死亡経路・`ActionResolved` Event 後に flush）
  - `Manager.OnEndPhase` 後（Burn / 呪い即死経路・`StatusProcessor.HandleEndPhase` で `OnStatusEffectKill` 経由
    の `Died` Event が積まれた後で flush）
- **2 経路の死亡通知を両方拾う**：`Executor.OnUnitDied` ＋ `StatusProcessor.OnStatusEffectKill` の両方を
  `HandleUnitDied` で受ける（重複追加は無視）
- **応急処置でなく契約として組み込み**：「Aura 解除 Event は必ず Died Event の後」を AuraTracker の
  仕様として明文化

#### 実装したファイル

| 区分 | ファイル |
|---|---|
| 修正 | `AuraTracker`（`_pendingDeaths` キュー＋ `FlushPendingDeaths` 追加・`HandleUnitDied` をキュー追加のみに変更）／`BattleAssembly.WireBattleLogic`（4 経路の結線追加・`Executor.OnActionResolved` ＋ `Manager.OnEndPhase` で flush・`StatusProcessor.OnStatusEffectKill` でキュー追加） |
| テスト追従 | `AuraTrackerTests`：各ケースに `FlushPendingDeaths` 呼び出しを追加＋「Flush 前は剥奪されない」アサート＋「同一 unit 重複追加は無視」ケース新規 |

#### 仕様書

[320 §4.9.3](320_vsprototype_combat_spec.md) 動的解除に「Event 順序保証」段を追記。

#### 試遊での影響

- ログとバッジの進行が 1:1 で揃う（王女が落ちる演出の **後** にバッジが消える）
- 同じ「アクション解決中の OnEffectRemoved → ActionResolved との順序ずれ」問題は他にも潜在するが、
  この対応で AuraTracker 経路は構造的にゼロ。今後のオーラ追加でもタイミングずれが再発しない契約

---

### 2026-06-19 同 Kind 効果の SourceAbilityName 単位分離：連携と鼓舞が重なる修正

ユーザー試遊で「連携 +3 と鼓舞 +N のバッジが重なっていないように見える」報告。調査結果、UI ではなく `StatusEffectStacker.ApplyWithStacking` が **同 Kind の既存効果に常にマージ**する設計で、連携（Permanent）が鼓舞（Triggered）で上書きされていた（Magnitude が鼓舞の値で書き換わる＝連携の +3 が事実上消える）。

#### 設計判断

- **SourceAbilityName 単位でスタック識別**：同 Kind ＋ 同 SourceAbilityName ならリフレッシュ、異なる SourceAbilityName なら共存
- 既存 Source 同士のリフレッシュ挙動は据え置き（後方互換）
- null / 空文字 Source 同士は同一視（旧来の無名 Source 系効果が予期せず分裂しないよう保護）
- UI snapshot も同様に `(Kind, SourceAbilityName)` ペアで一致判定＝バッジが 2 件並び、ホバーで個別表示

#### 実装したファイル

| 区分 | ファイル |
|---|---|
| 修正 | `Domain/Battle/Skills/StatusEffectStacker`（FindEffect 検索条件を Kind + SourceAbilityName に変更＋ SameSource ヘルパ）／`VSPrototypeBattleGUI`（AddOrReplaceEffectSnapshot / RemoveEffectSnapshotBySource を Source 一致に変更＋ SameSource ヘルパ） |
| 新規テスト | `StatusEffectStackerTests` 5 ケース（連携+鼓舞共存／同 Source リフレッシュ／異 Kind 当然共存／null/空文字同一視／ImmunityKinds 弾き） |

#### 仕様書

[320 §4.7.3a](320_vsprototype_combat_spec.md) スタックとマージのルール節を新設。

#### バランスへの影響

これまで「同 Kind は上書き」だったので連携が鼓舞に消されて鼓舞だけ効いていた箇所が、今後は **合算で強くなる**。プロト試遊で気になれば個別調整。

---

### 2026-06-19 オーラの動的解除：AuraTracker 新設

ブリジット能力実装の続き。「連携の片方が戦闘中に倒れたら効果が剥がれるのが自然」というユーザー指摘を受け、オーラ機構全般に動的解除を追加。

#### 設計判断

- **AuraSourceId 流用で識別**：AuraApplier 付与時に `eff.AuraSourceId = def.SourceAbilityName` をセット。Tracker は死亡ユニットの依存先 Def を SourceUnitId / RequiredPartnerUnitIds から見つけ、`AuraSourceId == def.SourceAbilityName` な効果を陣営から `RemoveEffectsWhere` で削除
- **Executor.OnUnitDied 結線**：StatusProcessor.HandleUnitDied と並行購読＝戦闘ロジックには影響しない
- **副次効果として王家の加護も動的解除**：王女死亡で味方全体 DEF +N も剥がれる。連携と整合的＝オーラ機構全般の挙動として自然
- **Recorder の OnEffectRemoved 抑制ルール調整**：`_inActionResolution` 中でも Aura 起因解除（AuraSourceId 持ち）は Event を残すように変更。アクション中の死亡経路で UI snapshot が段階更新される（バッジが正しく消える）

#### 実装したファイル

| 区分 | ファイル |
|---|---|
| 新規 | `Domain/Battle/Aura/AuraTracker.cs` |
| 修正 | `AuraApplier`（付与時に AuraSourceId セット）／`BattleAssembly`（AuraTracker 生成＋ Executor.OnUnitDied 結線）／`BattleEventRecorder.SubscribeUnitEffects`（Aura 起因の OnEffectRemoved は _inActionResolution 中でも記録） |
| テスト | `AuraTrackerTests` 5 ケース（王女死で王家の加護剥奪／ブリジット死で連携剥奪／王女死で連携剥奪／関係ない死亡で何も起きない／null 安全） |

#### 仕様書

[320 §4.9.3](320_vsprototype_combat_spec.md) 仕様節に「動的解除」を追記。

---

### 2026-06-19 ブリジットの能力実装：連携（王女）＋王家のペンダント

§1.2 王女・ブリジットのスキル実装の残り＝ブリジット側 2 能力を実装し、固有ユニット系を完全クローズ。

#### 設計判断

- **連携は AuraApplier 拡張で実現**：`AuraDefinition` に `RequiredPartnerUnitIds`（必須パートナー・1 体でも欠ければ不発）と `TargetMode`（陣営全員 vs SourceUnit+Partners）を追加。新規機構を立てず、王家の加護と同じ Manager.OnBattleStart 経路に乗せる
- **BoostUpgradeKind を `UpgradeKind?` に nullable 化**：王家の加護のメタ強化反映と「連携にはメタ強化を効かせない」要件を両立。null 指定で Boost なし
- **反撃無効化は新 EffectKind `IgnoreCounter` を新設**：既存 `SilencedCounter`（被弾側基準＝水の大盾兵「専守」）と直交する**攻撃側基準**フラグ。CounterAttackResolver で攻撃側に IgnoreCounter があれば反撃判定 false を返す
- **基礎ステータスは ATK 全味方最強**：HP/DEF 据え置きの「ガラスの大砲」設計（HP 110 / ATK 50 / DEF 3 / SPD 12・炎の双剣士の +10 / 王女の +6）
- **王家のペンダントは Unit.PersistentEffects に組込**：Bootstrap.PrepareForBattle 経由で戦闘開始時に AddEffect される＝前回の P4 で対応した `SubscribeUnitEffects` 冒頭の初期 PersistentEffects 集約に乗り、バッジ＆ログに自動表示

#### 実装したファイル

| 区分 | ファイル |
|---|---|
| 新規 | `Domain/Battle/Aura/AuraTargetMode.cs`（enum）／`Domain/Effects/Flags.cs` に `IgnoreCounterFlag` 追加 |
| 修正 | `AuraDefinition`（RequiredPartnerUnitIds / TargetMode / BoostUpgradeKind nullable）／`AuraApplier`（パートナー全員生存判定＋ TargetMode 配布範囲切替＋ Boost null ガード）／`AuraDefinitions`（BridgetCovenant 追加）／`EffectKind`（IgnoreCounter）／`EffectFactory.CreateByKind`（case 追加）／`CounterAttackResolver`（攻撃側 IgnoreCounter チェック）／`AlliesRoster.Bridget()`（ステ更新＋ AbilityLabels＋ PersistentEffects に王家のペンダント）／`VSPrototypeBattleGUI`（FormatKindShort `IC` / FormatKindLong `無反撃`） |
| テスト | `AuraApplierTests` 連携系 6 ケース／`CounterAttackResolverTests` IgnoreCounter 系 2 ケース |

#### 仕様書同期

- [320 §4.7.1](320_vsprototype_combat_spec.md) 効果型階層表に `IgnoreCounterFlag` 追加
- [320 §4.8.1](320_vsprototype_combat_spec.md) ブリジット数値更新
- [320 §4.8.3](320_vsprototype_combat_spec.md) 効果 Magnitude 暫定一覧に `IgnoreCounter` 追加
- [320 §4.9.1](320_vsprototype_combat_spec.md) オーラ表に「連携」追加＋発動条件／配布範囲列追加
- [320 §4.9.3](320_vsprototype_combat_spec.md) 仕様節に RequiredPartnerUnitIds / TargetMode / BoostUpgradeKind 追記

#### ユーザー作業

- Editor で `Echolos/Data/SO アセットを生成` 実行（ブリジット SO のステ値と PersistentEffects 反映）
- Test Runner で All Tests 実行
- 実機 Play で確認：
  - ブリジット単独配置時に「連携」バッジが出ないこと
  - 王女と同時配置で両者の ATK バッジに「連携：+3」が出ること
  - ブリジットが攻撃した際、相手から反撃を受けないこと
  - 王家のペンダントバッジが戦闘開始時から表示されること（前回 P4 の初期 PersistentEffects 集約経路）

---

### 2026-06-19 状態異常バッジの段階再生対応

VSPrototypeBattleGUI の状態異常バッジが **戦闘終了時の最終状態を常に表示** する問題（HP は段階再生されるのに、状態異常バッジは `RuntimeUnit.ActiveEffects` をライブ直読しているため）を修正。Phase 2 戦闘 UI 実装時に追従漏れしていた箇所。

#### 設計判断

- **値スナップショット方式**：戦闘ロジック終了後の IEffect は最終状態に確定するので、UI が直読する形だと再生不可。BattleEvent / HitOutcome に値オブジェクト `EffectChange`（Kind / Stacks / RemainingTurns / Lifetime / SourceAbilityName / IsCleansable / IsUndispellable）を載せ、UI 側に snapshot 辞書を構築する
- **同 Kind は 1 件で上書き**：StatusEffectStacker と整合。再攻撃の Stacks リフレッシュは ApplyStatusEffectEffect が `target.FindEffect(Kind)` の最新値を Outcome に乗せるため、UI は自動的に最新 Stacks に追従
- **RemainingTurns のターン経過減算はヒューリスティック**：StatusEffectProcessor.OnEndPhase の減算を Event 化せず、UI 側で TurnStart 時に全 Triggered 効果の RemainingTurns を 1 ずつ減らす（Permanent は不変）。自然消滅は OnEffectRemoved → StatusEffectExpired Event 経由で別途反映

#### 実装したファイル（4 コミット）

**P1 `81f2d20`**：`HitOutcome.AppliedEffects` / `RemovedEffects` を `IReadOnlyList<EffectKind>` から `IReadOnlyList<EffectChange>` に昇格
- 新規：`Domain/Battle/EffectChange.cs`
- 修正：HitOutcome ／ ApplyStatusEffectEffect ／ Dispel{Buffs,Debuffs}Effect ／ CleanseStatusAilmentsEffect ／ BattleLogFormatter
- テスト追従：ActionExecutorTests ／ HealAndApplyEffectTests ／ BattleLogFormatterTests（`ToChanges` ヘルパで簡略化）

**P2 `87b5342`**：`BattleEvent.EffectKind` フィールドを `EffectChange` に昇格
- 修正：BattleEvent ／ BattleEventRecorder（OnEffectAdded/Removed コールバックと FlushBattleStartBuffer で From() 埋め込み）
- テスト追従：BattleRunnerTests

**P3 `1526d7c`**：VSPrototypeBattleGUI に snapshot 辞書を追加し、描画とホバーを ActiveEffects 直読から snapshot 参照に切替
- `_effectsSnapshot: Dictionary<RuntimeUnit, List<EffectChange>>` を新設
- `InitializeForSegment` で初期化（陣営両方の Lineup 分）
- `ApplyEvent` の case 拡張：StatusEffectApplied／Expired／ActionResolved（AppliedEffects/RemovedEffects 反映）／Died（クリア）／TurnStart（Triggered RemainingTurns 段階減算）
- `DrawStatusEffectBadges` / `DrawSingleStatusBadge` / `DrawStatusEffectTooltip` / `GetCategoryBorderColor` / `GetCategoryLabel` を `IEffect` → `EffectChange` 引数に置換
- `_hoveredStatusEffect` 型も `IEffect` → `EffectChange`

**P4 `a713751`**：Permanent 系（シナジー/オーラ/固有パッシブ）バッジが表示されないバグ修正
- 原因 1：`FlushBattleStartBuffer` 集約 Event が複数 unit 対象だと `Target=null` で UI ガード（`ev.Target != null`）に弾かれていた
- 原因 2：ユニット固有 PersistentEffects（水の大盾兵「専守」等）は `SubscribeUnitEffects` 購読**前**に `Bootstrap.PrepareForBattle` で AddEffect されているため、`OnEffectAdded` 経路では拾えていなかった
- 新規：`Domain/Battle/EffectApplication.cs`（(Unit, EffectChange) ペア値オブジェクト）
- `BattleEvent.BulkEffectChanges: IReadOnlyList<EffectApplication>` 集約フィールド追加
- `Recorder.SubscribeUnitEffects` 冒頭で対象 unit の現状 ActiveEffects を `_battleStartBuffer` に予め積む処理追加（SelfGuard 除外）
- `FlushBattleStartBuffer` 内で集約 Event に `BulkEffectChanges` 全件埋め込み
- `ApplyEvent` の StatusEffectApplied case を Bulk 優先＋単体フォールバックに改修

#### 動作確認（ユーザー作業）

実機 Play でバッジが段階的に更新されるかチェック：
- 戦闘開始時シナジー/オーラ/固有パッシブのバッジが Turn 1 開始から出るか
- 攻撃で付与した Burn のバッジが付与ステップで出現するか
- Burn の Stacks が再攻撃でホバー表示の数値も上がるか
- Triggered 効果のホバー表示「残り N T」がターン経過で減るか
- 自然消滅／Dispel/Cleanse のステップで該当バッジが消えるか
- 戦闘不能ユニットのバッジが全消えするか

ロジック側に手入れなし＝ Test Runner は既存テスト走るだけ（HitOutcome 型変更追従済）。

#### 設計上の弱点メモ（プロト範囲では未対応・ユーザー判断で 912 §1 にも記載なし）

`FormatKindShort` / `FormatKindLong`（VSPrototypeBattleGUI）／ `GetMagnitude`（BattleLogFormatter）／ `EffectFactory.CreateByKind` が EffectKind ごとの case 分散になっており、新規 EffectKind 追加時に 3〜5 箇所同期メンテが必要（OCP 違反）。改善案は Registry 集約／IEffect に `ShortLabel` / `DisplayName` プロパティ追加／enum 属性駆動の 3 つ。本実装フェーズで再検討の余地あり。

---

### 2026-06-19 王女オーラ機構（王家の加護）実装

§1.2 王女・ブリジットのスキル実装のうち、**王女「王家の加護」のみ**本実装した（ブリジットは別軸検討でユーザー判断）。

#### 設計判断

- **対象は王女のみ**：VSプロト 17 体に軍師・傭兵は不在＝オーラ機構の最初の適用先は王女 1 体。AuraApplier は汎用化されており、SourceUnitId を切り替えれば将来の追加固有ユニットにそのまま流用可能
- **5 層パターン採用見送り**：SynergyApplier が SO 化していないので整合性で同じハードコード集約（`AuraDefinitions.All`）。データ駆動が必要になったら昇格
- **メタ強化との連動**：王女メタ拠点 Lv 強化の 3 択（BalancedPreset）を `{ ATK +5, HP +20, AuraBoost +2 }` に置換。新 `UpgradeKind.AuraBoost` を介して SourceUnit の `AppliedUpgrades` から Magnitude 合計を読み取り、AuraBuff.BaseMagnitude に加算
- **数値（仮）**：基本 DEF +3 ／メタ強化 1 段 +5 ／2 段 +7
- **発火タイミング**：`BattleAssembly.WireBattleLogic()` で `Manager.OnBattleStart` に `SynergyApplier.ApplyAll` の直後を結線。戦闘中の動的再評価なし

#### 実装したファイル

| 区分 | ファイル |
|---|---|
| 新規（4 件） | `Domain/Battle/Aura/AuraBuff.cs` ／ `AuraDefinition.cs` ／ `AuraDefinitions.cs` ／ `AuraApplier.cs` |
| 修正 | `Domain/Models/UpgradeKind.cs`（AuraBoost 追加）／ `Data/Roster/UpgradeRoster.cs`（AuraGuardPlus2 追加＋ BalancedPreset 置換）／ `Domain/Battle/Replay/BattleAssembly.cs`（AuraApplier 結線） |
| テスト | `Tests/Editor/Domain/Battle/AuraApplierTests.cs`（8 ケース：不在／生存／Boost1個／Boost2個／敵陣営対象外／Source戦闘不能／戦闘不能対象除外／SourceAbilityName伝搬） |

#### 仕様書同期

- [320 §4.9](320_vsprototype_combat_spec.md) オーラ節を新設（対象・メタ強化連動・仕様）
- [500 §2.2 ディレクトリ表](500_architecture.md) に `Battle/Aura/` 追記
- [500 §3.6 マッピング表](500_architecture.md) に「固有ユニットのオーラ」行追加
- [912 §1.2](#12-固有ユニット系) を実装完了表記に変更／§1.5 を軍師・傭兵拡張メモへ縮約

#### ユーザー作業

Editor で `Echolos/Data/SO アセットを生成` 実行が必要：
- 新 Upgrade SO `up_aura_guard_plus_2.asset` の生成
- 王女ユニット SO の `AvailableUpgradeIds` 反映（BalancedPreset の中身が変わったため）

実機試遊で「王女配置時に味方全体 DEF +3」が発動するか、メタ強化で「王家の加護 +2」を選んだら DEF +5 / +7 になるかを確認。

---

### 2026-06-17（後半）Tier A スコープ実装：1 系設計詰め～ 3 系戦線概念

ユーザー指示で「プロトのバランス調整に無限に時間かけても仕方ない」ため Tier A スコープを定義して着地優先で進行。Lv 軸統合（C1〜C6）と固有ユニットメタ強化（F1〜F6）完了後の続き。

#### Tier A スコープ確定（作業フロー 3）

| Tier | 対応 |
|---|---|
| A | 必須：[1] 設計詰め → [2] GUI 新規実装 → [3] バランス調整 → [4] アセット投入 |
| B | 余裕あれば：シナジー発動バッジ／状態異常バッジ（→ A 昇格） |
| C | 切り：地形バッジ／配置 ATK 補正可視化／検証ツール／110 本番版 |

#### 完了タスク 8 群

| # | 内容 | ハッシュ |
|---|---|---|
| 1-1 | 敵プール構成：Required+Random 二段構造＋ SortOrder で並び順保証（弱5 / 中6+2 / 強6+2+2） | `17269b9` |
| 1-2 | 固有ユニット強化 3 択：王女 BalancedPreset / ブリジット AttackerPreset、オーラ機構は本実装送り | `1c4b65f` |
| 1-3 | ラスボス編成：皇太子＋取り巻き 5 体固定 6 体（中ボス廃止）。新 Waza 2 種（闇剣／闇波・CD=2 交互発動） | `22183cc` |
| 2A | シナジー発動バッジ：アリーナ左上 32x32x3 重畳＋ホバーツールチップ＋画像/文字フォールバック | `b73ea16` |
| 2B | 状態異常バッジ：ユニット画像下端 22x16 重畳＋カテゴリ色枠（Cleansable 赤 / Persistent 青 / Triggered 黄）＋ホバー詳細 | `6996192` |
| 3a | 自領防衛をラウンド進行でプール切替：R1-2 弱 / R3-5 中 / R6 強 | `9f33721` |
| 3b | 戦線概念導入：自陣最前線／敵陣最前線／奪還戦／取り戻され＋ MapNode.RevertCapture | `056a1a3` |
| 3c | 本拠地連戦廃止＋自領取り戻し戦：陥落自領にチャンスを与える設計＋ MapNode.RevertFallen | `b196993` |

#### 設計判断

- **固有ユニットのオーラ系パッシブ**：「全体オーラ常時バフ」は現行機構（PersistentEffect / SynergyApplier / Waza）のどれにも該当しない振る舞い。専用機構 `AuraApplier` の新規導入が必要で、Tier A スコープ外として本実装フェーズに送り（§1.5 参照）。
- **中ボス**：VSプロト範囲では実装しない（R6 マスは強プール抽選のまま）。皇太子（ラスボス）のみ固定編成。
- **戦線概念**：現実装は「マスごとに独立した戦場」だったが、ユーザー指摘で戦略SLGとして自然な「列ごとの戦線」モデルに刷新。
  - 列の最前線が「敵が攻めてくる場所」になる
  - 占領済み敵領／敵拠点も奪還戦の対象（放置で安全＝退化戦略を構造的に抑止）
  - 自領陥落でもラウンド末まで猶予あり＋次ラウンドから取り戻し戦可能
  - 連戦廃止で「うっかり 1 敗で即終了」を回避
- **攻めてくる敵は R 帯ベース**（場所不問）：自領防衛／奪還戦／取り戻し戦／本拠地戦すべて同一 R 帯切替プール。

#### 試遊観察ポイント（残）

- 戦線概念の手応え：左の敵領占領→次 R で左敵領が奪還戦になるか
- 自領陥落→ラウンド末まで猶予がプレイヤーに伝わるか
- 取り戻し戦勝利→次 R で防衛戦に戻るか
- 全自領取り戻し成功→本拠地戦が発生しないか
- R6 の自領防衛 × 3 ヶ所が強プールでキツすぎないか

### 2026-06-15 テスト整理（Step prefix 除去＋サブディレクトリ化）

戦闘リファクタ中の経緯由来 prefix（`Step2_X_*` / `Step2X_X_*` / `Step3CXY_*` / `Step5_*` / `VSProto_*`）が
Step の意味が分からず追いづらかった問題を解消。

- **物理配置**：`Assets/Tests/Editor/Domain/` 直下 flat → `Battle/` `GameCycle/` `VSPrototype/` の 3 サブディレクトリ化
- **クラス名・ファイル名**：すべて対象クラスと同名に統一（例：`Step2_2_DamageFormulaTests` → `DamageFormulaTests`）
- **namespace** は `Echolos.Tests.Domain` 据え置き（48 ファイルの using 修正回避）
- **特例**：
  - `Step2_1_StatusRedefinitionTests` → `RuntimeUnitStatTests`（中身が Unit/RuntimeUnit のステータス全般のため意味的に変更）
  - `Step2X_1_BattleContextIntegrationTests` → `BattleContextTests`（Integration は冗長）

コミット分割：C1 物理移動 → C2 Step2_/Step2X_ リネーム → C3 Step3 リネーム → C4 Step5/VSProto_ リネーム → C5 仕様書同期。

### 2026-06-16 Debug_BattleSandbox シーン復活

戦闘単体検証のために [Debug_BattleSandbox.unity](../Assets/Scenes/Debug_BattleSandbox.unity) を新戦闘システム対応で復活。

旧 Bootstrap（`_old/` 退避済）は (1) 旧 `Echolos.Domain.Prototype` Roster 系（Phase R-3 で物理削除済）依存、
(2) 旧 ID 体系（`tank_def` / `samurai` / `boss_*`）ハードコード、(3) 旧 `BattleRunner.Run` 4 引数 API、の点で
新システムと非互換。

新版 [DebugBattleSandboxBootstrap.cs](../Assets/Scripts/Presentation/DevTools/DebugBattleSandboxBootstrap.cs)：
- `IUnitCatalog` / `IWazaCatalog` 経由で Unit を動的解決（旧 `UnitIdResolver` 廃止）
- Roster 命名規約「`imperial_*`」で陣営分離
- 新 `BattleRunner.Run` 9 引数 API＋ `PrepareForBattle` 同等処理を移植
- **追加検証機能**：攻め/守りトグル＋地形強度 3 択（Light/Medium/Heavy）を GUI で可変
  → Phase R-6 で戦線位置（自領 Light / 敵領 Medium / 敵拠点 Heavy）を再現してバランス検証できる

[DebugBattlePresets.cs](../Assets/Scripts/Presentation/DevTools/DebugBattlePresets.cs) は雛形のみ（「（空）」エントリだけ）。
具体的なプリセットは利用時に `new() { Name, Description, UnitIds }` を List に追加する設計。

シーン GUID 引継ぎ（`b34696c89bbd2b143ab5efd8966763e4`）で Missing Script 自動解決＋ユーザー側の再アタッチ不要。
合わせて `Presentation/DevTools/_old/` 配下を物理削除＋ 500 §13.1 退避表から `Echolos.Presentation.DevTools.Old` 行削除。

### 2026-06-16〜17 戦闘ログ／観戦ビュー周りの大規模整備＋戦闘ロジック修正

Phase R-6 バランス調整に向けた事前整備として、試遊観察ベースで戦闘ログ表示と
HP バー同期の構造的問題を一掃した。論点別に分割しつつ最終的に Log と Events を
構造的に 1:1 保証する設計に転換。

**戦闘ロジック修正**：
- 動的順序方式：行動順を事前確定する旧方式を廃止し、各ステップで未行動かつ生存の
  ユニットから実行時 SPD で次の 1 体を選定する while ループに置き換え
  （[Docs/320 §3.1](320_vsprototype_combat_spec.md) 追記）。問題 3「先行ユニットが
  ターゲットを倒した時の後続不発」を内包解消＋本番想定の SPD バフ・デバフにも備える
- 属性シナジー味方陣営限定：敵編成はランダム抽選なので発動偏りが数値設計を歪める
  問題を `SynergyApplier.ApplyAll` 内で構造的解消（[Docs/320 §4](320_vsprototype_combat_spec.md) 共通注記）
- シールド吸収時の Rider 無効化：`AttackEffect` で Shield 吸収時に onHitRiders を
  スキップし付帯付与も無効化（[Docs/320 §4.1](320_vsprototype_combat_spec.md) 水属性シールド項追記）
- Burn ダメージ順序修正＋ Died イベント追加：旧仕様は OnBurnTickDamage 発火が
  CurrentHP 減算「前」だったため Recorder が Burn 適用前 HP を記録していた問題＋
  Recorder が OnStatusEffectKill を購読していなかったため Burn 致死で Died イベント
  記録漏れ＋グレーアウト不発の問題を一括修正
- 看破/巫女浄化の解除を ActionResolved 集約に内包：HitOutcome.RemovedEffects を
  新設し、Dispel/Cleanse 系 Effect が「削除予定リストアップ → 個別 RemoveEffect →
  HitOutcome.Add」する設計に変更。「解除ログ → 看破宣言」順序逆転を解消

**観戦ビュー側の構造改善（Log/Events 1:1 化）**：
- `BattleEvent.LogLine` プロパティ統合：1 行ログを Event 自身に格納（[Docs/500 §3.6](500_architecture.md) マッピング表追記）
- `BattleEventRecorder.AddEvent(ev, logLine)` 共通ヘルパー集約：直接 Log.Add /
  Events.Add する経路を 1 箇所のみに限定＝規約依存ではなく型で強制
- `BattleReport.Log` を IReadOnlyList<string> get-only に変更（Events.LogLine から
  動的生成・派生 view）
- VSPrototypeBattleGUI を Events.LogLine 直接参照に切替＋ cursor=N-1 で同期
- 戦闘開始シナジー付与ログを SourceAbilityName ごとに 1 行集約＋ Lv 表記＋ Shield/
  バフ数値表示（「水の共鳴 Lv6：DefenseUp +30 & Shield 3 付与」のような表示）
- Step モードのワンテンポずれ解消（logIndex = _cursor - 1）

**バランス検証準備**：
- 1 周目固定構成変更：火 2／水 2／光 2 で全シナジー 2 体段階成立型に
- 列地形 GUI 表示（戦略マップ右パネル 1 行・抽選ロジック自体は未実装＝枠だけ）
- Debug_BattleSandbox 検証プリセット 4 種追加（火 6／水 6／聖 4 火 2／聖 4 水 2・
  味方／敵対称・王女は敵側で炎の双剣士代用）

**残タスク（次セッション）**：
- Phase R-6 バランス調整本体：[Docs/320 §4.8.1〜§4.8.3](320_vsprototype_combat_spec.md) 暫定値を
  実プレイ＋ Debug_BattleSandbox で調整
- 110 本番版反映（メンテ不可なら破棄＋再作成判断）

### 2026-06-17 敵プール × Unit.Level 軸統合設計（次セッション実装予定）

仮置きの敵編成（`VSPrototypeEnemyPatterns` のマス種別ごと固定 1 編成）を Phase R-6
バランス調整本体に乗せるための設計確定セッション。「ラウンドごとに敵 1.05 倍」型の
暗黙倍率を撤廃し、**敵味方共通の `Unit.Level` 軸** で強度カーブを表現する方針に切替。

**設計の核心**：

| 軸 | 内容 |
|---|---|
| Lv 上限 | Lv3（強化 2 回） |
| 強化選択 | 各 Unit が固有の **3 択 `AvailableUpgrades`** を持ち、Lv2 で 1 つ・Lv3 で残り 2 択から 1 つ選択（3 つ中 2 つ取得、1 つ捨てる） |
| 強化内容 | ステ上昇＋技数値強化（バフ Waza の Magnitude +）。新規技追加・効果激変はしない |
| 強化主体 | **個別ユニット**（旧「兵種＝同種全部に効く」から変更）。同種でも A は Lv3／B は Lv1 が成立 |
| 一般兵種強化 | 兵種強化 UI（行動力消費）から個別選択 |
| 固有ユニット | メタ強化画面から強化（メタ通貨消費）。3 択構造は同じ。仮置きは ATK/DEF/HP の 3 択 |
| 敵側 | 同じ 3 択を持ち **順序番号 0/1/2** を裏で持ち Lv 上昇のたびに 0→1 順に適用（順序 2 は実質「捨てる」扱い・味方と完全対称） |
| マスタボーナス | プロト未実装（フル版要素・拡張余地は残置） |

**仕様書側更新（本セッション完了）**：
- [Docs/310 §1.4.1](310_vsprototype_spec.md) 敵編成プール仕様を Lv 軸で再構成（プール × Lv × 地形追従 × 抽選）
- [Docs/310 §1.12.5](310_vsprototype_spec.md) ユニット強化を個別化＋ 3 択選択型に書き換え
- [Docs/310 §1.12.8](310_vsprototype_spec.md) / [§3.2](310_vsprototype_spec.md) メタ強化リスト：王女初期 ATK +3 を廃止、王女・ブリジット Lv 強化に置き換え

**次セッション実装ステップ**：

| Step | 内容 |
|---|---|
| 1（型整理）| `Unit.CurrentLevel` 廃止、`EnhancementLevel → Level` リネーム／`EnhancementXxxPerLevel` 4 フィールド廃止 → `List<UnitUpgradeDefinition>` に置換／`UnitUpgrade.ApplyEffect: Action<Unit>` を SO 化可能な POCO（`UpgradeKind` enum + `Magnitude` + `TargetWazaId`）に再設計／`RuntimeUnit.EffectiveXxx` を「累積 Upgrade 適用」方式に／既存テストの兵種強化関連箇所追従 |
| 2（兵種強化の個別化）| `VSPrototypeInteriorState._unitTypeEnhancementLevels` 廃止／`InteriorService.UpgradeUnit(Unit)` 個別化＋ 3 択選択 UI 改修／`CanUpgradeUnitType` の固有ユニット除外フラグ廃止／固有ユニットの強化アクションをメタ強化画面に追加 |
| 3（敵プール導入）| 各 Unit に強化選択肢 3 択を Roster で定義／敵生成時に `Level` セット → 順序番号 0,1 を順に適用／敵プール（弱／中／強）を `Dictionary<UnitId, Level>` の集合として定義／`VSPrototypeEnemyPatterns.CreateForNode` をプール抽選ロジック化（`IRandom` 注入で決定論テスト可能に） |
| 4（仕様書追従）| 実装過程での変更があった部分だけ [Docs/310](310_vsprototype_spec.md) を再修正（本セッションで現時点想定は記載済） |

**残った設計判断（実装時に詰める）**：
- 強化選択肢の具体的な 3 択内訳（剣士・タンク・アタッカー・支援役など兵種ごと）
- 敵プール（弱／中／強）の具体構成：出現ユニット ID × Lv の集合
- 地形追従の発動条件（火列なら火タンク 1 体必須など）の具体ルール
- 固有ユニットの Lv 強化メタ通貨コスト（仮置き 50）
- R6 中ボス／R7 ラスボスの Lv 設定（仮置き Lv1）

### 2026-06-17 Lv 軸統合設計 実装（C1〜C6 完了）

設計確定（2026-06-17 同日の前セッション）に従い、C1〜C5 で実装、C6 で仕様書追従。

| # | ハッシュ | 内容 |
|---|---|---|
| C1 | 588a83b | 旧 GrowthSystem/UnitUpgrade/UpgradeType/GameCycleTests を _old 退避。SO 化 5 層パターン新設（UpgradeKind / 新 UnitUpgrade / UnitUpgradeDefinition POCO / SO ラッパー / IUnitUpgradeCatalog / UnitUpgradeCatalog）。Unit/UnitDefinition/RuntimeUnit 型整理（CurrentLevel/EnhancementXxxPerLevel 廃止・Level に統合・AppliedUpgrades 累積方式）。UnitCatalog に IUnitUpgradeCatalog 注入 |
| C2 | fefa8b9 | UpgradeRoster 新設（仮 4 種：ATK +5 / DEF +3 / HP +20 / EVA +5）＋役割プリセット 3 種（Attacker / Tank / Support）。AlliesRoster 全 17 体に AvailableUpgradeIds 埋め。SoAssetGenerator に Upgrade SO 生成追加 |
| C3 | ad0ba29 | 兵種強化機構を個別ユニット Lv 強化に置換。InteriorState の `_unitTypeEnhancementLevels` 系廃止＋ `MaxUnitLevel = 3` 新設。InteriorService の `Can/ExecuteUpgradeUnit(unit, upgrade)` 個別化。InteriorGUI を「個別ユニット一覧 → 3 択カード」2 段構成に改修。InteriorServiceTests 新設（7 ケース） |
| C4 | f785c99 | VSPrototypeEnemyPool POCO 新設（弱／中／強の 3 プール仮構成） |
| C5 | aeb2464 | VSPrototypeEnemyPatterns をプール × Fisher–Yates 抽選＋ Lv 適用方式に置換。Bootstrap で `Func<int> rng` 注入追従。EnemyPatternsTests 新設（4 ケース） |
| C6 | （本コミット） | Docs/310 §1.4.1（プール構成表＋実装ファイルリンク）／§1.12.5（SO 化 5 層表＋ Upgrade 4 種表＋実装ファイルリンク）追従 |

**残タスク**：

- メタ強化画面での王女・ブリジット Lv 強化追加（C3 で「別フォローコミット予定」とした件）
- Phase R-6 バランス調整本体（プール構成・Upgrade 数値・地形追従ルールの実プレイ調整）

#### 2026-06-17 固有ユニットメタ強化追加（F1〜F6 完了）

Lv 軸統合設計（C1〜C6）で「別フォローコミット予定」とした件を実装。王女・ブリジットの Lv 強化をメタ拠点画面から購入可能にし、購入時に 3 択選択モーダルを表示する。

| # | ハッシュ | 内容 |
|---|---|---|
| F1 | 85c2f3a | MetaUpgradeIds の PrincessAtk 廃止・PrincessLevel/BridgetLevel 新設。MetaProgressState に AppliedUpgradeChoices（Dictionary<string, List<string>>）＋ ApplyUpgradeChoice/GetUpgradeChoices 追加。MetaProgressSerializer 追従。既存テスト 4 ファイル rename（cap 1→2 反映） |
| F2 | 92093b8 | MetaUpgradeRoster 新設（4 種ファクトリ）＋ SoAssetGenerator に CreateOrUpdateMetaUpgradeSo 追加。旧 meta_upgrade_princess_atk.asset 削除 |
| F3+F4 | 9ddd44b | MetaHubGUI：3 択モーダル DrawUpgradeChoiceModal 新設。TryPurchase を分岐（Princess/Bridget Lv → モーダル、その他即購入）。ブリジット未解禁時は購入ボタン disabled。Bootstrap：public IUnitCatalog プロパティ追加＋ ApplyMetaUpgradeChoices ヘルパで StartNewRunCore / StartNewRunWithDefaultRoster の固有ユニット生成直後に初期 Lv 反映 |
| F5 | 154839d | MetaProgressStateTests に ApplyUpgradeChoice 系 4 件追加。MetaProgressSerializerTests に往復 1 件＋旧スキーマ後方互換 1 件＋初期 JSON フォーマット追従 |
| F6 | （本コミット） | Docs/310 §3.2 メタ強化表を「✅ 実装済」化＋実装動線記述。Docs/912 に F1〜F6 のハッシュ表 |

**ユーザー作業（コンパイル確認後）**：

- Editor で `Echolos/Data/SO アセットを生成` メニュー実行 → MetaUpgrade SO 4 件生成
- メタ進行データをリセット（旧 `princess_atk` PlayerPrefs データを除去）
- Test Runner で All Tests 実行
- 試遊：メタ通貨を貯めて王女 Lv 強化を購入 → 3 択モーダル → 採用 → 次ラン開始時に王女が Lv2 で出てくることを確認

#### 試遊 FB 対応：ユニット強化リストのソート順を王国軍リストと共通化（Lv 降順を二次ソートに追加）

試遊で「強化リストの並びが入手順で見づらい」FB を受けて、王国軍リストと同じ DraftPool 順に統一。ロジック重複を避けるため共通ユーティリティに集約。

- [`UnitRosterSorter`](../Assets/Scripts/Presentation/Common/UnitRosterSorter.cs) 新設：`SortByPoolOrder(roster, draftPoolCatalog)` 静的メソッド。優先順は (1) 固有（王女 → ブリジット） (2) Normal index 順 (3) Rare index 順 (4) 該当なし末尾。**二次ソートは Unit.Level 降順**（Lv3 → Lv2 → Lv1）
- `VSPrototypeMapGUI.SortRosterForDisplay` private 関数を削除し、`UnitRosterSorter.SortByPoolOrder` に置換（王国軍リスト＋配置モーダル一覧の 2 箇所）
- `VSPrototypeInteriorGUI.DrawUpgradeList` も同関数経由でソート

#### 試遊 FB 対応：ユニット強化を同一ラウンド複数回可・最終ボタンだけ行動力 disabled

試遊で「強化が 1 回しかできない／行動力なくても選択画面までは見たい」FB を受けた対応。

- `VSPrototypeInteriorState.TryConsumeActionPoint()` 新設：行動力消費のみで実行履歴に記録しない（同一ラウンド複数回可）
- `VSPrototypeInteriorService.CanUpgradeUnit` から `CanExecuteAction` チェック削除 → `state.ActionPoints >= ActionCost` だけチェック。`ExecuteUpgradeUnit` は `MarkActionExecuted` → `TryConsumeActionPoint` に変更
- `VSPrototypeBootstrap.BeginUpgradeSubMode` から行動力チェック削除 → Phase 条件のみ。行動力 0 でも一覧画面まで進める
- `VSPrototypeMapGUI`：「ユニット強化」ボタンは常時 enabled に、「※ 強化は同一ラウンド1回まで」ヒント削除。ラベルを「ユニット強化（行動力 1／複数回可）」に
- `VSPrototypeInteriorGUI.DrawUpgradeChoiceCard`：「採用」ボタンに行動力チェック追加。0 なら disabled＋ラベル「行動力不足」
- `InteriorServiceTests`：「同一ラウンド 2 回目 false」テストを「同一ラウンド複数回成功」「実行履歴に記録しない」の 2 件に置換。既存「成功」テストのアサーションを `HasExecutedThisRound` 確認 → `ActionPoints == 1` 確認に変更

#### 試遊 FB 対応：強化の可視化＋実効値計算の集約

試遊で「強化が見えない」FB を受けて、`Unit` 側に実効値プロパティを集約し UI を更新（試遊 FB 起因のフォローコミット）。

- `Unit` に `EffectiveMaxHP / EffectiveATK / EffectiveDEF / EffectiveEvasion` 4 種＋ private `SumUpgrade` ヘルパを追加（実効値計算の SSoT）
- `RuntimeUnit` を `BaseUnit.EffectiveXxx` 参照に書き換え。`SumUpgradeMagnitude` 重複ロジックを削除
- `VSPrototypeMapGUI`：左ペイン王国軍リスト＋配置モーダル一覧に「Lv {Level}」を常時表示（Lv2 以上は金色強調 `_rosterLvBoostStyle`）。ステ表示を `EffectiveMaxHP` / `EffectiveATK` に変更
- `VSPrototypeInteriorGUI`：強化リストのステ表示を `EffectiveMaxHP` / `EffectiveATK` / `EffectiveDEF` に変更（Lv 表示は既存）
- 戦闘画面・戦闘ログには Lv 表示なし（戦闘画面は配置で陣営区別可能・ログは Unit.Name のみ）

### 2026-06-17 StatusEffect 階層再設計（β アプローチ・セッション 2 完了）

セッション 1 で Domain 層を新型 IEffect / EffectKind / Lifetime に移行済み（LegacyAdapter 経由で
旧 StatusEffect を併存）。本セッションで Data 層／テスト／Roster／旧 API 削除／SearingWound 実装まで
通し、旧 StatusEffect / StatusEffectType / BuffCategory / LegacyStatusEffectAdapter を完全削除。

**コミット（A〜G）**：
| # | ハッシュ | 内容 |
|---|---|---|
| A | dfb7c3f | Data 層 EffectDefinition POCO 新設＋ WazaDefinition/UnitDefinition/Catalog 移行＋ Roster 新ファクトリ置換 |
| B | 4cb6557 | SynergyBuff/SynergyDefinitions を EffectKind 直接化＋ SynergyApplier Adapter 撤去 |
| E | d9a23eb | テスト 21 ファイル＋ TestEff ヘルパ新設で新型 API 全置換 |
| F | 589c963 | 旧 StatusEffect / StatusEffectType / BuffCategory / LegacyStatusEffectAdapter / 旧 AddEffect 互換 API 完全削除＋ StatusEffectOverlay 新型化 |
| G | （本コミット） | SearingWound 割引ロジック実装＋焦熱波 RiderEffect 仕様化＋雑記更新 |

**新導入された API**：
- `Echolos.Domain.Effects.EffectFactory.CreateByKind(kind, magnitude)`：派生クラス生成の標準ファクトリ
- `Echolos.Domain.Effects.EffectDefinition`：SO シリアライズ可能 POCO（CreatePersistent / Triggered / Conditional / Cleansable）
- `Echolos.Tests.Domain.TestEff`：テスト用 IEffect 生成ヘルパ（Eff / Persistent / Triggered / Cleansable / Conditional）

**SearingWound 実装（Phase G）**：
- `HealEffect` 内で対象の `HealReceivedModifier` を集計し `最終回復 = 素回復 × max(0, 1 - Σ(Magnitude × Stacks)/100)`
- 焦熱波 RiderEffect を `CreateCleansable(SearingWound, magnitude:10, stacks:1, maxStacks:9)` に変更
- 旧 HealReceivedDown 30%/3T → 新 SearingWound 10%/Stack × 最大 9 Stack=最大-90%（永続蓄積）

**ユーザー作業依頼（コンパイル確認後）**：
- Unity Editor を開いて自動コンパイル状況を確認
- 残存エラーがあれば詳細を共有
- Editor の Test Runner で All Tests 実行
- `Echolos/Data/SO アセットを生成` メニュー実行（既存 SO の旧 StatusEffect フィールドを新 EffectDefinition 構造に再書き出し）

### 2026-06-17 StatusEffect 階層再設計（β アプローチ・セッション 1）

旧 5 分類 `BuffCategory` ＋ `IsAbilityDebuff` / `IsStatusAilment` ヘルパーによる「能力デバフ vs
状態異常」の二項対立を全面廃止し、振る舞いで型階層を分け持続/解除/識別子を独立フラグで管理する
構造に再設計。途中で出てきた SearingWound（熱傷）追加要求を契機に、根本設計のひずみ
（2 軸混在・グレーゾーン enum・仕様書とコードの乖離・ホワイトリスト対応の脆さ）を全部解決する
β アプローチを採用（[Docs/320 §4.7](320_vsprototype_combat_spec.md) 全面書き直し）。

**設計の要点**：
- 派生クラス＝振る舞いを表現（16 派生：`AbilityModifier` / `EvasionModifier` / `OutgoingDamageModifier`
  / `IncomingDamageModifier` / `CriticalRateModifier` / `CounterDamageModifier` /
  `HealReceivedModifier` / `ContinuousDot` / `ContinuousHot` / `FreezeEffect` / `ParalysisEffect`
  / `CurseEffect` / `ShieldEffect` / `SilencedCounterFlag` / `ReviveInvalidFlag` / `SelfGuard`）
- `EffectKind` enum＝個別効果の識別子（型と独立軸・1 型 N Kind）
- `Lifetime`（Triggered/Permanent 2 値）＝自然消滅の有無
- `IsUndispellable` / `IsCleansable` フラグ＝解除経路の許可（型とは独立）
- `Unit.ImmunityKinds: HashSet<EffectKind>`＝付与時の弾き（旧総称「状態異常無効」廃止）
- 「状態異常」概念そのものを廃止（Cleanse 対象 = `IsCleansable=true` で代替）

**コミット（c3f1e7c〜2148e2d）**：
- C1: `Domain/Effects/` namespace ＋ 13 派生クラス＋ `EffectKind` / `Lifetime` / `IEffect` / `EffectBase`
- C2: `Unit.ImmunityKinds` 追加（旧 `ImmuneToStatusAilments` は当面残す）
- C3: `LegacyStatusEffectAdapter`（旧 StatusEffect → 新 IEffect 変換）
- C4-C5: `RuntimeUnit` ＋ Domain 全消費側を IEffect 型に一括移行
  - StatusEffectProcessor / StatusEffectStacker / DamageFormula / ShieldConsumer /
    AttackEffect / Dispel・Cleanse / CounterAttackResolver / TargetEvaluator /
    SynergyApplier / BattleEvent / BattleEventRecorder / BattleLogFormatter
- HitOutcome.AppliedEffects / RemovedEffects を `IReadOnlyList<EffectKind>` に
- BattleEvent.EffectType → EffectKind プロパティ

**セッション 2 残作業**：
- テスト追従（21 ファイル・StatusEffectType ベース呼び出しを EffectKind に書き換え）
- Data 層 `RiderEffectDefinition` POCO 新設＋ `WazaCatalog` 変換
- `WazaRoster` 全 Waza を新型ファクトリに置換＋ SO 再生成
- 旧 `StatusEffect` / `StatusEffectType` / `BuffCategory` / `ImmuneToStatusAilments` / `LegacyStatusEffectAdapter` / 旧 AddEffect 互換 API の完全削除
- SearingWound 実装（HealEffect 内で HealReceivedModifier の Stacks × Magnitude % 割引）
- 焦熱波 RiderEffect を SearingWound に置換＋効果有効化に伴うバランス調整
- 全テスト確認

セッション 2 開始時は Unity Editor でコンパイル確認 → エラー修正してから着手。WazaRoster 移行と
並行してテスト追従（同じ enum を参照しているため連動修正が多い）。

### 2026-06-17 CD 持ち Waza を毎ターン発動化（バランス調整準備）

戦闘ロジック整備が一段落したので、Phase R-6 バランス調整本体に入る前段として、Waza の
発動頻度に関する暫定仕様（推測で入れていた CD 設定）を整理した。

旧仕様：4 Waza に CD が設定され、いずれも 2T に 1 回発動（焦熱波 CD=1 / 看破・浄化・聖光裁き
CD=2）。「Nターンおき発動」自体の仕組みは将来必要（ボス技・大技フェーズ等）なので残すが、
プロト範囲の基本 Waza には不要と判断。

| Waza | 旧 | 新 |
|---|---|---|
| 焦熱波 (PyroBlast) | CD=1, mult=0.8 | CD=0, mult=0.4 |
| 看破 (DispelAura) | CD=2 | CD=0（数値なし） |
| 浄化の癒し (PurifyHeal) | CD=2, InitialCD=1, wazaPower=3 | CD=0, InitialCD=0, wazaPower=1.5 |
| 聖光裁き (RadiantJudgment) | CD=2, InitialCD=1, mult=0.7 | CD=0, InitialCD=0, mult=0.35 |

毎ターン発動化に伴う数値半減で平均出力は同等を維持。`WazaPower` は wazaPower=1.5 等の
小数値を持たせるため `int → double` に型変更（HealEffect.cs は元から double 受け取り）。

PyroBlast の RiderEffect（HealReceivedDown 30/3T）は触らない。`StatusEffectStacker` が同種
効果に対し RemainingTurns/Magnitude を上書きするだけで Stacks 加算（MaxStacks=1）しないため、
毎ターン上書きでも効果量は変わらない（旧 CD=1 でも実質永続化していた）。

SO アセット反映：Unity Editor で `Echolos/Data/SO アセットを生成` メニュー実行が必要。

### 2026-06-17 光属性 HealOverTime ログ陣営単位 1 行集約

範囲攻撃・範囲バフが 1 行ログに集約されているのに対し、光属性のターン終了時 HP 割合回復は
per-unit で 1 行ずつ流れていた問題を解消（[Docs/320 §4.2](320_vsprototype_combat_spec.md)
ログ集約項追記）。

設計原則：**1 Event = 1 ログ**（[Docs/500 §3.6](500_architecture.md) 構造的 1:1 同期）。
範囲攻撃の `ActionResolved` と同じく、HealOverTime も 1 Event に全 unit ぶんの HP 更新＋
集約 LogLine を載せる。観戦ビューは Step モードで 1 cursor 進めると全 HP 一括反映＋ログ 1 行
表示になる。

実装：
- `StatusEffectProcessor` の旧 `OnHealOverTimeTick`（per-unit）を廃止し、新 `OnHealOverTimePhase`
  （`IReadOnlyList<HealOverTimeTick>` 集約）に差し替え。`HandleEndPhase` が unit ループ中に
  tick を蓄積→末尾で 1 回だけ Invoke
- `BattleEventKind.HealOverTimePhase` 新設＋ `BattleEvent.HealTicks` プロパティ追加
- `BattleEventRecorder` は集約 1 件の `HealOverTimePhase` Event を AddEvent（HealTicks に全件＋
  LogLine に集約行）。per-unit Event は作らない
- `BattleLogFormatter.FormatHealOverTimePhaseLine` 新設：「✚ 味方全体に 光の共鳴 LvN：継続回復
  +X/+Y/...」形式。対象表記は陣営全員一致なら「味方全体／敵全体」、部分なら「味方N体」、1 体なら個別名
- `VSPrototypeBattleGUI.ApplyEvent` に `HealOverTimePhase` 分岐追加：HealTicks 全件の HP を
  一括反映
- SourceAbilityName は HealOverTime を持つ effect のうち最初に設定されたものを採用
  （SynergyApplier が「光の共鳴 LvN」を埋めている）

### 2026-06-18 試遊バランス調整＋戦線システムの構造リファクタ

【スコープ】前 compact から R6 まで遊んだ試遊観察を起点に、3d 本拠地戦判定バグ／ R2 自領敵編成消失バグ／戦線概念の構造課題などを解消しつつ、敵プール体数・Lv・司祭/弓の数値調整を順次実施。BattleMode 一級概念導入で戦線システムの暗黙副作用を構造的に防止し、本拠地戦予告 UI／本拠地配置 carryOver／2 パス解決まで戦線まわりを整理した。

#### バランス調整

| # | 論点 | 内容 | コミット |
|---|---|---|---|
| 1 | 弱プール体数 | 3→2 抽選（プレイヤー初期手札と数を揃える） | `4568202`＋`eff03fb` |
| 2 | 本拠地戦体数 | R 帯別＝自領防衛と完全同体数（旧 5 体固定廃止） | `cd7ffd0` |
| 3 | 反撃／司祭／弓（A1+B1+C1） | DefaultCounter mult 0.5／lesser_heal WazaPower 3／fire_archer ATK 30 | `2d57582`＋ SO `c813fe7` |
| 4 | 中プール Lv | 全エントリ Lv1 | `0dea8e6` |
| 5 | 光の司祭 HP/DEF | 90→60／3→1（HealEffect 連鎖で回復量も自動低下） | `bfbbe29`＋ SO `d132a81` |
| 6 | R6 自領防衛体数 | 3→5（敵拠点と完全一致） | `24ad340` |
| 7 | 強プール Lv | 全エントリ Lv1（試遊調整中・Lv 上昇は後で再検討） | `7dbddd7`＋テスト `eddf50c` |

#### 構造リファクタ

| 論点 | 内容 | コミット |
|---|---|---|
| 3d 本拠地戦発火判定 | ラウンド中陥落は次ラウンドから猶予（fallenAtRoundStart スナップショット） | `5de6a58` |
| R2 自領敵編成消失バグ調査 | 一時 Debug.Log 仕込み | `0235255`＋`cc07e0e` |
| **BattleMode 一級概念導入** | MapNode.BattleMode（None/Defense/Attack/Boss）追加。ResolveAllBattles は BattleMode!=None フィルタ／ApplyNodeOutcome は switch で宣言的分岐。敵編成 0 マスの自動勝利→誤 Capture を構造的に防止 | `365d554` |
| 本拠地戦予告 UI | StartRound で陥落自領>0 なら本拠地に予告編成セット＋ BattleMode=Defense。ResolveHomeBattle は予告編成をそのまま使用（決定論性保証） | `0034474`＋テスト `277b495` |
| 配置の継続性ルール改定 | 戦闘不能でも配置維持／敗北占領マスはマスから外して Roster 戻し（最初の実装）→ Roster 二重登録バグ発見 → HP 回復タイミング統一（戦闘終了時 Roster 全員一括）→ 局所回復全廃 | `b1059eb`→`5deb228`→`c2b7e07` |
| 本拠地配置 carryOver | CaptureCarryOverPlacements の Home スキップ撤去。本拠地戦予告に備えた配置を毎ラウンド再入力しなくていい | `eae91c6` |
| **2 パス解決** | ResolveAllBattles を Pass1 Attack→ Pass1.5 敵領占領で同列の自領防衛不発化→ Pass2 Defense に分割。「敵領を占領した列の自領防衛戦が同ラウンドで湧く」事象を解消 | `fe4b7ea` |
| Pass1.5 拡張：敵拠点占領分 | 敵拠点を攻略した列の「占領済み敵領の奪還戦」も Pass1.5 で不発化。`CancelDefenseIfTerritoryCaptured` → `CancelObsoleteDefense` に改名（敵領／敵拠点いずれの攻め込み成功でも前線後退で無効になる Defense を一括キャンセル） | `6e5fe7f` |
| 戦闘 UI ステータスリスト並び順 | `DrawTeamStatusList` で `SlotIndex` 昇順表示に変更（lineup は配置操作順そのままだった） | `0796d97` |
| 戦闘 UI 状態異常バッジ位置 | 左下／右下 → 左上／右上 に変更。`iconRect` 上端から +50px のやや下寄りで前列のはみ出し対策 | `fa0e1a2` |
| 戦略マップ拠点アイコン所属切替 | `NodeIconKey` を所属ベース（自領陥落＝敵領画像／敵領占領＝自領画像／敵拠点占領＝`node_friendly_stronghold` 新規）に改修。陥落自領の彩度低下は廃止。`node_friendly_stronghold` 画像アセットは後で投入（未配置時はマス透明） | `f1dbf4f` |
| バルドゥイン解放フラグ整理＋発火タイミング移設 | (a) R2/R3 ラウンド開始演出（BalduinIntro / BalduinLetter）の判定に `!IsBridgetRescued` 追加：本ラン先行解放後に救援の手紙イベントが流れる矛盾を解消。 (b) `BalduinRescue` 演出を「R5 終了固定」から「解放したラウンドの終了直後」に移設。`IsBalduinRescuePlayed` フラグで本ラン 1 回のみ再生制御。R5 までならどのラウンドで解放しても直後に演出 | `2e03b6e` |
| ブリジットのラン内加入 | B-d 演出完了直後に `_roster` へブリジットを追加（永続解禁＝次ラン以降だけでなく、解放した周回中から配置可能に）。`AddBridgetToRosterIfAbsent` で Id 重複ガード（永続解禁ランは構造的に B-d 発火しないが防御） | `f787cb6` |
| 占領済み敵拠点の配置可化 | `CanAssign` の敵拠点分岐から `!IsCaptured` 縛りを除去。占領後（バルドゥイン拠点解放含む）も配置モーダルを開けて配置済みユニットの解除／再配置ができるように。戦闘は発生しない（BattleMode=None） | `b0a2516` |
| R6 PendantNote 当該ラン救出対応 | R6 開始時のペンダント気づきイベント判定に `IsBridgetRescued` を OR 追加。当該ラン中に R5 までに救出した周回でも PendantNote → SwordEmpowered 連鎖が走り、R7 で A-c2 経路（聖剣強化済＝皇太子撃破可能）に到達できる。「ブリジットを救った周回で同時にクリア」が成立 | `788721f`＋テスト棚卸し `2c4a6e7` |
| ストーリー既見短縮システム | 永続フラグ `MetaProgressState.SeenStorySceneIds` 追加（Serializer round-trip／後方互換）／ `StoryScene.RepeatNarration` フィールド追加／ Bootstrap に `BeginStorySceneById` ヘルパ実装し既存 5 経路（Opening / RoundStart 通常 / PendantNote / SwordEmpowered / BalduinRescue）を置換。エンディング系 4 件は対象外。Editor メニュー「短縮ナレーション既定値を書き込み」で 10 シーン分の暫定ナレを SO に流し込み（ユーザーが Inspector で校正） | `ba0550a` |
| メタ拠点 UI 背景画像対応 | `MetaHubGUI.OnGUI` 冒頭で `BackgroundRegistry.TryDrawCover(rect, "meta_hub_background")` 試行＋失敗時 `ColorBg` 単色塗りフォールバック。中央パネルを半透明（α=0.88 仮置き）にして拠点の雰囲気を出す。画像は別途配置 | `d684393` |
| メタ強化コスト段階別化＋ UI 整理 | (a) `MetaUpgrade.Cost` (int) → `Costs` (IReadOnlyList&lt;int&gt;) ＋ `GetCostForNextLevel(curLv)` ヘルパに刷新。コスト＝王女/ブリジット Lv 強化 30→60、行動力 +1 100、初期ユニット +1 50→80→100。 (b) 解禁ユニット表示欄削除（縦長対策）。 (c) ブリジット未解禁時は Lv 強化行を「ブリジット : 未解禁」テキスト 1 行に差替。 SO 再生成必要（MetaUpgradeDefinitionSO のフィールド構造変更） | `0392b44` |
| メタ強化 表示順＆未解禁表記の微調整 | (a) 表示順を MetaHubGUI 側の `UpgradeDisplayOrder` 定数配列で明示制御し、行動力→初期ユニット→王女→ブリジットに並べ替え（汎用強化→個別ユニット Lv 強化の順／Resources.LoadAll は順序保証ナシ→ GUI 責務で順序決定）。 (b) ブリジット未解禁差替行のテキストを「ブリジット : 未解禁」のみに（解禁条件説明はメニューに書かずゲーム内チュートリアル／ゲーム外ガイドに委ねる方針） | `5ae5d97` |
| タイトル画面実装 | Phase 先頭に `Title` 追加＋ Bootstrap.Awake 起点を Title に変更＋ `StartFromTitle()` ヘルパ（セーブ有無で Hub vs StartNewRun 分岐）＋ `MetaProgressStore.HasSaveData()` API ＋ `VSPrototypeTitleGUI` 新規 MonoBehaviour（背景画像 `title_background` ＋タイトル文字「Echoes of the Lost Kingdom」＋「ゲームスタート」ボタン）。初回プレイ判定はセーブファイル存在ベース | `7bc4bac` |
| SO 再生成＋シーン更新（アセット反映） | MetaUpgrades 4 件を Costs 構造に再生成／StoryScenes 10 件に Migrator で短縮ナレ流し込み／EcholosProto_VS シーンに TitleGUI コンポーネントを Bootstrap GameObject にアタッチ済 | `588c9c2` |
| Debug_StoryViewer GUI 実装 | `Presentation/DevTools/StoryViewerGUI.cs` 新規＋ `Bootstrap.DevPlayStoryScene(id, treatAsSeen)` ／ `DevResetProgressForStoryViewer()` ／ `StorySceneCatalog` 公開 API 追加。Phase=Title 中に全 14 シーンの「初見再生／既見再生」ボタンと「セーブ＋既見フラグリセット」を提供。シーン本体は EcholosProto_VS を Save As で `Debug_StoryViewer.unity` を作成し VSPrototypeManager に StoryViewerGUI コンポーネントを Add してもらう運用 | `66e6a9d` |
| StoryViewer 初見再生バグ修正 | `DevPlayStoryScene` を `BeginStorySceneById` 経由から外し、`treatAsSeen` フラグだけで本文／短縮を分岐させる直接構築に。Meta フラグの読み書き両方を抑止＝連続「初見再生」「既見再生」で Meta を汚さない | `b2b460d` |
| タイトル文字を背景画像時に非表示 | `BackgroundRegistry.TryDrawCover` の成否を保存し、画像あり時はゲーム名テキスト描画をスキップ（ロゴと二重表示にならない）。画像なし時のフォールバックでのみ「Echoes of the Lost Kingdom」中央表示 | `4686676` |
| Defeat 演出強化＋ Debug シーン投入（ユーザー作業） | NormalClear/Repeated に神殿送り返し 6 ページ構成／True エンドの 2 段目に魔王影付き scene_capital_hug_with_dark／オープニング微調整／Debug_StoryViewer.unity を VSPrototypeManager+StoryViewerGUI 構成で投入 | `4686676` ／ `63951dd` |

#### バグ修正・ユーティリティ

| 論点 | コミット |
|---|---|
| テスト 9 ファイルの重複 using 一括撤去 | `4f587f7` |
| MetaHubGUI セーブリセットボタン追加 | `ebd34a6` |
| MetaHubGUI NRE（強化選択モーダル購入後の残カード描画） | `d5ebeba` |

#### 設計判断ログ

- **HP の扱い**：VSプロトは「戦闘間 HP 引き継ぎは仕様外＝戦闘中以外は常に HP=MaxHP」が原則。`Bootstrap.ResolveCurrentRound` 末尾で Roster 全員一括 `CurrentHP=EffectiveMaxHP` リセット。`RuntimeUnit.CurrentHP` は `BaseUnit.CurrentHP` の proxy なので、Roster の Unit を回復すれば配置中の RuntimeUnit も同時に回復される（同一インスタンス参照）。Domain は Roster を知らない＝ Presentation 一括処理で吸収
- **戦線概念の課題**：「攻め→ 防衛」を明示的にフェーズ分割するのが本来の筋だが、プロトでは 2 パス解決で内部実装的に吸収。フル版で明示フェーズ化を再検討
- **BattleMode 設計判断**：「妥協＝メリット薄い修正」リスクを自己レビューしたうえで Y 案（BattleMode）を採用。HadEnemies フラグだけの応急処置（X 案）は同種バグの再発温床になるため却下

#### 設計判断ログ（追記）

- **バルドゥイン解放まわりの B 案採用**：フラグ漏れ修正（R2/R3 抑制）に加えて、BalduinRescue 演出を「R5 終了固定」から「解放したラウンドの終了直後」に移設＋ラン内 `IsBalduinRescuePlayed` で 1 回再生制御。R5 までならどの周回で解放してもその場で演出 → 同周回内クリアパス（R6 PendantNote → SwordEmpowered → R7 A-c2）が成立
- **ストーリー既見短縮：エンディング系除外**：Defeat 3 種・True エンディングはそれ自体がランの結末演出なので毎回フル本文。短縮対象は Opening + B 系列 5 + A-c1/c2 の 10 シーン
- **既見短縮フォールバック**：`RepeatNarration` 未設定 SO は警告ログ＋通常本文を再生（誤って通り過ぎるよりログで気付かせる方針）
- **メタ強化コスト段階別化**：`Cost` (int) → `Costs` (List&lt;int&gt;) ＋ `GetCostForNextLevel(curLv)` でアクセス API 統一。同一インスタンスの値オブジェクトのまま段階別を表現＝呼び出し側が現 Lv を渡すだけ
- **タイトル画面分岐：セーブ存在ベース**：「初回プレイ＝Hub スキップ」判定は `Meta.RunCount==0` ではなく `MetaProgressStore.HasSaveData()` を採用（AbandonRunAndReturnToHub 等で RunCount=0 のままセーブが残るケースもあるため、セーブファイル存在で純粋に判定）

#### 残課題

> 2026-06-19 全項目消化済み（バランス調整一旦完了／ストーリースチル＋短縮ナレ完了／戦略マップ拠点アイコン投入完了）。
> 以降の VSプロト残タスクは §1 に再集約済み。