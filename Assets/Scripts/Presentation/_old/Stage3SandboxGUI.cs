// Assets/Scripts/UnityView/Stage3SandboxGUI.cs
// 段階3 バトルサンドボックス：OnGUIで編成を組み替え、1戦闘を実行してログを観察するハーネス。
//
// 【目的】
// - 4マッチアップNUnitテストの固定編成に縛られず、味方/敵の各スロットを自由に組み替えて
//   「他の敵パターン（弱/中）」をその場で試作するための遊び場。
// - 戦闘ロジック本体は BattleRunner.Run() をそのまま流用するので、テストと実機で
//   挙動が一致する（バグ再現・統計取りの土台もここから派生させる想定）。
//
// 【方針】
// - Canvas・Prefabは使わず、空GameObjectにこのコンポーネントを付けて Play するだけで動く。
// - 編成はスロットごとに [<] [ユニット名] [>] のサイクル選択（None含む18択）。
// - プリセット6種（ボス想定編成2＋味方検証編成4）を敵味方それぞれ独立に適用できる。
//   味方どうしの対戦（4種総当たり）や同編成ミラーマッチも検証可能。
// - 乱数は Seed 固定 or System ランダムを選べる（固定にすれば NUnit と同じログを再現可能）。
// - 「初期ドラフト試作」ボタンで通常プール12体から3択×3回を実行し、味方slot0-2に編成する。
//   3回目は前衛セーフティネット（それまで前衛0体なら前衛のみで3択を構成）を適用。レア抽選やラウンドドラフトは未実装の最小スコープ。
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
    /// <summary>段階3 バトル単体検証のサンドボックスUI（OnGUI/IMGUI）。</summary>
    public sealed class Stage3SandboxGUI : MonoBehaviour
    {
        // ── ユニット選択肢 ──
        // インデックス0は空きスロット。1〜15が通常ユニット、16〜17が2ボス。
        // 順序は Stage3Roster.cs の宣言順を踏襲。
        private static readonly (string Label, Func<Unit> Factory)[] UnitChoices =
        {
            ("(空き)",         null),
            ("重装兵",         Stage3Roster.GeneralTank),
            ("大盾兵",         Stage3Roster.HpTank),
            ("騎士",         Stage3Roster.Paladin),
            ("双剣士",         Stage3Roster.Attacker1),
            ("サムライ",       Stage3Roster.Samurai),
            ("大槌兵",         Stage3Roster.Debuffer),
            ("アサシン",         Stage3Roster.Assassin),
            ("弓兵",           Stage3Roster.Archer),
            ("忍者",           Stage3Roster.Ninja),
            ("炎魔導士",       Stage3Roster.FireMage),
            ("雷魔導士",       Stage3Roster.AoeMage),
            ("司祭",           Stage3Roster.Healer1),
            ("巫女",         Stage3Roster.Healer2),
            ("踊り子",         Stage3Roster.Buffer),
            ("軍師",           Stage3Roster.Tactician),
            ("[ボス]男爵",    Stage3Roster.PoisonBaron),
            ("[ボス]サムライ", Stage3Roster.OneEyedSamurai),
            ("[敵専用]散兵", Stage3Roster.Skirmisher),
        };

        // ── 編成プリセット（敵味方どちらにも適用可能）──
        // 上2つはボス想定編成、下4つは NUnit テストの味方検証編成。
        // どれも slot[0..2]=前列, slot[3..5]=後列。スロット内容は Stage3MatchupTests.cs と完全一致。
        //
        // スロットは Func<Unit>（Stage3Roster のファクトリメソッド）参照で持つ＝表示ラベルや
        // ユニット名を変更してもこの定義は触らなくて良い。
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
        };

        // ── ドラフト試作用：通常プール12体と前衛判定（§10.5・サンドボックス専用の最小実装）──
        // レア3体（騎士・忍者・軍師）と敵専用（散兵・ボス2体）と空きは除外して通常プールを定義。
        // 前衛セーフティネット判定用に「前衛ユニットのラベル集合」もここで持つ。
        private static readonly string[] NormalPoolLabels =
        {
            "重装兵", "大盾兵", "双剣士", "サムライ", "大槌兵", "アサシン",
            "弓兵", "炎魔導士", "雷魔導士", "司祭", "巫女", "踊り子",
        };
        private static readonly HashSet<string> FrontUnitLabels = new HashSet<string>
        {
            "重装兵", "大盾兵", "双剣士", "サムライ", "大槌兵", "アサシン",
            // 騎士はレア除外なので通常プールには出ないが、前衛判定としては前衛
            "騎士",
        };

        // 各スロットの選択インデックス（UnitChoices に対するindex）
        private readonly int[] _allyChoice = new int[6];
        private readonly int[] _enemyChoice = new int[6];

        // 戦闘パラメータ
        private int _maxTurns = 15;
        private string _seedInput = "0";
        private bool _useFixedSeed = true;

        // 結果保持
        private string _logText = "編成を組んで『戦闘開始』を押してください。";
        private string _resultLabel = "";
        private Vector2 _logScroll;
        private Vector2 _mainScroll;

        // ── ドラフト試作の状態 ──
        // _draftMode が true の間は OnGUI 上部にドラフト3択を表示。
        // _draftStep は 0/1/2 で「何回目のドラフトか」を保持。
        // _draftCandidates は現在提示されている3候補（UnitChoices インデックス）。
        // _draftRng はドラフト専用の乱数源（テストで Seed 固定したい場合に流用可）。
        private bool _draftMode;
        private int _draftStep;
        private int[] _draftCandidates;
        private System.Random _draftRng;
        private string _draftMessage = "";

        // スタイル
        private GUIStyle _header, _sub, _wrap, _box, _logStyle;

        // ──────────────────────────────────────────────
        // Unity ライフサイクル
        // ──────────────────────────────────────────────

        private void Start()
        {
            // 初期は「サムライ戦・硬前列遠隔(勝想定)」 vs 「サムライPT」をセット（テストと同条件）。
            ApplyPreset(_allyChoice,  Presets[4].Factories); // サムライ戦・硬前列遠隔
            ApplyPreset(_enemyChoice, Presets[1].Factories); // サムライPT
        }

        // ──────────────────────────────────────────────
        // 描画
        // ──────────────────────────────────────────────

        private void OnGUI()
        {
            // WebGL ビルドでも日本語が崩れないよう、フォントを毎フレーム適用する
            // （配置されていなければ何もせず素通り）。
            GuiTheme.EnsureJapaneseFont();

            EnsureStyles();

            GUILayout.BeginArea(new Rect(10, 10, Screen.width - 20, Screen.height - 20));
            _mainScroll = GUILayout.BeginScrollView(_mainScroll);

            GUILayout.Label("◆ 段階3 バトルサンドボックス", _header);
            GUILayout.Label("味方/敵の各スロットを自由に組んで1戦闘を回せます。Seed固定にすればNUnitと同条件で再現可能。", _wrap);
            GUILayout.Space(6);

            // ドラフト試作中は専用UIを上に出して通常のプリセット/編成UIをそのまま下に並べる
            if (_draftMode)
            {
                DrawDraftUI();
                GUILayout.Space(6);
            }

            DrawPresetRow();
            GUILayout.Space(6);

            DrawPartiesRow();
            GUILayout.Space(8);

            DrawRunRow();
            GUILayout.Space(8);

            DrawLog();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // ── プリセット行 ──
        // 各編成はラベル右の「→味方」「→敵」で適用先を選べる。
        // これで味方どうしの対戦（4種総当たり）や同編成ミラーマッチも検証可能。
        private void DrawPresetRow()
        {
            GUILayout.BeginVertical(_box);
            GUILayout.Label("【プリセット】どの編成も味方/敵どちらにも適用できます。", _sub);

            foreach (var preset in Presets)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("● " + preset.Label, _wrap, GUILayout.Width(280));
                if (GUILayout.Button("→ 味方", GUILayout.Width(70)))
                    ApplyPreset(_allyChoice, preset.Factories);
                if (GUILayout.Button("→ 敵", GUILayout.Width(70)))
                    ApplyPreset(_enemyChoice, preset.Factories);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("味方を消去", GUILayout.Width(100)))
                ClearChoices(_allyChoice);
            if (GUILayout.Button("敵を消去", GUILayout.Width(100)))
                ClearChoices(_enemyChoice);
            if (GUILayout.Button("両方消去", GUILayout.Width(100)))
                ClearAll();
            GUILayout.Space(20);
            GUI.enabled = !_draftMode;
            if (GUILayout.Button("▶ 初期ドラフト試作（味方 slot0-2）", GUILayout.Width(280)))
                StartInitialDraft();
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        // ── ドラフトUI ──
        // 3択の候補をカード形式で並べ、選択ボタンを押すと味方 slot に反映する。
        // 3回終わるとドラフトモードを抜ける。
        private void DrawDraftUI()
        {
            GUILayout.BeginVertical(_box);
            GUILayout.Label($"【ドラフト試作】 {_draftStep + 1} / 3 回目（味方 slot{_draftStep}）", _sub);
            GUILayout.Label(_draftMessage, _wrap);
            GUILayout.Space(4);

            GUILayout.BeginHorizontal();
            for (int i = 0; i < _draftCandidates.Length; i++)
            {
                int choiceIdx = _draftCandidates[i];
                var label = UnitChoices[choiceIdx].Label;
                var factory = UnitChoices[choiceIdx].Factory;

                GUILayout.BeginVertical(_box, GUILayout.Width(260));
                GUILayout.Label(label, _sub);
                if (factory != null)
                {
                    var u = factory();
                    string side = FrontUnitLabels.Contains(label) ? "前衛" : "後衛";
                    GUILayout.Label($"想定配置: {side}", _wrap);
                    GUILayout.Label($"HP {u.MaxHP} / ATK {u.BaseATK} / PDEF {u.PDEF} / MDEF {u.MDEF} / SPD {u.BaseSPD}", _wrap);
                }
                if (GUILayout.Button("▶ これを選ぶ", GUILayout.Height(28)))
                {
                    PickDraftCandidate(i);
                }
                GUILayout.EndVertical();
                GUILayout.Space(6);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            if (GUILayout.Button("ドラフトを中止する", GUILayout.Width(180)))
            {
                _draftMode = false;
                _draftMessage = "";
            }

            GUILayout.EndVertical();
        }

        // ──────────────────────────────────────────────
        // ドラフト試作のロジック
        // ──────────────────────────────────────────────

        // 初期3ドラフトを開始する。味方 slot0-2 をクリアし、3択を生成する。
        // ドラフトのRNGは常に時間ベース（戦闘実行用の Seed 設定とは独立）。
        // 押すたびに違う候補が出るようにしないと「何回試しても同じ結果」になってしまうため。
        private void StartInitialDraft()
        {
            ClearChoices(_allyChoice);
            _draftMode = true;
            _draftStep = 0;
            _draftRng = new System.Random();
            GenerateNextDraftCandidates();
            _draftMessage = "通常プール12体から3択で1体を選んでください。3回目で前衛が0体なら自動で前衛のみの3択になります。";
        }

        // 次の3択候補を生成する。
        // 3回目（_draftStep == 2）かつそれまでに前衛0体なら、通常プールの前衛のみで構成する。
        private void GenerateNextDraftCandidates()
        {
            bool needFrontOnly = (_draftStep == 2) && !HasAnyFrontInSlots(0, _draftStep);

            // 通常プールから抽選対象のラベル群を準備
            var pool = new List<string>(NormalPoolLabels);
            if (needFrontOnly)
                pool.RemoveAll(label => !FrontUnitLabels.Contains(label));

            // 重複なしで3つ抽選
            _draftCandidates = new int[3];
            for (int i = 0; i < 3; i++)
            {
                int idx = _draftRng.Next(pool.Count);
                string label = pool[idx];
                pool.RemoveAt(idx);
                _draftCandidates[i] = LabelToUnitChoiceIndex(label);
            }
        }

        // 候補 i を選択して味方 slot に反映、次のステップへ進む。
        private void PickDraftCandidate(int candidateIndex)
        {
            int unitChoiceIdx = _draftCandidates[candidateIndex];
            _allyChoice[_draftStep] = unitChoiceIdx;
            _draftStep++;

            if (_draftStep >= 3)
            {
                _draftMode = false;
                _draftMessage = "";
                _resultLabel = "ドラフト完了：味方 slot0-2 に編成済み";
            }
            else
            {
                GenerateNextDraftCandidates();
                bool willBeFrontOnly = (_draftStep == 2) && !HasAnyFrontInSlots(0, _draftStep);
                _draftMessage = willBeFrontOnly
                    ? $"これまでに前衛0体のため、3回目は前衛のみの3択を提示しています。"
                    : "通常プール12体から3択で1体を選んでください。";
            }
        }

        // 味方の指定 slot 範囲 [start, end) に前衛ユニットが含まれているか。
        private bool HasAnyFrontInSlots(int start, int end)
        {
            for (int slot = start; slot < end; slot++)
            {
                int idx = _allyChoice[slot];
                if (idx == 0) continue; // 空き
                string label = UnitChoices[idx].Label;
                if (FrontUnitLabels.Contains(label)) return true;
            }
            return false;
        }

        // 通常プールのラベルから UnitChoices のインデックスを引く。
        private static int LabelToUnitChoiceIndex(string label)
        {
            for (int i = 0; i < UnitChoices.Length; i++)
                if (UnitChoices[i].Label == label) return i;
            return 0;
        }

        // ── 編成行（左：味方、右：敵）──
        private void DrawPartiesRow()
        {
            GUILayout.BeginHorizontal();

            DrawPartyColumn("【味方】", _allyChoice);
            GUILayout.Space(10);
            DrawPartyColumn("【敵】", _enemyChoice);

            GUILayout.EndHorizontal();
        }

        private void DrawPartyColumn(string title, int[] choices)
        {
            GUILayout.BeginVertical(_box, GUILayout.Width(380));
            GUILayout.Label(title, _sub);

            // スロット 0,1,2 が前列、3,4,5 が後列
            DrawRowHeader("前列");
            for (int slot = 0; slot < 3; slot++) DrawSlotRow(slot, choices);
            GUILayout.Space(4);
            DrawRowHeader("後列");
            for (int slot = 3; slot < 6; slot++) DrawSlotRow(slot, choices);

            GUILayout.EndVertical();
        }

        private void DrawRowHeader(string label)
        {
            GUILayout.Label(label, _sub);
        }

        private void DrawSlotRow(int slot, int[] choices)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"slot{slot}:", GUILayout.Width(50));

            if (GUILayout.Button("◀", GUILayout.Width(30)))
                choices[slot] = (choices[slot] - 1 + UnitChoices.Length) % UnitChoices.Length;

            GUILayout.Label(UnitChoices[choices[slot]].Label, _wrap, GUILayout.Width(190));

            if (GUILayout.Button("▶", GUILayout.Width(30)))
                choices[slot] = (choices[slot] + 1) % UnitChoices.Length;

            // 簡易ステータス（HP/ATK）も右側に表示するとデバッグ時に便利
            var factory = UnitChoices[choices[slot]].Factory;
            if (factory != null)
            {
                var u = factory();
                GUILayout.Label($"HP{u.MaxHP} ATK{u.BaseATK}", _wrap, GUILayout.Width(110));
            }
            else
            {
                GUILayout.Label("", GUILayout.Width(110));
            }

            GUILayout.EndHorizontal();
        }

        // ── 実行行 ──
        private void DrawRunRow()
        {
            GUILayout.BeginHorizontal(_box);

            GUILayout.Label("ターン上限:", _sub, GUILayout.Width(80));
            var turnsStr = GUILayout.TextField(_maxTurns.ToString(), GUILayout.Width(50));
            if (int.TryParse(turnsStr, out var t) && t > 0 && t <= 100) _maxTurns = t;

            GUILayout.Space(20);

            _useFixedSeed = GUILayout.Toggle(_useFixedSeed, " Seed固定", GUILayout.Width(90));
            GUI.enabled = _useFixedSeed;
            _seedInput = GUILayout.TextField(_seedInput, GUILayout.Width(80));
            GUI.enabled = true;

            GUILayout.Space(20);

            if (GUILayout.Button("▶ 戦闘開始", GUILayout.Width(120), GUILayout.Height(28)))
                RunBattle();

            if (GUILayout.Button("ログクリア", GUILayout.Width(100), GUILayout.Height(28)))
            {
                _logText = "";
                _resultLabel = "";
            }

            GUILayout.FlexibleSpace();
            if (!string.IsNullOrEmpty(_resultLabel))
                GUILayout.Label(_resultLabel, _sub);

            GUILayout.EndHorizontal();
        }

        // ── ログ表示 ──
        private void DrawLog()
        {
            GUILayout.Label("【戦闘ログ】", _sub);
            _logScroll = GUILayout.BeginScrollView(_logScroll, _box, GUILayout.Height(420));
            // TextArea にすればコピー可能。読みやすさのため等幅をやめて wrap ラベルで表示する選択もあるが、
            // 検証中はコピーして外に貼れる方が便利なので TextArea を採用。
            GUILayout.TextArea(_logText, _logStyle, GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();
        }

        // ──────────────────────────────────────────────
        // 戦闘実行
        // ──────────────────────────────────────────────

        private void RunBattle()
        {
            var allies = BuildParty(_allyChoice);
            var enemies = BuildParty(_enemyChoice);

            if (allies.Count == 0 || enemies.Count == 0)
            {
                _logText = "味方・敵それぞれ1体以上配置してください。";
                _resultLabel = "";
                return;
            }

            Func<int> rng;
            if (_useFixedSeed)
            {
                int seed = int.TryParse(_seedInput, out var s) ? s : 0;
                var rnd = new System.Random(seed);
                rng = () => rnd.Next(0, 100);
            }
            else
            {
                rng = () => UnityEngine.Random.Range(0, 100);
            }

            try
            {
                var report = BattleRunner.Run(allies, enemies, _maxTurns, rng);
                _logText = report.LogText;
                _resultLabel = $"結果: {ResultJp(report.Result)}（{report.Turns}T）";
                _logScroll = Vector2.zero; // 戦闘開始から読みたいので先頭へ
            }
            catch (Exception ex)
            {
                _logText = "戦闘実行中に例外:\n" + ex;
                _resultLabel = "例外発生";
                Debug.LogException(ex);
            }
        }

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

        // ──────────────────────────────────────────────
        // プリセット
        // ──────────────────────────────────────────────

        // プリセットを指定側（味方 or 敵）に適用する。
        // dst は _allyChoice か _enemyChoice の参照。factories は Presets[].Factories（6要素のファクトリ配列）。
        // 各 factory を UnitChoices 内のインデックスに引き直して、選択状態として保存する。
        private static void ApplyPreset(int[] dst, Func<Unit>[] factories)
        {
            for (int i = 0; i < 6; i++)
                dst[i] = FactoryToIndex(factories[i]);
        }

        private void ClearChoices(int[] choices)
        {
            for (int i = 0; i < 6; i++) choices[i] = 0;
            _resultLabel = "";
        }

        private void ClearAll()
        {
            ClearChoices(_allyChoice);
            ClearChoices(_enemyChoice);
        }

        // Func<Unit> 参照を UnitChoices のインデックスに引き直す。
        // ラベル文字列に依存しないので、表示名を変えても Presets 側の修正は不要。
        private static int FactoryToIndex(Func<Unit> factory)
        {
            for (int i = 0; i < UnitChoices.Length; i++)
                if (UnitChoices[i].Factory == factory) return i;
            return 0; // 不一致は空き扱い
        }

        // ──────────────────────────────────────────────
        // ユーティリティ
        // ──────────────────────────────────────────────

        private static string ResultJp(BattleResult r)
        {
            switch (r)
            {
                case BattleResult.PerfectVictory:      return "完勝";
                case BattleResult.AdvantageousVictory: return "辛勝";
                case BattleResult.MarginalDefeat:      return "惜敗";
                case BattleResult.CrushingDefeat:      return "完敗";
                default:                               return "未決着";
            }
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, wordWrap = true };
            _sub    = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, wordWrap = true };
            _wrap   = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            _box    = new GUIStyle(GUI.skin.box)   { fontSize = 12, alignment = TextAnchor.UpperLeft, padding = new RectOffset(8, 8, 8, 8) };
            _logStyle = new GUIStyle(GUI.skin.textArea) { fontSize = 12, wordWrap = false };
            GUI.skin.button.fontSize = 12;
            GUI.skin.button.wordWrap = true;
        }
    }
}
