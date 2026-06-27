// Assets/Scripts/UnityView/DebugCampaignStarter.cs
// プロト 段階2-Inc3: 多戦線ミニループ（H2）の検証用デバッグUI（OnGUI/IMGUI）
//
// 【方針】Canvas・prefab・シーン配線を一切使わず、空GameObjectに本コンポーネントを付けて
// Play するだけで多戦線ループを操作できる「使い捨て検証ハーネス」。本番UIは後段で別途作る。
//
// 1ターンの流れ（仕様 120_prototype_spec.md §5.2）:
//   内政（偵察/防衛強化/強化＝行動力消費）→ 師団を各戦線へ配置 → 解決 → 次ターン
// H2の核：手駒6 < 全戦線を強く守る数。偵察しないと敵強度は不明。捨てた戦線は本拠地HPを削る。
using System.Collections.Generic;
using UnityEngine;
using Echolos.Domain.Models;
using Echolos.Domain.Prototype;

using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Story;
using Echolos.Presentation.Common;
using Echolos.Presentation.Battle;
using Echolos.Presentation.Story;
namespace Echolos.Presentation
{
    /// <summary>多戦線ミニループをOnGUIで操作・観戦するデバッグコントローラ。</summary>
    public sealed class DebugCampaignStarter : MonoBehaviour
    {
        private CampaignManager _mgr;
        private CampaignState _state;
        private BattleFrontResolver _resolver;

        private int _selectedFrontIndex;
        private bool _resolved;                       // 当ターンを解決して結果表示中か
        private List<FrontResolution> _lastReports;
        private string _flash = "";

        private Vector2 _scroll;
        private GUIStyle _header, _box, _wrap;

        private void Start() => NewCampaign();

        private void NewCampaign()
        {
            _state = PrototypeCampaign.Build();
            _mgr = new CampaignManager();
            _resolver = new BattleFrontResolver();
            _mgr.StartCampaign(_state);
            _selectedFrontIndex = 0;
            _resolved = false;
            _lastReports = null;
            _flash = "戦線を偵察し、手駒をどこに送るか決めて『このターンを解決』。";
        }

        // ──────────────────────────────────────────────
        // 描画
        // ──────────────────────────────────────────────

