// Assets/Scripts/UnityView/Stage3BattleSpectatorSandboxGUI.cs
// 段階4 §11.5 Step 4-13a 検証ハーネス：観戦ビュー（戦闘可視化画面）だけを単独で起動する遊び場。
//
// 【目的】
// - 戦闘演出（A6/A7/A10/A11+B1/A4-1 など）の検証のため、毎回ドラフトを通すコストを排除する。
// - 編成を即決めて「観戦開始」一発で観戦ビューに入り、終わったら「もう一度（同Seed）」で
//   同条件を繰り返し再生できる。アイコン素材の差し替え・演出パラメータ調整の確認に最適。
//
// 【既存との関係】
// - [[Stage3SandboxGUI]] は戦闘結果をテキストログだけ表示するハーネス（統計取り・バグ再現用）。
// - こちらは観戦ビューを描画するためのハーネス。役割を分離する目的で別ファイルにしている。
// - 編成 UI ロジックは Stage3SandboxGUI の構造をコピーして簡略化（共通化はせず独立保守）。
//
// 【シーン配線】
// 空 GameObject に本コンポーネントを付けるだけで動く（Canvas/Prefab 不要・OnGUI/IMGUI）。
// 推奨：Assets/Scenes/Stage3BattleSpectatorSandbox.unity を作成し本コンポーネントを付ける。
//
// 【操作フロー】
// Play → 編成画面（プリセット or 個別選択）→ [観戦開始]
//     → 観戦ビュー（戦闘可視化 + 速度切替 + ターゲット線トグル + スキップ）
//     → 終了後 [もう一度（同Seed）] [新Seedで再生] [編成に戻る]
using System;
using System.Collections.Generic;
using UnityEngine;
using Echolos.Domain.Battle;
using Echolos.Domain.Models;
using Echolos.Domain.Prototype;

using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Story;
using Echolos.Presentation.Common;
using Echolos.Presentation.Battle;
using Echolos.Presentation.Story;
namespace Echolos.Presentation
{
    /// <summary>観戦ビュー単独起動用のサンドボックス（戦闘演出検証ハーネス）。</summary>
    public sealed class Stage3BattleSpectatorSandboxGUI : MonoBehaviour
    {
        // ════════════════════════════════════════════════
        // ユニット選択肢（Stage3SandboxGUI と同じ・順序も合わせる）
        // ════════════════════════════════════════════════
        private static readonly (string Label, Func<Unit> Factory)[] UnitChoices =
        {
            ("(空き)",         null),
            ("重装兵",         Stage3Roster.GeneralTank),
            ("大盾兵",         Stage3Roster.HpTank),
            ("騎士",           Stage3Roster.Paladin),
            ("双剣士",         Stage3Roster.Attacker1),
            ("サムライ",       Stage3Roster.Samurai),
            ("大槌兵",         Stage3Roster.Debuffer),
            ("アサシン",       Stage3Roster.Assassin),
            ("弓兵",           Stage3Roster.Archer),
            ("忍者",           Stage3Roster.Ninja),
            ("炎魔導士",       Stage3Roster.FireMage),
            ("雷魔導士",       Stage3Roster.AoeMage),
            ("司祭",           Stage3Roster.Healer1),
            ("巫女",           Stage3Roster.Healer2),
            ("踊り子",         Stage3Roster.Buffer),
            ("軍師",           Stage3Roster.Tactician),
            ("[ボス]男爵",     Stage3Roster.PoisonBaron),
            ("[ボス]サムライ", Stage3Roster.OneEyedSamurai),
            ("[敵専用]散兵",   Stage3Roster.Skirmisher),
        };

