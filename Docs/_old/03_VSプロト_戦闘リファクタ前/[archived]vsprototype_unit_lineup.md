# Echolos VSプロト ユニットラインナップ

> **【アーカイブ通知】2026-06-16**
> VSプロトのユニット一覧などの情報。
> バランス調整中に戦闘が面白くないという結論になり、戦闘の大幅リファクタリングを決断。それによりユニットも大幅刷新になったため本書も凍結。
> 記録として残すが参照はしないこと。

> 本書はユニット 28 体（味方 15／敵 13）の Id・基本ステータス・性能の **Single Source of Truth**（二重ドリフト防止・[900 §8.4](900_development_rules.md)）。
> 数値は [AlliesRoster.cs](../Assets/Scripts/Domain/Prototype/AlliesRoster.cs) ／ [EnemiesRoster.cs](../Assets/Scripts/Domain/Prototype/EnemiesRoster.cs) ／ [BossesRoster.cs](../Assets/Scripts/Domain/Prototype/BossesRoster.cs) の実装を反映。
> バランス調整対象の最終確定値は調整フェーズで決定する前提（本書では仮置きを明示）。

---

## 0. 全体構成

| 区分 | 体数 | 構成 |
|---|---|---|
| 味方固有（固定加入） | 2 | 王女／ブリジット |
| 味方汎用 Normal（ドラフト対象） | 9 | 重装兵／騎士／双剣士／大槌兵／傭兵／弓兵／炎魔導士／司祭／踊り子 |
| 味方汎用 Rare（ドラフト対象） | 5 | サムライ／忍者／軍師／雷魔導士／巫女 |
| 敵ボス（中ボス＋ラスボス） | 4 | どくどく男爵／隻眼のサムライ／皇太子（闇）／皇太子（浄化） |
| 敵汎用 | 11 | 帝国偵察兵／帝国の影／帝国軍 9 体（味方コピー） |
| **合計** | **31** | |

---

# 第1部：ユニット仕様

各区分ごとに「基本ステータス表」と「ユニット詳細」を並べる。詳細セクションは「キャラ説明＋役割＋技＋兵種強化」の構成。

---

## 1. 味方固有（2 体）

固定加入。ドラフト対象外＋兵種強化対象外（固有キャラ）。

### 1.1 基本ステータス

| 表示名 | Id | HP | ATK | PDEF | MDEF | SPD | Range | Element |
|---|---|---|---|---|---|---|---|---|
| 王女 | `princess` | 170 | 28 | 12 | 14 | 8 | Melee | Light |
| ブリジット | `bridget` | 150 | 26 | 10 | 12 | 9 | Melee | Light |

### 1.2 ユニット詳細

#### 王女（princess）

亡国の王女。初期ユニット。常時発動の全体防御バフを持った前衛アタッカー。

**役割**：近接物理アタッカー、全体補助（防御バフ）

**技**：
- 聖剣の一閃（敵単体への物理攻撃）
- 王家の加護：常在型バフ。全味方 PDEF/MDEF +5 。

#### ブリジット（bridget）

伝説の騎士の孫娘。イベントにより加入。その後、メタ強化により初期加入。純・前衛アタッカー。王女とのシナジー。

**役割**：近接物理アタッカー、メイン火力（バフ先）

**技**：
- 大剣の一撃：敵単体への物理攻撃
- 王家のペンダント：条件型バフ。味方に王女がいる場合、自身と王女に PDEF +3
- 攻防一体：条件型バフ。自身のPDEFにバフがかかっている場合、バフ量と同じ数値分 ATK が上昇。（PDEFデバフを受けてもATKは減少しない）

---

## 2. 味方汎用 Normal（9 体）

ドラフト対象の通常枠。前衛 5＋後衛 4。雷魔導士・巫女は性能の特殊性／全体回復の強さを理由に Rare 移動済（§3）。

### 2.1 基本ステータス

| 表示名 | Id | HP | ATK | PDEF | MDEF | SPD | Range | Element | Tags |
|---|---|---|---|---|---|---|---|---|---|
| 重装兵 | `tank_def` | 240 | 0 | 12 | 8 | 5 | Melee | Earth | RowCover |
| 騎士 | `paladin` | 190 | 26 | 10 | 14 | 7 | Melee | Light | RowCover |
| 双剣士 | `atk_multi` | 115 | 36 | 4 | 4 | 9 | Melee | None | — |
| 大槌兵 | `debuffer` | 130 | 32 | 5 | 4 | 8 | Melee | None | — |
| 傭兵 | `mercenary` | 130 | 34 | 5 | 4 | 8 | Melee | None | Loner |
| 弓兵 | `archer` | 100 | 34 | 3 | 3 | 10 | Ranged | Wind | — |
| 炎魔導士 | `firemage` | 80 | 36 | 2 | 8 | 8 | Mid | Fire | MageRole |
| 司祭 | `healer` | 50 | 0 | 3 | 6 | 9 | Ranged | Light | — |
| 踊り子 | `buffer` | 85 | 0 | 3 | 5 | 8 | Ranged | None | — |

### 2.2 ユニット詳細

