# 実装ノート

[500_architecture.md](500_architecture.md) はアーキテクチャ憲法として「変えてはいけない設計原則」を扱う。
[900_development_rules.md](900_development_rules.md) は毎回読む禁止事項・判断基準を扱う。
本書は**実装で詰まりがちな落とし穴と回避パターン**を層別に集約する。500 = 設計原則、900 = 規約、本書 = 落とし穴集 と Claude 操作 Tips。

**前提**：§1 IMGUI 実装ノートは IMGUI 採用がプロト段階の判断（[500 §7.4](500_architecture.md) 冒頭参照）であることに依存する。フル版で UI フレームワークが変わる場合、§1 の Tips は移行時に役目を終える可能性がある。

**章構成**：層別／作業別に枠を用意する。現状中身があるのは §1（IMGUI）と §2（Claude 操作）のみ。将来 Test / Data / Domain の Tips が出てきたら §3 以降に追記する（肥大化したら 921 へ分離検討）。

新規 GUI を作る／OnGUI でモーダルを足す等の作業に入る前に §1 を、Claude 自身の操作で詰まったら §2 を確認すること。

---

## 1. IMGUI 実装ノート

VSプロト範囲で踏んだ IMGUI 由来の落とし穴と回避パターン。設計方針（プラットフォーム前提・画面遷移アーキ・SSoT 集約・アセット取り込みパターン・ビジュアル方針・Inspector 公開パターン）は [500 §7.4](500_architecture.md) を参照。

### 1.1 OnGUI モーダル 3 大落とし穴

OnGUI モーダルには 3 大落とし穴がある。新規モーダル追加時は必ず以下のパターンを踏襲する：

1. **ホット争奪**：背後の `GUI.Button` が MouseDown 時にホットコントロールを横取りすると、上に重なるモーダル内ボタンが MouseUp で発火しなくなる。対策：モーダル表示中は背後の GUI を `GUI.enabled=false` で抑止する（OnGUI 冒頭で `prev = GUI.enabled; if (モーダル表示中) GUI.enabled = false;` → 描画後に `GUI.enabled = prev;`）
2. **MouseDown + Event.Use**：モーダル外クリックで閉じる際は `Event.current.type == EventType.MouseDown && !modalRect.Contains(mouse)` を見て `Event.current.Use()` で後段の GUI に Event を伝播させない
3. **遅延状態消去**：閉じる動作（外クリック／閉じるボタン／状態遷移）は共通の `CloseXxxModal()` メソッドに集約し、関連状態（スクロール位置・選択中アイテム等）も同時にクリアする。閉じ忘れ／状態残りで「前データが見える」バグの温床

実装例：[VSPrototypeBattleGUI.DrawLogModal](../Assets/Scripts/Presentation/VSPrototype/VSPrototypeBattleGUI.cs) ／ [VSPrototypeMapGUI.DrawPlacementModal](../Assets/Scripts/Presentation/VSPrototype/VSPrototypeMapGUI.cs) ／ [VSPrototypeInteriorGUI.DrawInteriorSubModalLayout](../Assets/Scripts/Presentation/VSPrototype/VSPrototypeInteriorGUI.cs)

### 1.2 GUILayout vs 絶対座標 GUI（描画スキップ条件の罠）

描画有無がフレームごとに変わる UI（ホバーツールチップ等）は **絶対座標 `GUI.Label` / `GUI.Box` で書く**。`GUILayout` は Layout/Repaint で同じ `Begin/End` 数を要求するため、描画スキップ条件があるだけで `ArgumentException: Getting control N's position in a group with only N controls` で破綻する。

### 1.3 描画順制御の罠（DefaultExecutionOrder vs Phase 別 return）

`[DefaultExecutionOrder]` での描画順制御は実機で不安定なケースあり。同 GameObject に複数 GUI が並ぶ場合は [500 §7.4.2](500_architecture.md) の **Phase 別早期 return で描画担当を排他化** する方が確実。

### 1.4 WebGL 日本語フォント遅延ロードコール忘れ