        // ════════════════════════════════════════════════
        // プリセット（Stage3SandboxGUI と同一）
        // ════════════════════════════════════════════════
        private static readonly (string Label, Func<Unit>[] Factories)[] Presets =
        {
            ("男爵PT（毒/耐久ボス）", new Func<Unit>[]
            {
                Stage3Roster.GeneralTank, Stage3Roster.HpTank,      Stage3Roster.Debuffer,
                Stage3Roster.GeneralTank, Stage3Roster.PoisonBaron, Stage3Roster.Healer2,
            }),
            ("サムライPT（攻撃全振り）", new Func<Unit>[]
            {
                Stage3Roster.GeneralTank, Stage3Roster.OneEyedSamurai, Stage3Roster.Samurai,
                Stage3Roster.Ninja,       Stage3Roster.Ninja,          Stage3Roster.Ninja,
            }),
            ("男爵戦・火力対策(勝想定)", new Func<Unit>[]
            {
                Stage3Roster.Debuffer, Stage3Roster.Attacker1, Stage3Roster.Samurai,
                Stage3Roster.Buffer,   Stage3Roster.Tactician, Stage3Roster.FireMage,
            }),
            ("男爵戦・鉄壁耐久(負想定)", new Func<Unit>[]
            {
                Stage3Roster.HpTank,      Stage3Roster.Attacker1, Stage3Roster.Debuffer,
                Stage3Roster.GeneralTank, Stage3Roster.Healer1,   Stage3Roster.Healer2,
            }),
            ("サムライ戦・硬前列遠隔(勝想定)", new Func<Unit>[]
            {
                Stage3Roster.HpTank, Stage3Roster.GeneralTank, Stage3Roster.Paladin,
                Stage3Roster.AoeMage, Stage3Roster.Healer1,    Stage3Roster.Archer,
            }),
            ("サムライ戦・デバフバフ火力(負想定)", new Func<Unit>[]
            {
                Stage3Roster.GeneralTank, Stage3Roster.Attacker1, Stage3Roster.Debuffer,
                Stage3Roster.Healer1,     Stage3Roster.FireMage,  Stage3Roster.Buffer,
            }),
            // 状態異常・バフデバフが多発するように偏らせた編成（A10 アイコン確認向け）
            ("A10検証用（バフデバフ祭り）", new Func<Unit>[]
            {
                Stage3Roster.Debuffer, Stage3Roster.Buffer,    Stage3Roster.Tactician,
                Stage3Roster.FireMage, Stage3Roster.Healer1,   Stage3Roster.Healer2,
            }),
        };

        // ════════════════════════════════════════════════
        // 状態
        // ════════════════════════════════════════════════
        private enum Mode { Build, Spectating, Finished }
        private Mode _mode = Mode.Build;

        // 編成（各スロットの UnitChoices インデックス）
        private readonly int[] _allyChoice = new int[6];
        private readonly int[] _enemyChoice = new int[6];

        // 戦闘パラメータ
        private int _maxTurns = 15;
        private string _seedInput = "0";
        private bool _useFixedSeed = true;

        // 直前実行のパラメータ（「もう一度（同Seed）」で再現するため保持）
        private List<RuntimeUnit> _lastAllies;
        private List<RuntimeUnit> _lastEnemies;
        private int _lastSeed;
        private bool _lastUsedFixedSeed;
        private int _lastMaxTurns;

        // 観戦本体
        private readonly Stage3BattleSpectatorView _spectator = new Stage3BattleSpectatorView();
        private BattleReport _lastReport;

        // 観戦コントロール（Stage3CampaignGUI と同じトーンで）
        private float _spectateSpeed = 1f;  // 0=ステップ, 1, 2, 4
        private bool _showTargetLines;

        // UI
        private Vector2 _mainScroll;
        private string _flash = "";
        private GUIStyle _header, _sub, _wrap, _box;

        // ════════════════════════════════════════════════
        // Unity ライフサイクル
        // ════════════════════════════════════════════════

        private void Start()
        {
            // 初期編成：A10 検証用（バフデバフ祭り）vs サムライPT を入れておく
            ApplyPreset(_allyChoice,  Presets[6].Factories);
            ApplyPreset(_enemyChoice, Presets[1].Factories);
            _flash = "編成を確認して『観戦開始』。観戦中は速度切替・ターゲット線トグル・スキップ可。";
        }