#### 重装兵（tank_def）

重い鎧と大盾で身を固めた歩兵。攻撃せず同列の仲間を守るのに特化。物理防御が高く、魔法防御が低め。

**役割**：タンク（かばう特化）

**技**：
- 攻撃技なし（自己防御フォールバックで毎ターン「防御」を発動）
- かばう：同列の後衛が狙われた攻撃を肩代わり（RowCoverTag）

**兵種強化**：PDEF +3 / 段階

#### 騎士（paladin）

剣と盾を構えた王国正規兵。同列の仲間を守りつつ自身も殴れる前衛。重装兵より低い物理防御/HPと、少し高い魔法防御。純アタッカーには大きく劣る攻撃力。

**役割**：タンク 兼 近接物理サブアタッカー

**技**：
- 剣撃（敵単体への物理攻撃）
- かばう：同列の後衛が狙われた攻撃を肩代わり（RowCoverTag）

**兵種強化**：PDEF +2 / MDEF +2 / 段階

#### 双剣士（atk_multi）

二刀流の前衛アタッカー。2回攻撃のため、ATKバフの効果を強く受けるデザイン。前衛の中で特に耐久力が低い。

**役割**：近接物理アタッカー、メイン火力（バフ先）

**技**：
- 連撃（敵単体への 2 回物理攻撃／1 撃あたりの威力は控えめ）

**兵種強化**：ATK +5 / 段階

#### 大槌兵（debuffer）

大型のハンマーを振るう前衛兵。敵の鎧を砕き、後続の火力を引き上げる。純アタッカーより火力低め、耐久高め。

**役割**：近接物理サブアタッカー 兼 防御デバフ、タンク対策

**技**：
- 鎧砕き（敵単体への物理攻撃 mult 1.0 ＋防御力↓ −7 を蓄積／1 スタック 5 ターン持続・最大 6 スタック）

**兵種強化**：ATK +3 / PDEF +2 / 段階

#### 傭兵（mercenary）

大剣を振りかざす荒々しい前衛アタッカー。味方が少数のときに有効な序盤型ユニットとしてデザイン。

**役割**：近接物理アタッカー、序盤向け

**技**：
- 大剣の薙ぎ（敵単体への物理攻撃）
- 孤高の戦士：条件型の自己バフ。陣営の生存数が自分含めて 3 体以下のとき、自身の ATK／PDEF にバフを獲得。ATK は 3 体→+10／2 体→+20／1 体→+30、PDEF は ATK の半分（3 体→+5／2 体→+10／1 体→+15）。人数が減るほど効果量が大きくなる。編成時点で 3 体以下でも発動する。

**兵種強化**：ATK +5 / 段階

#### 弓兵（archer）

後衛から弓を引く遊撃兵。必中スキルにより回避を無効化する。後衛の中では耐久力が高い。

**役割**：遠隔物理アタッカー、回避対策

**技**：
- 狙撃（敵単体への物理攻撃／必中＝回避無効）

**兵種強化**：ATK +5 / 段階

#### 炎魔導士（firemage）

攻撃魔法を操る魔導士。前列の物理タンクを焼き払う。

**役割**：後衛、魔法アタッカー（前列単体）、継続ダメージ（燃焼付与）、物理タンクキラー

**技**：
- 火炎弾（中射程・敵前列単体への魔法攻撃／命中時に燃焼を 2 スタックずつ蓄積・最大 12 スタック）

**兵種強化**：ATK +5 / 段階

#### 司祭（healer）

教会所属の癒し手。神聖魔法で味方を回復する。

**役割**：後衛、回復役（単体）

**技**：
- 治癒の光（最も HP 割合の低い味方を中量回復・約 35）
- 防御（次の自分の行動順まで自身の防御微バフ／HPの減った味方がいない場合の予備技）

**兵種強化**：回復量 +10 / 段階

#### 踊り子（buffer）

戦場で舞い、味方を鼓舞する支援兵。

**役割**：後衛、攻撃力バフ（単体）

**技**：
- 鼓舞（味方単体に AttackUp 3 ターン・最大 3 スタック）　最も攻撃力の高い味方を対象にする
- 防御（次の自分の行動順まで自身の防御微バフ／バフ対象がいない状況で選ばれる予備技）

**兵種強化**：バフ強度 +3 / 段階

---

## 3. 味方汎用 Rare（5 体）

ドラフト召集の Rare 枠（既定 15% × 3 枠の独立抽選 + ★全 Rare スペシャル 3%）で出現。Normal より一段強い性能や尖った仕掛けを持つ。

### 3.1 基本ステータス

| 表示名 | Id | HP | ATK | PDEF | MDEF | SPD | Range | Element | Tags |
|---|---|---|---|---|---|---|---|---|---|
| サムライ | `samurai` | 130 | 36 | 4 | 4 | 7 | Melee | None | — |
| 忍者 | `ninja` | 105 | 42 | 3 | 3 | 16 | Melee | Dark | MageHunter / Infiltrator |
| 軍師 | `tactician` | 80 | 0 | 4 | 6 | 7 | Ranged | Dark | — |
| 雷魔導士 | `aoemage` | 78 | 36 | 2 | 6 | 6 | Ranged | None | MageRole |
| 巫女 | `medic` | 50 | 0 | 3 | 6 | 9 | Ranged | Light | — |