        private void OnGUI()
        {
            EnsureStyles();

            GUILayout.BeginArea(new Rect(10, 10, Screen.width - 20, Screen.height - 20));
            _scroll = GUILayout.BeginScrollView(_scroll);

            DrawHeader();

            if (_state.Result != CampaignResult.None) DrawEndScreen();
            else if (_resolved) DrawResolvedScreen();
            else DrawPlanningScreen();

            if (!string.IsNullOrEmpty(_flash))
            {
                GUILayout.Space(6);
                GUILayout.Label("▶ " + _flash, _wrap);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawHeader()
        {
            GUILayout.Label("◆ 多戦線ミニループ（H2検証）", _header);
            GUILayout.Label(
                $"ターン {_state.CurrentTurn}/{_state.MaxTurns}　｜　"
                + $"本拠地HP {_state.HomeBaseHP}/{_state.HomeBaseMaxHP}　｜　"
                + $"行動力 {_state.ActionPoints}/{_mgr.Config.ActionPointsPerTurn}");
            GUILayout.Space(4);
        }

        // ── 計画フェーズ（内政＋配置）──
        private void DrawPlanningScreen()
        {
            GUILayout.Label("【戦線】選択中の戦線へ手駒を配置できます。偵察しないと敵編成は見えません。", _wrap);

            GUILayout.BeginHorizontal();
            for (int i = 0; i < _state.Fronts.Count; i++) DrawFrontColumn(i);
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            DrawRosterPool();

            GUILayout.Space(10);
            if (GUILayout.Button("▶ このターンを解決する", GUILayout.Height(34)))
                ResolveCurrentTurn();
        }

        private void DrawFrontColumn(int i)
        {
            var f = _state.Fronts[i];
            bool selected = i == _selectedFrontIndex;

            GUILayout.BeginVertical(_box, GUILayout.Width(250));

            GUILayout.Label((selected ? "● " : "○ ") + $"[{i}] {f.Name}", _header);
            if (GUILayout.Button(selected ? "選択中" : "この戦線を選択"))
                _selectedFrontIndex = i;

            // 敵編成（偵察済みのみ可視）
            if (f.IsScouted)
            {
                GUILayout.Label("敵編成（偵察済）:", _wrap);
                foreach (var e in f.EnemyDivision)
                    GUILayout.Label($"  ・{e.BaseUnit.Name} HP{e.MaxHP}/ATK{e.BaseUnit.BaseATK}", _wrap);
            }
            else
            {
                GUILayout.Label("敵編成: 未偵察（不明）", _wrap);
            }

            GUILayout.Label($"突破時 本拠地ダメージ: {f.BaseBreakthroughDamage}（守って勝てば0）", _wrap);

            if (GUILayout.Button($"偵察(AP{_mgr.Config.ScoutCost})"))
                _flash = _mgr.Scout(_state, i) ? $"{f.Name}を偵察した。" : "行動力が足りない。";

            // 配置済み師団
            GUILayout.Label("配置:", _wrap);
            if (f.AssignedAllies.Count == 0)
                GUILayout.Label("  （なし＝捨てる）", _wrap);
            foreach (var u in new List<RuntimeUnit>(f.AssignedAllies))
            {
                if (GUILayout.Button($"✕ {u.BaseUnit.Name}"))
                    _mgr.Unassign(_state, u);
            }

            GUILayout.EndVertical();
        }

        private void DrawRosterPool()
        {
            GUILayout.Label($"【手駒】未配置のユニットを選択中の戦線[{_selectedFrontIndex}] {_state.Fronts[_selectedFrontIndex].Name} へ配置：", _wrap);

            foreach (var u in _state.Roster)
            {
                bool resting = _state.RestingUnits.Contains(u);
                bool assigned = IsAssigned(u);

                GUILayout.BeginHorizontal();
                string info = $"{u.BaseUnit.Name}（HP{u.MaxHP}/ATK{u.BaseUnit.BaseATK}/守{u.BaseUnit.PDEF}・{u.BaseUnit.MDEF}）";

                if (resting)
                    GUILayout.Label("　休養中: " + info, _wrap, GUILayout.Width(360));
                else if (assigned)
                    GUILayout.Label("　配置済: " + info, _wrap, GUILayout.Width(360));
                else if (GUILayout.Button("▶ 配置 " + info, GUILayout.Width(360)))
                    _mgr.AssignToFront(_state, u, _selectedFrontIndex);

                // 攻撃強化（恒久ATK+）／守備強化（恒久PDEF・MDEF+）。配置済み以外なら投資可能。
                if (!assigned && GUILayout.Button($"攻強化(AP{_mgr.Config.ReinforceCost})", GUILayout.Width(120)))
                    _flash = _mgr.Reinforce(_state, u)
                        ? $"{u.BaseUnit.Name} 攻撃強化（ATK+{_mgr.Config.ReinforceAtkBonus}）。"
                        : "行動力が足りない。";
                if (!assigned && GUILayout.Button($"守強化(AP{_mgr.Config.FortifyCost})", GUILayout.Width(120)))
                    _flash = _mgr.Fortify(_state, u)
                        ? $"{u.BaseUnit.Name} 守備強化（DEF+{_mgr.Config.FortifyDefBonus}）。"
                        : "行動力が足りない。";

                GUILayout.EndHorizontal();
            }
        }

        // ── 解決結果表示 ──
        private void DrawResolvedScreen()
        {
            GUILayout.Label("【解決結果】", _header);
            if (_lastReports != null)
            {
                foreach (var r in _lastReports)
                {
                    string status = !r.Defended ? "無防備→突破"
                                  : r.Held ? "維持" : "突破";
                    string dmg = r.BreakthroughDamage > 0 ? $"　本拠地-{r.BreakthroughDamage}" : "";
                    string downed = r.DownedAllies.Count > 0 ? $"　戦闘不能{r.DownedAllies.Count}" : "";
                    GUILayout.Label($"・{r.FrontName}: {status}（{ResultJp(r.BattleResult)}）{dmg}{downed}", _wrap);
                }
            }

            GUILayout.Space(8);
            if (GUILayout.Button("▶ 次のターンへ", GUILayout.Height(30)))
            {
                _mgr.AdvanceTurn(_state);
                _resolved = false;
                _flash = $"ターン{_state.CurrentTurn}開始。行動力が回復した。";
            }
        }

        // ── 終了画面 ──
        private void DrawEndScreen()
        {
            string msg = _state.Result == CampaignResult.Clear
                ? "★ クリア！ 規定ターン、本拠地を守り抜いた。"
                : "× 敗北… 本拠地が陥落した。";
            GUILayout.Label(msg, _header);

            if (_lastReports != null)
            {
                GUILayout.Label("最終ターンの結果:", _wrap);
                foreach (var r in _lastReports)
                {
                    string status = !r.Defended ? "無防備→突破" : r.Held ? "維持" : "突破";
                    GUILayout.Label($"・{r.FrontName}: {status}", _wrap);
                }
            }

            GUILayout.Space(8);
            if (GUILayout.Button("▶ 最初からやり直す", GUILayout.Height(30)))
                NewCampaign();
        }

        // ──────────────────────────────────────────────
        // 操作・ユーティリティ
        // ──────────────────────────────────────────────

        private void ResolveCurrentTurn()
        {
            _lastReports = _mgr.ResolveTurn(_state, _resolver);
            _resolved = true;
            _flash = "結果を確認して『次のターンへ』。";
        }

        private bool IsAssigned(RuntimeUnit u)
        {
            foreach (var f in _state.Fronts)
                if (f.AssignedAllies.Contains(u)) return true;
            return false;
        }

        private static string ResultJp(BattleResult r)
        {
            switch (r)
            {
                case BattleResult.PerfectVictory: return "完勝";
                case BattleResult.AdvantageousVictory: return "辛勝";
                case BattleResult.MarginalDefeat: return "惜敗";
                case BattleResult.CrushingDefeat: return "完敗";
                default: return "—";
            }
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, wordWrap = true };
            _wrap = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            _box = new GUIStyle(GUI.skin.box) { fontSize = 13, alignment = TextAnchor.UpperLeft, padding = new RectOffset(8, 8, 8, 8) };
            GUI.skin.button.fontSize = 13;
            GUI.skin.button.wordWrap = true;
        }
    }
}