        private void Update()
        {
            if (_mode != Mode.Spectating) return;

            // ステップモードでは Tick せず手動送り。
            if (_spectateSpeed <= 0f) return;

            _spectator.SecondsPerEvent = 0.5f / _spectateSpeed;
            if (!_spectator.IsFinished)
                _spectator.Tick(Time.deltaTime);
            else
            {
                // 完走したら Finished に遷移し、結果バナーを出す
                _mode = Mode.Finished;
                if (_lastReport != null)
                    _flash = $"戦闘終了：{ResultJp(_lastReport.Result)}（{_lastReport.Turns}T）";
            }
        }

        // ════════════════════════════════════════════════
        // 描画
        // ════════════════════════════════════════════════

        private void OnGUI()
        {
            // 既存サンドボックスに合わせて日本語フォントを毎フレーム適用
            GuiTheme.EnsureJapaneseFont();
            EnsureStyles();

            if (_mode == Mode.Build)
                DrawBuildScreen();
            else
                DrawSpectateScreen();

            if (!string.IsNullOrEmpty(_flash))
            {
                // 画面下に薄いフラッシュメッセージ
                var r = new Rect(10, Screen.height - 28, Screen.width - 20, 22);
                GUI.Label(r, "▶ " + _flash, _wrap);
            }
        }