### 3.2 ユニット詳細

#### サムライ（samurai）

和装の侍。広い太刀筋で同列の敵を巻き込む。

**役割**：近接アタッカー（同列スプラッシュによる範囲ダメージ）

**技**：
- 薙ぎ払い（通常のターゲットロジックで選んだメインターゲットに加え、同じ横列にいる敵に 0.8 倍のスプラッシュダメージ。スプラッシュダメージにはかばうは発動しない。）

**兵種強化**：ATK +5 / 段階

**戦術的含意**：かばう持ちタンクがいるとメインターゲットがタンクに移り、タンクにメインダメージ＋同列の敵に 0.8 倍のスプラッシュダメージを与える。「タンク＋脆い前列アタッカー」の陣形に強く、後衛には届かない設計。

#### 忍者（ninja）

忍術と暗器を駆使する潜入暗殺者。敵陣に潜り込んで「とどめを刺せそうな相手」を冷静に狩る。魔導士キラーとして設計。

**役割**：特殊アタッカー（潜入暗殺者／魔導士キラー）

**技**：
- 疾風刃（敵単体への物理攻撃／かばう無視＋列単位保護無視（つまりすべての敵を常に攻撃可能））
- MageHunter タグ：敵魔導士（MageRole 持ち）には基礎ダメージ ×2
- **Infiltrator タグ**：自身は味方タンクのかばう対象外（敵陣に潜るため自陣からは守られない）
- 回避 5%

**兵種強化**：ATK +5 / 段階

**戦術的含意**：
- 「後衛タンクで魔導士を守る」構成への明確なアンチ。
- 忍者自身は近接扱いのため前衛に配置する必要があり、なおかつ自陣タンクに守られない → 敵から狙われやすい立場でもある弱点を抱える
- 攻撃力は控えめ調整想定（魔導士 2-3 撃／非魔導士後衛 4-5 撃で倒す程度）：魔導士の即死は避け、「逆に忍者を倒す猶予」を残す。
- ターゲットロジックで「とどめ刺せる相手 > 魔導士 > その他」が自然に選ばれる意図。

#### 軍師（tactician）

戦場の指揮役。攻撃せず、敵バフ／味方デバフの解除で戦況を整え、味方全体をわずかに強化する。

**役割**：後衛補助・全軍バフ＋解除役

**技**：
- 戦術隊形：常在型バフ。全味方へ AttackUp（攻撃力+5）を付与
- 看破（奇数ターンに敵全体のバフ解除）
- 戦線整理（偶数ターンに味方全体のデバフ解除）

**兵種強化**：バフ強度 +2 / 段階

#### 雷魔導士（aoemage）

雷魔法を操る魔導士。詠唱時間が必要だが、敵全体を巻き込む強力な落雷を発生させる。

**役割**：全体魔法アタッカー（詠唱あり）

**技**：
- サンダー（2ターン詠唱後、敵全体へ魔法攻撃）
- 詠唱（サンダー以外のターンに発動する「何もしない」アクション／ログ上は「詠唱」と表示）

**兵種強化**：ATK +5 / 段階

#### 巫女（medic）

神に仕える巫女。攻撃は行わず、全体小回復で味方を支える。

**役割**：後衛回復役・全体回復

**技**：
- 祈り（毎ターン、味方全体を小回復）

**兵種強化**：回復量 +2 / 段階

---

## 4. 敵ボス（4 体）

中ボス 2 体（6R に自領のどこかへ襲来）＋ラスボス 2 体（R7・経路分岐で出現体が変わる）。取り巻きは帝国軍ユニットで構成（味方ユニットを敵側に混在させない方針）。

### 4.1 基本ステータス

| 表示名 | Id | HP | ATK | PDEF | MDEF | SPD | Range | Element | Tags | 出現 |
|---|---|---|---|---|---|---|---|---|---|---|
| どくどく男爵 | `boss_baron` | 200 | 24 | 6 | 8 | 6 | Ranged | Dark | AntiHealPassive | 6R 中ボス |
| 隻眼のサムライ | `boss_one_eyed_samurai` | 240 | 34 | 10 | 6 | 8 | Melee | None | 状態異常無効 | 6R 中ボス |
| 皇太子（闇） | `boss_prince_dark` | 9999 | 30 | 999 | 999 | 10 | Melee | Dark | 状態異常無効 | R7 ラスボス（A-c1 必敗） |
| 皇太子（浄化） | `boss_prince_light` | 400 | 40 | 12 | 12 | 10 | Melee | Light | 状態異常無効 | R7 ラスボス（A-c2 戦える） |

### 4.2 ユニット詳細

#### どくどく男爵（boss_baron）

毒を操るギミックボス。全体に毒を蒔きつつ、味方の回復を強制的に弱体化させる。

**役割**：耐久＋全体毒（回復封じ）

