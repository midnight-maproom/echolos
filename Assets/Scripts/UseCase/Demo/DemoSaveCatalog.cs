// R4 ショートカット用セーブの集約。
//
// タイトル画面の「救出戦から始める（R4）」ボタンが本カタログを参照し、
// ラン中の一時状態（手駒・マップ状態・CurrentRound）だけを構築してラン開始する。
// メタ進行は触らない＝既存セーブを破壊しない。
using System;
using System.Collections.Generic;
using Echolos.Domain.Models;

namespace Echolos.UseCase.Demo
{
    public static class DemoSaveCatalog
    {
        public const string Save2Id = "demo_save_2";

        /// <summary>セーブ 2：救出戦体験用（R4 開始・左列敵領制圧済・手駒 12 体全員 Lv2）。</summary>
        public static DemoSaveDefinition Save2 => _save2.Value;

        private static readonly Lazy<DemoSaveDefinition> _save2 = new Lazy<DemoSaveDefinition>(BuildSave2);

        public static DemoSaveDefinition Get(string id)
        {
            switch (id)
            {
                case Save2Id: return Save2;
                default: throw new ArgumentException($"未知のデモセーブ ID: {id}", nameof(id));
            }
        }

        private static DemoSaveDefinition BuildSave2()
        {
            // R4 開始時点：「あと一手で救出できる手前」状態。
            // 左列敵領は前ラウンドまでに制圧済＝ R4 で左列敵拠点に攻め込んで救出戦。
            // R4 開始時にラウンド開始演出なし（R2 B-a / R3 B-b1 / R5 B-b2 / R6 B-c のいずれも該当せず）。
            // R5 B-b2 を経由しないので 330 §3.4 の IsBalduinSurrendered ガードで B-c も発火しない。
            // 手駒 12 体は通常進行で R4 開始まで到達した想定（1 周目固定編成 6 体＋ R1〜3 で自動加入 3＋召集 3）。
            return new DemoSaveDefinition(
                id: Save2Id,
                displayName: "セーブ 2：救出戦体験",
                startRound: 4,
                nodeStates: new[]
                {
                    // 左列敵領 (col=0, layer=2=LayerEnemyTerritory) を制圧済
                    new DemoNodeStateEntry(col: 0, layer: 2, isCaptured: true),
                },
                initialRoster: BuildSave2Roster());
        }

        private static IReadOnlyList<DemoRosterEntry> BuildSave2Roster()
        {
            return new[]
            {
                new DemoRosterEntry(UniqueUnitIds.Princess, 2),
                new DemoRosterEntry("fire_swordsman",  2),
                new DemoRosterEntry("fire_archer",     2),
                new DemoRosterEntry("fire_tank",       2),
                new DemoRosterEntry("fire_buffer",     2),
                new DemoRosterEntry("water_swordsman", 2),
                new DemoRosterEntry("water_archer",    2),
                new DemoRosterEntry("water_tank",      2),
                new DemoRosterEntry("water_buffer",    2),
                new DemoRosterEntry("water_healer",    2),
                new DemoRosterEntry("light_paladin",   2),
                new DemoRosterEntry("light_priest",    2),
            };
        }
    }
}