        // ── 編成画面 ──
        private void DrawBuildScreen()
        {
            GUILayout.BeginArea(new Rect(10, 10, Screen.width - 20, Screen.height - 40));
            _mainScroll = GUILayout.BeginScrollView(_mainScroll);

            GUILayout.Label("◆ 観戦ビュー サンドボックス（戦闘演出検証ハーネス）", _header);
            GUILayout.Label(
                "編成を組んで『観戦開始』を押すと観戦画面（A6/A7/A10/A11+B1/A4-1 の演出が動く）に入ります。" +
                "終了後は同 Seed で何度でも再生できるので、アイコン素材差し替えや演出調整の検証に使えます。", _wrap);
            GUILayout.Space(6);

            DrawPresetRow();
            GUILayout.Space(6);

            DrawPartiesRow();
            GUILayout.Space(8);

            DrawRunRow();
            GUILayout.Space(8);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // ── プリセット行（敵味方どちらにも適用可能・既存と同方針）──
        private void DrawPresetRow()
        {
            GUILayout.BeginVertical(_box);
            GUILayout.Label("【プリセット】右の『→味方／→敵』で適用先を選択。", _sub);
            for (int i = 0; i < Presets.Length; i++)
            {
                var p = Presets[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label(p.Label, GUILayout.Width(280));
                if (GUILayout.Button("→ 味方", GUILayout.Width(80))) ApplyPreset(_allyChoice, p.Factories);
                if (GUILayout.Button("→ 敵",   GUILayout.Width(60))) ApplyPreset(_enemyChoice, p.Factories);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }

        // ── 編成（味方／敵）スロット選択 ──
        private void DrawPartiesRow()
        {
            GUILayout.BeginHorizontal();
            DrawPartyColumn("味方", _allyChoice);
            GUILayout.Space(8);
            DrawPartyColumn("敵",   _enemyChoice);
            GUILayout.EndHorizontal();
        }

        private void DrawPartyColumn(string title, int[] choices)
        {
            GUILayout.BeginVertical(_box, GUILayout.Width((Screen.width - 40) / 2f));
            GUILayout.Label($"【{title}】slot 0-2=前列 / slot 3-5=後列", _sub);
            for (int s = 0; s < 6; s++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"slot {s}", GUILayout.Width(48));
                if (GUILayout.Button("<", GUILayout.Width(28)))
                    choices[s] = (choices[s] - 1 + UnitChoices.Length) % UnitChoices.Length;
                GUILayout.Label(UnitChoices[choices[s]].Label, GUILayout.Width(140));
                if (GUILayout.Button(">", GUILayout.Width(28)))
                    choices[s] = (choices[s] + 1) % UnitChoices.Length;
                if (GUILayout.Button("空", GUILayout.Width(36)))
                    choices[s] = 0;
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            if (GUILayout.Button($"{title} 全クリア", GUILayout.Width(140)))
                for (int i = 0; i < 6; i++) choices[i] = 0;
            GUILayout.EndVertical();
        }

        // ── 実行設定行＋観戦開始ボタン ──
        private void DrawRunRow()
        {
            GUILayout.BeginVertical(_box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("最大ターン", GUILayout.Width(80));
            string mtStr = GUILayout.TextField(_maxTurns.ToString(), GUILayout.Width(50));
            if (int.TryParse(mtStr, out var mt) && mt > 0 && mt < 999) _maxTurns = mt;
            GUILayout.Space(16);

            _useFixedSeed = GUILayout.Toggle(_useFixedSeed, " Seed 固定", GUILayout.Width(96));
            if (_useFixedSeed)
            {
                GUILayout.Label("Seed", GUILayout.Width(40));
                _seedInput = GUILayout.TextField(_seedInput, GUILayout.Width(80));
            }
            else
            {
                GUILayout.Label("(Seed: ランダム)", GUILayout.Width(140));
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            if (GUILayout.Button("▶ 観戦開始", GUILayout.Height(40))) StartSpectate();
            GUILayout.EndVertical();
        }

        // ── 観戦画面 ──
        private void DrawSpectateScreen()
        {
            const float margin = 10f;
            const float footerH = 60f;

            // 観戦ビュー本体（画面ほぼ全面）
            var viewRect = new Rect(margin, margin, Screen.width - margin * 2,
                Screen.height - margin * 2 - footerH - 28f /* flash 帯 */);
            _spectator.DrawGUI(viewRect);

            // フッターコントロール
            var footerRect = new Rect(margin, Screen.height - margin - footerH - 28f,
                Screen.width - margin * 2, footerH);
            GUILayout.BeginArea(footerRect);
            GUILayout.BeginHorizontal();

            // 速度トグル
            GUILayout.Label("速度", GUILayout.Width(36));
            DrawSpeedToggle(0f, "ステップ");
            DrawSpeedToggle(1f, "×1");
            DrawSpeedToggle(2f, "×2");
            DrawSpeedToggle(4f, "×4");

            GUILayout.Space(12);

            // ターゲット線（ステップ／×1 のみ有効）
            bool canShowLines = _spectateSpeed <= 1f;
            GUI.enabled = canShowLines;
            _showTargetLines = GUILayout.Toggle(_showTargetLines, " ターゲット線", GUILayout.Width(116));
            GUI.enabled = true;
            _spectator.ShowTargetLines = canShowLines && _showTargetLines;

            GUILayout.Space(12);

            // ステップモードのみ「次イベント →」を出す
            if (_spectateSpeed <= 0f)
            {
                GUI.enabled = !_spectator.IsFinished;
                if (GUILayout.Button("次イベント →", GUILayout.Width(140), GUILayout.Height(36)))
                    _spectator.StepOne();
                GUI.enabled = true;
                GUILayout.Space(8);
            }

            GUI.enabled = !_spectator.IsFinished;
            if (GUILayout.Button("スキップ", GUILayout.Width(100), GUILayout.Height(36)))
            {
                _spectator.SkipToEnd();
                _mode = Mode.Finished;
                if (_lastReport != null) _flash = $"スキップ完了：{ResultJp(_lastReport.Result)}（{_lastReport.Turns}T）";
            }
            GUI.enabled = true;

            GUILayout.Space(12);

            // 完走後だけ有効化される「再生」「編成に戻る」
            GUI.enabled = _mode == Mode.Finished;
            if (GUILayout.Button("↻ もう一度（同Seed）", GUILayout.Width(170), GUILayout.Height(36)))
                ReplaySameSeed();
            if (GUILayout.Button("⟳ 新Seedで再生", GUILayout.Width(150), GUILayout.Height(36)))
                ReplayNewSeed();
            GUI.enabled = true;

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("◀ 編成に戻る", GUILayout.Width(140), GUILayout.Height(36)))
            {
                _mode = Mode.Build;
                _flash = "編成画面に戻りました。";
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawSpeedToggle(float speed, string label)
        {
            bool selected = Mathf.Approximately(_spectateSpeed, speed);
            var prev = GUI.backgroundColor;
            if (selected) GUI.backgroundColor = Color.cyan;
            if (GUILayout.Button(label, GUILayout.Width(56), GUILayout.Height(36)))
                _spectateSpeed = speed;
            GUI.backgroundColor = prev;
        }

        // ════════════════════════════════════════════════
        // 実行ロジック
        // ════════════════════════════════════════════════

        private void StartSpectate()
        {
            var allies = BuildParty(_allyChoice);
            var enemies = BuildParty(_enemyChoice);
            if (allies.Count == 0 || enemies.Count == 0)
            {
                _flash = "味方か敵のどちらかが空です。最低1体ずつ配置してください。";
                return;
            }

            int seed;
            if (_useFixedSeed)
            {
                seed = int.TryParse(_seedInput, out var s) ? s : 0;
            }
            else
            {
                seed = UnityEngine.Random.Range(0, int.MaxValue);
                _seedInput = seed.ToString();
            }

            RunWith(allies, enemies, seed, _useFixedSeed, _maxTurns);
        }

        private void ReplaySameSeed()
        {
            if (_lastAllies == null || _lastEnemies == null)
            {
                _flash = "再生履歴がありません。";
                return;
            }
            // 同 Seed で完全再現するには RuntimeUnit を再生成（HP/StatusEffect を初期化）。
            var allies  = RebuildFromSnapshot(_lastAllies);
            var enemies = RebuildFromSnapshot(_lastEnemies);
            RunWith(allies, enemies, _lastSeed, _lastUsedFixedSeed, _lastMaxTurns);
        }

        private void ReplayNewSeed()
        {
            if (_lastAllies == null || _lastEnemies == null)
            {
                _flash = "再生履歴がありません。";
                return;
            }
            var allies  = RebuildFromSnapshot(_lastAllies);
            var enemies = RebuildFromSnapshot(_lastEnemies);
            int newSeed = UnityEngine.Random.Range(0, int.MaxValue);
            _seedInput = newSeed.ToString();
            RunWith(allies, enemies, newSeed, useFixedSeed: false, _lastMaxTurns);
        }

        /// <summary>
        /// 戦闘を1回実行し、観戦ビューを初期化してモードを Spectating に遷移する。
        /// 引数 RuntimeUnit は HP 全快前提（BuildParty/RebuildFromSnapshot で常に新規生成）。
        /// </summary>
        private void RunWith(List<RuntimeUnit> allies, List<RuntimeUnit> enemies,
            int seed, bool useFixedSeed, int maxTurns)
        {
            // Seed 固定なら System.Random 経由で完全再現。可変なら UnityEngine.Random でブレを楽しむ。
            Func<int> rng;
            if (useFixedSeed)
            {
                var rnd = new System.Random(seed);
                rng = () => rnd.Next(0, 100);
            }
            else
            {
                rng = () => UnityEngine.Random.Range(0, 100);
            }

            try
            {
                _lastReport = BattleRunner.Run(allies, enemies, maxTurns, rng);
            }
            catch (Exception ex)
            {
                _flash = "戦闘実行中に例外：" + ex.Message;
                Debug.LogException(ex);
                return;
            }

            // 観戦ビュー起動。タイトルは「サンドボックス戦闘」固定で十分。
            _spectator.Initialize(_lastReport, "サンドボックス戦闘");
            _spectator.ShowTargetLines = _spectateSpeed <= 1f && _showTargetLines;

            // 履歴保存（同 Seed 再生のため）
            _lastAllies  = SnapshotChoices(allies);
            _lastEnemies = SnapshotChoices(enemies);
            _lastSeed = seed;
            _lastUsedFixedSeed = useFixedSeed;
            _lastMaxTurns = maxTurns;

            _mode = Mode.Spectating;
            _flash = $"観戦開始（Seed={seed}・最大{maxTurns}T）";
        }

        // ════════════════════════════════════════════════
        // 編成構築
        // ════════════════════════════════════════════════

        private List<RuntimeUnit> BuildParty(int[] choices)
        {
            var list = new List<RuntimeUnit>();
            for (int slot = 0; slot < 6; slot++)
            {
                var factory = UnitChoices[choices[slot]].Factory;
                if (factory == null) continue;
                list.Add(new RuntimeUnit(factory(), slot));
            }
            return list;
        }

        /// <summary>
        /// 再生用に「ユニット ID と SlotIndex だけ」のスナップショットを取って保存。
        /// 戦闘で消費された RuntimeUnit はそのまま再利用できないため、
        /// 再生時に Stage3Roster の Factory から再構築する。
        /// </summary>
        private static List<RuntimeUnit> SnapshotChoices(List<RuntimeUnit> runtimes)
        {
            // ここでは RuntimeUnit 自体を保持しておくが、再構築時には Snapshot 内の
            // BaseUnit.Id を頼りに Factory を引き直す（RebuildFromSnapshot 参照）。
            return new List<RuntimeUnit>(runtimes);
        }

        private static List<RuntimeUnit> RebuildFromSnapshot(List<RuntimeUnit> snapshot)
        {
            var rebuilt = new List<RuntimeUnit>(snapshot.Count);
            foreach (var ru in snapshot)
            {
                // BaseUnit.Id から UnitChoices を引き直す。マッチしなければ
                // BaseUnit をそのままクローン Factory として使えないので、
                // Stage3Roster の Factory を Id 名前一致で総当りで探す。
                Func<Unit> factory = FindFactoryForId(ru.BaseUnit.Id);
                if (factory == null)
                {
                    Debug.LogWarning($"Stage3BattleSpectatorSandbox: Factory not found for unit id={ru.BaseUnit.Id}. 再生スキップ。");
                    continue;
                }
                rebuilt.Add(new RuntimeUnit(factory(), ru.SlotIndex));
            }
            return rebuilt;
        }

        /// <summary>UnitChoices テーブルから該当 Id のファクトリを探す。</summary>
        private static Func<Unit> FindFactoryForId(string id)
        {
            for (int i = 0; i < UnitChoices.Length; i++)
            {
                var f = UnitChoices[i].Factory;
                if (f == null) continue;
                if (f().Id == id) return f;
            }
            return null;
        }

        private static void ApplyPreset(int[] dst, Func<Unit>[] factories)
        {
            for (int i = 0; i < 6; i++)
                dst[i] = FactoryToIndex(factories[i]);
        }

        private static int FactoryToIndex(Func<Unit> factory)
        {
            if (factory == null) return 0;
            // Func の参照比較で確実に引き当てる（同じ static メソッドは同一 delegate）
            for (int i = 0; i < UnitChoices.Length; i++)
                if (UnitChoices[i].Factory == factory) return i;
            return 0;
        }

        // ════════════════════════════════════════════════
        // ユーティリティ
        // ════════════════════════════════════════════════

        private static string ResultJp(BattleResult r)
        {
            switch (r)
            {
                case BattleResult.PerfectVictory:      return "完勝";
                case BattleResult.AdvantageousVictory: return "辛勝";
                case BattleResult.MarginalDefeat:      return "惜敗";
                case BattleResult.CrushingDefeat:      return "完敗";
                default:                               return "—";
            }
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            _header.normal.textColor = Color.white;

            _sub = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            _sub.normal.textColor = new Color(0.85f, 0.85f, 0.85f);

            _wrap = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 12 };
            _wrap.normal.textColor = new Color(0.9f, 0.9f, 0.9f);

            _box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(8, 8, 6, 6) };
        }
    }
}