**技**：
- 毒霧（ほぼ毎ターン・敵全体へ弱い闇属性魔法ダメージ＋燃焼（毒）を 1 スタック蓄積／最大 5 スタック・永続）
- 瘴気弾（毒霧以外のターンに発動する敵単体への弱い闇属性魔法）
- AntiHealPassive：生存中、味方（プレイヤー側）が受ける回復を 1/3 に減衰

**想定取り巻き**：後列の毒撒き＋耐久壁

#### 隻眼のサムライ（boss_one_eyed_samurai）

隻眼の侍剣士。連撃と前列薙ぎで一気に押し切ってくる前衛型ボス。

**役割**：高火力前衛

**技**：
- 三段斬り（毎ターン通常時／敵単体への 3 回物理攻撃／防御 50% 無視）
- 真・薙ぎ払い（3 ターンごとに優先発動／敵前列範囲を物理攻撃／防御 50% 無視）
- 状態異常無効

**想定取り巻き**：前列の物理高火力＋後列の遠隔

#### 皇太子（闇）（boss_prince_dark）

闇に染まった皇太子。漆黒の愛馬にまたがりランスを構え、目は闇に染まり、全身に闇のオーラをまとう。**戦っても絶対に勝てない設計**：物理／魔法防御が実質無限（999）で、超絶バフで一気に終わらせる。

**役割**：R7 必敗ボス（A-c1 経路）

**技**：
- 闇槍の薙ぎ（毎ターン／敵全体への物理攻撃）
- 闇のオーラ（毎ターン全体攻撃しつつ自己 ATK が `floor(currentTurn / 3) × 15` で青天井に蓄積／**バフ除去で打ち消されない**・実体は §8.6 PrinceDarkAuraConditionalProcessor）
- 状態異常無効

**想定取り巻き**：前列タンク 1＋帝国の精鋭 4（取り巻き詳細は Step 5-7 マター）

#### 皇太子（浄化）（boss_prince_light）

闇が祓われた皇太子。漆黒の愛馬にまたがりランスを構えるが、目は正気を取り戻し、正々堂々とした騎士の姿。**全ボス中最強性能**。3 行動サイクル（鼓舞→破邪の一撃→審判）を CD3 互い違いで周回する。

**役割**：R7 最強ボス（A-c2 経路）

**技**：
- 鼓舞（CD3・初期 CD0／味方全体に AttackUp +20・3T 持続）
- 破邪の一撃（CD3・初期 CD1／敵全体への物理攻撃（mult 0.7）＋敵全体のバフ解除＋防御デバフ -30・2T 持続）
- 審判（CD3・初期 CD2／敵全体への物理攻撃（mult 1.2））
- 状態異常無効

**想定取り巻き**：6 人編成（中身は Step 5-7 マター・暫定で帝国軍精鋭を仮置き）

---

## 5. 敵汎用（11 体）

### 5.1 基本ステータス

帝国軍 9 体（imperial_tank_def 以下）は対応する味方ユニットと**性能完全同一**。数値の独立調整は別途バランス調整時に実施する前提（味方／敵の数値を独立に動かせるよう Id を分離している）。

| 表示名 | Id | HP | ATK | PDEF | MDEF | SPD | Range | Element | コピー元 |
|---|---|---|---|---|---|---|---|---|---|
| 帝国偵察兵 | `imperial_scout` | 50 | 20 | 3 | 3 | 7 | Mid | None | （独自・敵専用） |
| 帝国の影 | `imperial_shadow` | 60 | 22 | 3 | 3 | 14 | Mid | None | （独自・敵専用／回避 15%） |
| 帝国重装兵 | `imperial_tank_def` | 220 | 0 | 10 | 8 | 5 | Melee | Earth | 重装兵（HP/PDEF 調整） |
| 帝国騎士 | `imperial_paladin` | 190 | 26 | 10 | 14 | 7 | Melee | Light | 騎士 |
| 帝国双剣士 | `imperial_atk_multi` | 115 | 36 | 4 | 4 | 9 | Melee | None | 双剣士 |
| 帝国傭兵 | `imperial_samurai` | 130 | 36 | 4 | 4 | 7 | Melee | None | サムライ |
| 帝国暗殺者 | `imperial_assassin` | 105 | 42 | 3 | 3 | 16 | Melee | Dark | 忍者 |
| 帝国弓兵 | `imperial_archer` | 100 | 34 | 3 | 3 | 10 | Ranged | Wind | 弓兵 |
| 帝国炎魔導士 | `imperial_firemage` | 80 | 36 | 2 | 8 | 8 | Mid | Fire | 炎魔導士 |
| 帝国大魔導士 | `imperial_aoemage` | 78 | 36 | 2 | 6 | 6 | Ranged | None | 雷魔導士 |
| 帝国司祭 | `imperial_healer` | 50 | 0 | 3 | 6 | 9 | Ranged | Light | 司祭 |

### 5.2 ユニット詳細

#### 帝国偵察兵（imperial_scout）

帝国軍の偵察任務に就く軽装歩兵。

**役割**：序盤の弱パターン向け雑魚

**技**：
- 短槍突き（中射程・敵単体への物理攻撃／mult 1.0）