WebGL での日本語表示は `GuiTheme.EnsureJapaneseFont()` を各 OnGUI 冒頭で呼ばないと文字化けする。`Resources/Fonts/NotoSansJP-Regular.ttf` を遅延ロード方式で持っている都合上、初回 OnGUI 呼び出しまでロード完了していない。各 OnGUI 冒頭で呼ぶパターンが必要。

### 1.5 Inspector Override 優先の罠

[500 §7.4.6](500_architecture.md) の `[SerializeField] private` 公開パターンを使うと、Inspector で値を変更するとシーンファイルに Override として保存される。**コードのデフォルトを変えても Inspector の値が優先される**（青ハイライト表示）。リセットしたい時は Inspector で右クリック → Reset。

Claude がコードのデフォルトを更新したのに実機で反映されない時は、まず Inspector で Override されていないか確認すること。

### 1.6 GUILayout.Label の wordWrap 高さ自動算出が不安定

`style.wordWrap = true` の Label がパネル幅で 2 行以上に折り返したとき、`GUILayout.Label(text, style)` は 1 行分の高さしか確保せず、後続レイアウトと重なって 2 行目以降が見切れることがある。

回避パターン：親レイアウトの実描画幅 `contentWidth` を渡し、`style.CalcHeight(new GUIContent(text), contentWidth)` で必要高さを算出して `GUILayout.Height(h)` で明示確保する。

```cs
float h = style.CalcHeight(new GUIContent(text), contentWidth);
GUILayout.Label(text, style, GUILayout.Height(h));
```

実装例：[VSPrototypeMapGUI.DrawSynergyLine](../Assets/Scripts/Presentation/VSPrototype/VSPrototypeMapGUI.cs)

---

## 2. Claude 操作 Tips

Claude 自身がツール操作で詰まりやすいパターンと回避策。900 から「毎回読むほどではないが知らないと事故る操作 Tips」を分離して集約。

### 2.1 同一ファイルへの並列 Edit は禁止

- 同じファイルに対する複数の Edit を 1 メッセージ内で並列実行すると、Read 状態管理の race condition で 2 つ目以降が "File has not been read yet" エラーで失敗することがある。
- **同一ファイル内で複数置換が必要な場合は、Edit を順次（1 メッセージにつき 1 Edit）実行する**。
- 異なるファイルへの Edit は並列実行 OK。
- Read 直後の Edit でも、Read に `limit` を付けると Edit が失敗する場合がある（経験則）。確実性重視なら limit なし Read → Edit。

### 2.2 git mv で履歴保持を優先

- ファイル名変更・移動は `git rm` ＋ `Write` ではなく **`git mv` を使う**。Git の rename 検出を働かせて履歴を保持できる（90% 類似度で `R` 表示）。
- 大量のクラス名置換を伴うリネームでも、`git mv` で物理移動 → `Edit replace_all` で中身置換、の順で履歴維持できる。
- .cs ファイルだけでなく `.cs.meta` も連動して `git mv` すること（Unity の GUID 整合のため）。

### 2.3 SO アセット生成は Editor 拡張ツール経由

- Claude は ScriptableObject の `.asset` ファイルを直接生成できない（Unity の GUID 計算ロジック・SerializedObject 経由のシリアライズが必要なため）。
- SO アセットが必要な場合の手順：
  1. Claude が **Editor 拡張ツール**（例：`SoAssetGenerator`）を `Assets/Scripts/Data/Editor/` 配下に作成（`AssetDatabase.CreateAsset` を使う）
  2. ユーザーが Unity Editor のメニュー（例：`Tools > Echolos > ...`）から実行
  3. 生成された `.asset` ＋自動生成された `.cs.meta` ＋ `.asset.meta` を、次セッション冒頭でユーザーからの取り込みコミット依頼に従って commit
- 原則は [900 §7.8](900_development_rules.md) も参照。

---

## 3. Test 実装ノート

（未着手。テスト実装中に「同じ落とし穴に二度ハマった」パターンが出てきた時点で起こす）

## 4. Data 実装ノート

（未着手。SO アセット生成 Editor ツール・SO シリアライズの罠等が溜まってきた時点で起こす）

## 5. Domain / UseCase 実装ノート

（未着手。純 C# 層なので一般的な C# パターンで足り、本節が起こされる見込みは低い。例外的に出てきた時のみ追記）