「数の有利不利を直感的に体感させる基準ユニット」として配置（偵察兵 2 体 vs 双剣士 1 体は双剣士勝ち／偵察兵 3 体 vs 双剣士 1 体は双剣士負け、を想定したバランス）。

#### 帝国の影（imperial_shadow）

帝国の影部隊に属する隠密兵。麻痺の付与と回避で前衛を妨害する。

**役割**：麻痺・回避型の妨害ユニット

**技**：
- 当て身（中射程の物理攻撃／威力は控えめ／命中対象に麻痺を蓄積・永続）
- 回避 15%

麻痺の挙動は[§8.11 麻痺仕様](#811-麻痺仕様許容量倍化方式)を参照（連続再付与に強い「短期戦で強・長期戦で弱」の許容量倍化方式）。

#### 帝国軍 9 体（味方コピー）

性能は対応する味方ユニットと完全同一。詳細仕様（技・役割）は §2 / §3 の対応する味方ユニット詳細を参照。キャラクター上の立ち位置は「帝国軍に属する同種兵」。

| Id | 表示名 | 参照先 |
|---|---|---|
| `imperial_tank_def` | 帝国重装兵 | §2.2 重装兵 |
| `imperial_paladin` | 帝国騎士 | §2.2 騎士 |
| `imperial_atk_multi` | 帝国双剣士 | §2.2 双剣士 |
| `imperial_samurai` | 帝国傭兵 | §3.2 サムライ（技 Id は `imperial_samurai_sweep` に独立化）|
| `imperial_assassin` | 帝国暗殺者 | §3.2 忍者（技 Id は `nin_slash` を Roster 直書きで共有・Waza インスタンスとして独立）|
| `imperial_archer` | 帝国弓兵 | §2.2 弓兵 |
| `imperial_firemage` | 帝国炎魔導士 | §2.2 炎魔導士 |
| `imperial_aoemage` | 帝国大魔導士 | §3.2 雷魔導士（表示名のみ「大魔導士」に変更） |
| `imperial_healer` | 帝国司祭 | §2.2 司祭 |

---

# 第2部：実装情報

開発者が実装の在処を探すための補助情報。詳細なロジックは実コードを参照。

---

## 6. 命名規約

### 6.1 Unit.Id 体系

| 区分 | 形式 | 例 |
|---|---|---|
| 味方 | 無印（兵種名英訳・スネークケース） | `princess` / `tank_def` / `paladin` / `samurai` |
| 敵（帝国軍） | `imperial_` プレフィックス | `imperial_tank_def` / `imperial_paladin` / `imperial_samurai` |
| 敵（中ボス／ラスボス） | 固有名（`boss_*`） | `boss_baron` / `boss_one_eyed_samurai` |
| 敵（雑魚汎用） | 役割名（`imperial_*`） | `imperial_scout` |

### 6.2 アイコンディレクトリ

| 区分 | パス |
|---|---|
| 味方固有 | `Resources/Icons/Battlers/Allies/Unique/{id}.png` |
| 味方汎用 | `Resources/Icons/Battlers/Allies/Generic/{id}.png` |
| 敵汎用 | `Resources/Icons/Battlers/Enemies/{id}.png` |
| 敵ボス | `Resources/Icons/Battlers/Bosses/{id}.png` |

味方の固有／汎用の判別は Id ハードコードではなく、IconRegistry が Unique → Generic の順で探索する（[§8.3](#83-アイコン解決順フォールバック付き)）。新固有キャラ追加時はアセットを Unique/ に置くだけでコード変更不要。

### 6.3 SO アセット

`Resources/Data/Units/unit_{id}.asset` のフラット構成（Id プレフィックスで陣営判別可能なため階層を切らない）。

---

## 7. 実装ファイルの所在

| 領域 | ファイル |
|---|---|
| 味方ユニット定義 | [AlliesRoster.cs](../Assets/Scripts/Domain/Prototype/AlliesRoster.cs) |
| 敵雑魚ユニット定義 | [EnemiesRoster.cs](../Assets/Scripts/Domain/Prototype/EnemiesRoster.cs) |
| 中ボス／編成定義 | [BossesRoster.cs](../Assets/Scripts/Domain/Prototype/BossesRoster.cs) |
| Roster 共通ヘルパ（Unit/Waza ビルダー） | [RosterHelpers.cs](../Assets/Scripts/Domain/Prototype/RosterHelpers.cs) |
| 兵種ガイド表示テキスト | [GuideContent.cs](../Assets/Scripts/Presentation/Common/GuideContent.cs) |
| アイコン読み込み | [IconRegistry.cs](../Assets/Scripts/Presentation/Common/IconRegistry.cs) |
| Waza SO 由来データ | `Resources/Data/Wazas/waza_*.asset` |
| Unit SO 由来データ | `Resources/Data/Units/unit_*.asset` |

---

## 8. 特殊実装メモ

### 8.1 軍師の 2 ターン交互発動

「敵全体のバフ解除」「味方全体のデバフ解除」を 2 つの Waza に分離し、CD を互い違いに設定して 1 ターン毎に交互発動を実現している。

- Waza：`tac_purge`（敵バフ解除）／ `tac_dispel`（味方デバフ解除）
- 両方とも `Cooldown=2` ＋ `InitialCooldown=0/1` の互い違い
- 両方とも `IsForcedWhenReady=true`（CD 完了時に強制発動）
- 実装：[AlliesRoster.Tactician](../Assets/Scripts/Domain/Prototype/AlliesRoster.cs)

新規 TriggerCondition を追加する案も検討したが、既存の CD 機構で表現できるため不採用。

### 8.2 巫女の全体小回復

新規 Waza `heal_small_aoe`（祈り）として実装。毎ターン発動で挙動を単純化し、1 回あたりの数値を半減（合計回復量は維持）。

- SO アセット：`Resources/Data/Wazas/waza_heal_small_aoe.asset`
- 実装：[AlliesRoster.Healer2](../Assets/Scripts/Domain/Prototype/AlliesRoster.cs)
- SO 生成は [SoAssetGenerator](../Assets/Scripts/Data/Editor/SoAssetGenerator.cs) の Editor メニュー経由

### 8.3 アイコン解決順（フォールバック付き）

[IconRegistry](../Assets/Scripts/Presentation/Common/IconRegistry.cs) は以下の順で試行する：

1. 味方固有：`Resources/Icons/Battlers/Allies/Unique/{id}.png`
2. 味方汎用：`Resources/Icons/Battlers/Allies/Generic/{id}.png`
3. 味方直下（旧構成互換）：`Resources/Icons/Battlers/Allies/{id}.png`
4. 敵汎用：`Resources/Icons/Battlers/Enemies/{id}.png`
5. 敵ボス：`Resources/Icons/Battlers/Bosses/{id}.png`
6. 旧フラットパス（最終フォールバック）：`Resources/Icons/{id}.png`

陣営判定は Id プレフィックスで自動：`imperial_*` → 4 のみ／`boss_*` → 5 のみ／その他 → 1〜3 を順に。1〜3 で見つかった時点で確定。

### 8.4 中ボス／ラスボス編成

中ボス／ラスボス本体ユニットだけでなく取り巻きを含めた編成セットも [BossesRoster.cs](../Assets/Scripts/Domain/Prototype/BossesRoster.cs) で `PoisonBaronParty()` / `SamuraiParty()` / `PrinceDarkParty()` / `PrinceLightParty()` として定義。

### 8.5 孤高の戦士（傭兵のパッシブ）

陣営生存数連動の Conditional バフ。[LonerWolfConditionalProcessor](../Assets/Scripts/Domain/Battle/Conditional/LonerWolfConditionalProcessor.cs) が `ConditionalBuffProcessor` 派生として一括評価する（[500 §4.4.5](500_architecture.md)）。

- 購読フック：`BattleStart`（編成時点で 3 体以下でも発動）／`UnitDied`（人数減少時に強度を上書き）
- 付与する `AttackUp` / `DefenseUp` は `Category=Conditional` ／ `IsUndispellable=true` で軍師の `DispelsBuffs` を貫通する

### 8.6 闇のオーラ（皇太子（闇）のパッシブ）

ターン経過連動の Conditional バフ。[PrinceDarkAuraConditionalProcessor](../Assets/Scripts/Domain/Battle/Conditional/PrinceDarkAuraConditionalProcessor.cs) が動的に評価する。

- 購読フック：`BattleStart` ／ `TurnStart`
- 仕様：毎ターン全体攻撃「闇槍の薙ぎ」を撃ちつつ、自己 `AttackUp` のスタック数を `floor(currentTurn / 3)`（青天井）で更新する
- 1 スタックあたり Magnitude +15。T1=0／T3=15／T6=30／T9=45／T12=60／T15=75 ...
- 必敗演出のため打ち止めなし。バフのスタックが 3 になる頃には全滅する想定
- 付与は `Category=Conditional` ／ `IsUndispellable=true`

### 8.7 王家のペンダント（ブリジットのパッシブ）

クロスユニット型の Conditional バフ。[PendantConditionalProcessor](../Assets/Scripts/Domain/Battle/Conditional/PendantConditionalProcessor.cs) が評価する。

- 購読フック：`BattleStart` ／ `UnitDied`
- 対象：`PendantOwnerTag` を持つユニット（ブリジット）と、コンストラクタで指定された Partner Id を持つユニット（VSプロトでは王女）
- 仕様：両者生存時、両者に `DefenseUp +3` を付与（単一の `AuraSourceId="pendant:<owner.id>"` で束ねる）。どちらかが死亡 or 不在になった瞬間、両者から剥奪
- 付与は `Category=Conditional` ／ `IsUndispellable=true`
- 既存 Processor が「条件→自分にバフ」だったのに対し、本 Processor は **「条件→自分＋他ユニットにバフ」のクロスユニット型**

### 8.8 攻防一体（ブリジットのパッシブ）

バフ連動型の Conditional バフ。[OffenseDefenseLinkConditionalProcessor](../Assets/Scripts/Domain/Battle/Conditional/OffenseDefenseLinkConditionalProcessor.cs) が評価する。

- 購読フック：`BattleStart` ／ `TurnStart` ／ `BuffApplied` ／ `BuffRemoved`
- 対象：`OffenseDefenseLinkTag` を持つユニット（ブリジット）
- 仕様：対象ユニットの `DefenseUp` バフ Magnitude×Stacks 合計を、同値分の `AttackUp` として転写する。`DefenseDown` 系は集計対象外（**「デバフを受けても減少はしない」を自然に達成**）
- 自身が付与した `AttackUp`（同 `AuraSourceId="off_def_link:<unit.id>"`）は集計から除外することで `BuffApplied` フックの自己ループを構造的に止める
- 付与は `Category=Conditional` ／ `IsUndispellable=true`

### 8.9 バフ・デバフ・状態異常の 5 分類（500 §4.4）

`StatusEffect.Category`（`BuffCategory` enum）で意図ラベルを表現する。挙動（`IsUndispellable` / `RemainingTurns` / `AuraSourceId`）は独立属性。

| 分類 | 例 | 持続 | 解除 | 発生源死亡時 | 処理 |
|---|---|---|---|---|---|
| **Persistent**（常在型） | 王女「王家の加護」／軍師「戦術隊形」 | 永続 | 解除されない | 効果を失う | Static |
| **Triggered**（発動型） | スキル発動の有限ターンバフ・デバフ | 有限ターン | 解除される | そのまま残る | Static |
| **Conditional**（条件型） | 傭兵「孤高の戦士」／皇太子（闇）「闇のオーラ」／ブリジット「王家のペンダント」「攻防一体」 | 条件成立中は永続 | 解除されない | 効果を失う／条件再評価 | Dynamic |
| **StatusAilment**（状態異常） | Burn / Freeze / Paralysis / Curse | 永続 | DispelsBuffs/Debuffs では×／CleansesStatusAilments で○ | そのまま残る | Static |
| **ActionGuard**（自己防御一過性） | 自己防御フォールバック (def_guard) の DefenseUp | 付与から次の自分の行動順まで | 解除されない | そのまま残る | Dynamic（行動順削除） |

実装手順は [500 §4.4 / §4.4.5](500_architecture.md) を参照。生成は `StatusEffect.CreatePersistent / CreateTriggered / CreateConditional / CreateStatusAilment / CreateActionGuard` ファクトリ経由が原則。例外は CI テスト（`VSProto_StatusEffectConsistencyTests`）でホワイトリスト管理する。

**StatusAilment の位置づけ**：Triggered の「持続を永続化＋解除経路を別系統に置き換えた」エッジケースが既に 4 種あって独立カテゴリに昇格したもの。将来「動的な状態異常」が出てきたら Conditional のエッジケースとして実装可能（500 §4.4.5 拡張点）。

**ActionGuard の位置づけ**：Triggered の「持続を行動順イベントに置き換えた＋ログ抑制」のエッジケース。RemainingTurns=-1 で OnEndPhase の時限減算から除外し、`StatusEffectProcessor.HandleActionStart` が「自分の行動順到達時」に削除する。SPD の遅いユニットが防御を発動した場合に同ターン内で消えてしまう Triggered の落とし穴を構造的に回避するための独立カテゴリ。技と効果が 1:1 対応で意味が自明なものに限定（ログ抑制のため）。

### 8.10 IsUndispellable フラグ

`StatusEffect.IsUndispellable=true` を立てた能力バフ／デバフは `Waza.DispelsBuffs` / `DispelsDebuffs` 経路で剥奪されない（実装：[ActionExecutor.ApplyCleanseDispel](../Assets/Scripts/Domain/Battle/ActionExecutor.cs)）。`CleansesStatusAilments`（状態異常解除）は別経路なので影響を受けない。

Persistent / Conditional はファクトリ経由で自動的に `IsUndispellable=true` が立つ。Triggered で例外的に解除不能にしたい場合はコンストラクタ直接生成で明示宣言する（500 §4.4.3 L2 レイヤー）。

### 8.11 麻痺仕様（許容量倍化方式）

実装：[RuntimeUnit.IsParalyzed / ParalysisTolerance](../Assets/Scripts/Domain/Models/RuntimeUnit.cs) ／ [StatusEffectProcessor.HandleActionSkipped](../Assets/Scripts/Domain/Battle/StatusEffectProcessor.cs)

**ルール**：

- 各ユニットは戦闘中に **麻痺スタック許容量**（`ParalysisTolerance`）を持つ。初期値は `Unit.BaseParalysisTolerance`（既定 1・耐麻痺ユニットは 2 以上）
- 自分の行動順が来た時、**麻痺スタック合計が許容量以上なら行動不能**（`IsParalyzed=true`）
- 行動不能発動時、**麻痺効果を全削除**＋**許容量を倍化**（1→2→4→8…）。次回以降は同じ強度では止められない
- スタック上限は実質撤廃（帝国の影は `MaxStacks=99`）。同ターン中に複数体から麻痺を受ければスタック合算される

**シミュレーション例**（毎ターン Stacks+=1 が継続する場合）：

| T | 付与後 stacks | 許容量 | 結果 |
|---|---|---|---|
| 1 | 1 | 1 | 不能 → 許容量 2 |
| 2 | 1 | 2 | 動ける |
| 3 | 2 | 2 | 不能 → 許容量 4 |
| 4-6 | 1,2,3 | 4 | 動ける |
| 7 | 4 | 4 | 不能 → 許容量 8 |
| 15 | 8 | 8 | 不能 → 許容量 16 |

→ 行動不能になるのは **T1, T3, T7, T15...** と指数間隔で発生。短期戦では強力、長期戦では実質無効化される。

**3 体麻痺ユニット同時運用時**（毎ターン Stacks+=3）：

| T | 付与後 stacks | 許容量 | 結果 |
|---|---|---|---|
| 1 | 3 | 1 | 不能 → 2 |
| 2 | 3 | 2 | 不能 → 4 |
| 4 | 6 | 4 | 不能 → 8 |
| 7 | 9 | 8 | 不能 → 16 |
| 13 | 18 | 16 | 不能 → 32 |

→ 行動不能回数は単独運用と大きく変わらず、**麻痺ハメ編成は成立しない**設計。

**個別仕様**：

- `Unit.BaseParalysisTolerance` を 2 以上に設定すれば、特定ユニットを耐麻痺化できる（将来用）
- 凍結による行動スキップでは許容量は上昇しない（凍結は別系統）
- `Cleanse`（状態異常解除）で麻痺スタックを 0 にできるが、許容量は据置（戦闘中の累積耐性は失われない）

---

## 9. 戦闘仕様メモ（エッジケース）

ベースの戦闘仕様は [110_combat_spec.md](110_combat_spec.md) に集約。本章は VSプロト範囲で実装と試遊から確定したエッジケースの **正確な挙動** を「もう一度調べ直さなくて済む」ように記録する場所。

110 に書くと冗長になる粒度のメモを保管し、新しい仕様議論で「これどうだったっけ」と思ったときの一次資料にする。

### 9.1 麻痺中はかばう無効化＋自分の行動順で麻痺解除

「麻痺攻撃でかばうユニットを一時的に無効化できる」というメカニクス。行動順に依存して挙動が変わる。

**確定挙動**：
- 麻痺中のユニットは [`RuntimeUnit.IsCovering`](../Assets/Scripts/Domain/Models/RuntimeUnit.cs) が `false` になり、かばう Cover 効果が**そのアクションごとにリアルタイム無効化**される
- 麻痺の **解消は自分の行動順到達時**：`StatusEffectProcessor.HandleActionSkipped` で `RemoveEffectsWhere(Paralysis) + ParalysisTolerance *= 2`
- つまり「自分の行動順より前に来る敵攻撃」は素通り、「自分の行動順以降」はかばう復帰

**シナリオ A：SPD 低いかばう持ち（重装兵）**

| 順 | 発生 | 結果 |
|---|---|---|
| 1 | 敵Aが麻痺攻撃 | 重装兵が麻痺付与 |
| 2 | 敵Bが攻撃 | `IsCovering=false` → かばわず元ターゲットに着弾 |
| 3 | 重装兵の行動順 | 麻痺解消＋許容量倍化 |
| 4 | 敵Cが攻撃 | `IsCovering=true` → かばう発動 |

**シナリオ B：SPD 早いかばう持ち**

| 順 | 発生 | 結果 |
|---|---|---|
| 1 | かばう持ちの行動順 | まだ麻痺してないので何もしない（攻撃しないなら待機） |
| 2 | 敵Aが麻痺攻撃 | 付与 |
| 3 | 敵B/Cが攻撃 | かばう無効化 |
| 4 | 次ターン・かばう持ちの行動順 | 麻痺解消（許容量倍化） |

**設計的含意**：
- SPD 早いかばう持ちは「短期戦で損／長期戦で復帰早い」というキャラ特性。許容量倍化のおかげで長期戦ではハメられない
- 重装兵に麻痺が効くのは「攻撃を止める意味」ではなく「かばう無効化のデメリット」が刺さるため。麻痺攻撃は前衛タンクへの有効戦術
- 凍結（`IsFullyFrozen`：Freeze スタック合計 ≥ 10）も同じ構造でかばう無効化。ただし凍結は「自分の行動順での自動解除」を持たないため、麻痺より重い拘束

**関連実装**：
- [RuntimeUnit.IsCovering](../Assets/Scripts/Domain/Models/RuntimeUnit.cs)
- [ActionExecutor.ResolveCoverTarget](../Assets/Scripts/Domain/Battle/ActionExecutor.cs)
- [StatusEffectProcessor.HandleActionSkipped](../Assets/Scripts/Domain/Battle/StatusEffectProcessor.cs)

---

## 10. 関連ドキュメント

- [310_vsprototype_spec.md §1.4](310_vsprototype_spec.md) — マス種別ごとの敵編成
- [310_vsprototype_spec.md §1.12](310_vsprototype_spec.md) — 兵種仕様
- [500_architecture.md §2.2](500_architecture.md) — ディレクトリ構造＋ asmdef 配置
- [110_combat_spec.md](110_combat_spec.md) — 戦闘システム仕様（射程・かばう・状態異常等の元仕様）
