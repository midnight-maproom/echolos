// チュートリアル／ガイドのための表示用データ集約。
//
// 設計方針：
// - フェーズごとの操作説明と、ユニット詳細カード用の役割解説をここに集める。
// - Unit/Waza モデルは「内部処理＋永続データ」、本クラスは「UI 表示用文字列」を担当して分離する。
// - 主要な兵種ロール・技ターゲット種別のラベル化もここに集める。
// - ローカライズ予定がないため、内容はすべて日本語の固定文字列で良い。
using System.Collections.Generic;
using Echolos.Domain.Battle.Skills;
using Echolos.Domain.Models;

namespace Echolos.Presentation.Common
{
    /// <summary>ユニット1体ぶんの表示用補足情報。Roster の docstring を要約したもの。</summary>
    public sealed class UnitGuide
    {
        public string RoleLabel { get; set; }       // 「前衛タンク」「物理アタッカー」など短ラベル
        public string Summary { get; set; }         // 1〜2行の役割説明
        public string Recommendation { get; set; }  // 「〇〇に有効」のような短い推し文
    }

    /// <summary>OnGUI のガイド文言をまとめた静的辞書。</summary>
    public static class GuideContent
    {
        // ══════════════════════════════════════════════
        // フェーズヘルプ（A案：各フェーズ冒頭の操作説明）
        // ══════════════════════════════════════════════

        public const string HelpInitialDraft =
            "ゲーム開始時に3択ドラフトを3回繰り返して、合計3体の初期メンバーを選びます。\n" +
            "「前衛で殴る役・耐えてかばう役・後衛で支える役」をどう揃えるかが最初の判断。";

        public const string HelpRoundDraft =
            "ラウンド開始時に3択ドラフトで1体追加。R6は★レア確定。\n" +
            "今ラウンドの敵編成（偵察前は不明）と現在のロスター補強の優先度で選ぶ。";

        public const string HelpInterior =
            "行動力2を消費して以下4つから2回選ぶ（同じアクション禁止）。\n" +
            "・偵察：3戦線すべての敵編成を可視化（強く推奨）\n" +
            "・拠点強化：選んだ戦線の拠点 Lv↑（Lv1=守↑、Lv2=攻↑、Lv3=さらに重バフ）\n" +
            "・兵種強化：手駒の同種別を Lv2 まで強化（HP/ATK/防御 等が永続+）\n" +
            "・招集：3択ドラフトで1体追加（行動力2フル消費）";

        public const string HelpAssignment =
            "手駒を戦線スロットに配置。手駒→配置スロットの2クリックで完了。\n" +
            "前列(0,1,2)・後列(3,4,5)、レーンは(0,3)(1,4)(2,5)。前列が後列をかばう。\n" +
            "「配置しない＝完全放置」も可（敗北扱いで点数+2）。完全放置で時間稼ぎという戦術もある。";

        public const string HelpSpectating =
            "戦線ごとに戦闘を順に観戦。速度トグル（ステップ/×1/×2/×4）で進行を調整。\n" +
            "ステップモードはじっくり観察用。「すべてスキップ」で結果へ直行。";

        public const string HelpBattleResult =
            "各戦線の獲得点数を確認。点数上限（平原6・街4・砦2）に達するとその戦線は決着。\n" +
            "R7のボス戦に勝利で勝利、戦線が決着前に上限超え・ボスに敗北は敗北。";

        /// <summary>フェーズ名（PhaseLabel と同じ文字列）→ヘルプテキスト</summary>
        public static string GetPhaseHelp(string phaseLabel)
        {
            switch (phaseLabel)
            {
                case "初期ドラフト":   return HelpInitialDraft;
                case "ラウンドドラフト": return HelpRoundDraft;
                case "内政":           return HelpInterior;
                case "配置":           return HelpAssignment;
                case "戦闘観戦":       return HelpSpectating;
                case "戦闘結果":       return HelpBattleResult;
                default:               return null;
            }
        }

        // ══════════════════════════════════════════════
        // ユニットガイド（ドラフトの詳細カード用）
        // ══════════════════════════════════════════════

        // Unit.Id → ガイド情報の辞書。Roster の docstring を初見プレイヤー向けに短くまとめている。
        private static readonly Dictionary<string, UnitGuide> _unitGuides = new Dictionary<string, UnitGuide>
        {
            // ── 固有キャラ ──
            ["princess"] = new UnitGuide {
                RoleLabel = "固有・前衛＋全軍守護",
                Summary = "物語の主人公。初期手駒に固定加入。騎士よりやや脆い前衛で、戦闘開始時に全味方へ DefenseUp の置物オーラを付与する。軍師の AttackUp と棲み分け。",
                Recommendation = "前列維持の柱。固有なので兵種強化対象外。",
            },
            ["bridget"] = new UnitGuide {
                RoleLabel = "固有・前衛＋攻撃支援",
                Summary = "バルドゥイン拠点で救出すると永続加入する固有ユニット。戦闘開始時に全味方へ AttackUp の置物オーラを付与する（軍師より弱め＝補完役）。",
                Recommendation = "救出成功でアタッカーの火力が一段引き上がる。",
            },

            // ── 通常 Normal 10（前衛 4＋後衛 6）──
            ["tank_def"] = new UnitGuide {
                RoleLabel = "前衛タンク（DEF）",
                Summary = "横列をかばう堅い盾。攻撃不可で殴り合いには参加しない代わりに高い物理防御で前列を守る。",
                Recommendation = "前列を維持したい編成の基礎。",
            },
            ["paladin"] = new UnitGuide {
                RoleLabel = "前衛タンク（攻も）",
                Summary = "横列をかばう＋剣撃で殴れる前衛。重装兵より柔らかいが攻撃力を持つ「殴れる盾」。",
                Recommendation = "重装兵より火力寄りで前列を厚くしたいとき。",
            },
            ["atk_multi"] = new UnitGuide {
                RoleLabel = "物理アタッカー",
                Summary = "2回攻撃の前衛アタッカー。バフ・防デバフとシナジー大。タンクを連撃で削り抜く役。",
                Recommendation = "デバッファー or バッファー併用で本領発揮。",
            },
            ["debuffer"] = new UnitGuide {
                RoleLabel = "デバフ役（火力寄り）",
                Summary = "鎧砕きで物理防御デバフを蓄積（最大6スタック）。長期戦で対タンクを溶かす火力寄りの調整。",
                Recommendation = "堅い前列を突破したいときの定番。",
            },
            ["mercenary"] = new UnitGuide {
                RoleLabel = "前衛物理（孤高の戦士）",
                Summary = "大剣の前衛アタッカー。陣営生存数 3 以下で「孤高の戦士」が発動し ATK/DEF が強化される（3→+10／2→+20／1→+30）。このバフはバフ除去を貫通する。",
                Recommendation = "味方の少ない序盤・編成が薄い局面で本領発揮。",
            },
            ["archer"] = new UnitGuide {
                RoleLabel = "後衛物理（必中）",
                Summary = "遠隔・必中の単体攻撃。回避持ち（帝国の影など）のカウンター。後列から後列まで届く。",
                Recommendation = "回避持ちの敵・後列処理に。",
            },
            ["firemage"] = new UnitGuide {
                RoleLabel = "魔法アタッカー",
                Summary = "中射程・前列単体に火炎弾（魔法）。命中時に燃焼付与。物理タンクの魔防を抜いて削る。",
                Recommendation = "物理特化の前列タンクキラー。",
            },
            ["aoemage"] = new UnitGuide {
                RoleLabel = "全体魔法",
                Summary = "3ターンごとに敵全体にサンダー（チャージ中は完全待機）。一撃の総ダメージは大きいがテンポは遅い。",
                Recommendation = "中〜長期戦で全体を削りたいとき。",
            },
            ["healer"] = new UnitGuide {
                RoleLabel = "後衛回復",
                Summary = "最もHP割合の低い味方を回復。瞬間回復量は中。HP50で脆く、毒や直撃で先に落ちる。",
                Recommendation = "前列が持続的に削られる編成に。",
            },
            ["medic"] = new UnitGuide {
                RoleLabel = "全体小回復",
                Summary = "祈りの雫で全味方を小回復（CD2）。攻撃技なし。司祭の単体大回復と棲み分け、面で受けるダメージのケアに。",
                Recommendation = "敵に全体攻撃（雷魔導士など）が居るときに。",
            },
            ["buffer"] = new UnitGuide {
                RoleLabel = "攻撃バフ",
                Summary = "味方単体の攻撃力を強化（最大3スタック・3ターン）。アタッカー1体の火力を引き上げる。",
                Recommendation = "双剣士・サムライ等の火力を伸ばす。",
            },

            // ── 通常 Rare 3 ──
            ["samurai"] = new UnitGuide {
                RoleLabel = "前衛同列スプラッシュ★",
                Summary = "毎ターン「薙ぎ払い」で単体ターゲット+同列の他の敵に0.8倍の巻き込み。タンク+脆い前列の密集陣形を一気に削る。",
                Recommendation = "敵前列が密集している場合に強い。",
            },
            ["ninja"] = new UnitGuide {
                RoleLabel = "潜入暗殺者★",
                Summary = "高速・かばう貫通+列単位保護貫通の単体攻撃で敵後列まで直接届く。魔導士には常時2倍ダメ。自身は味方タンクに守られない。",
                Recommendation = "後列タンクで守られた敵の魔導士・回復役を狩る役。",
            },
            ["tactician"] = new UnitGuide {
                RoleLabel = "置物★（全体バフ＋2T交互）",
                Summary = "戦闘開始時に全軍へ常時攻撃バフ（軍師が死ぬと剥奪）。2ターン交互に「味方デバフ解除」「敵バフ解除」を発動。攻撃技なし。",
                Recommendation = "デバフ持ちのボス・敵バッファーへの保険。",
            },

            // ══════════════════════════════════════════════
            // 既存敵 3 体
            // ══════════════════════════════════════════════

            ["imperial_scout"] = new UnitGuide {
                RoleLabel = "帝国偵察兵（雑魚）",
                Summary = "帝国軍の偵察任務に就く軽装歩兵。中射程・通常攻撃のみで突出した能力はない。数の有利不利を直感的に体感させる基準ユニット。",
                Recommendation = "—",
            },
            ["boss_baron"] = new UnitGuide {
                RoleLabel = "中ボス：耐久・毒",
                Summary = "全体に毒を蓄積。味方への回復を1/3に減衰するパッシブ持ち。「治しながら耐える」を許さない。6R 中ボスとして自領に襲来。",
                Recommendation = "—",
            },
            ["boss_one_eyed_samurai"] = new UnitGuide {
                RoleLabel = "中ボス：高火力前衛",
                Summary = "3回攻撃＋3ターン毎に真・薙ぎ払い（前列全体・防御割合無視）。状態異常無効。6R 中ボスとして自領に襲来。",
                Recommendation = "—",
            },
            ["boss_prince_dark"] = new UnitGuide {
                RoleLabel = "ラスボス：必敗形態（A-c1）",
                Summary = "闇に染まった皇太子。物理／魔法防御 999 で実質無敵。毎ターン全体物理＋3ターン毎に解除不能 ATK バフを蓄積。3ターン以内に全滅する超絶バフ式の必敗ボス。",
                Recommendation = "ペンダントの力を知らないと出現（A-c1 経路）。",
            },
            ["boss_prince_light"] = new UnitGuide {
                RoleLabel = "ラスボス：最強形態（A-c2）",
                Summary = "闇が祓われた皇太子。3 行動サイクル（鼓舞→破邪の一撃→審判）を CD3 互い違いで繰り返す。鼓舞で取り巻きの攻撃を底上げし、破邪でこちらのバフ解除＋防御デバフ、審判で押し切る。",
                Recommendation = "聖剣強化済（A-c2 経路）なら戦える。",
            },

            // ══════════════════════════════════════════════
            // 新規敵専用（帝国軍 10 体）
            // 性能は対応する味方と同一だが、ガイド文言は敵専用に分離。
            // ══════════════════════════════════════════════

            ["imperial_tank_def"] = new UnitGuide {
                RoleLabel = "帝国重装兵（前衛盾）",
                Summary = "帝国軍の重装歩兵。横列をかばう堅い盾で攻撃しない。突破には長期戦か火力集中が必要。",
                Recommendation = "—",
            },
            ["imperial_paladin"] = new UnitGuide {
                RoleLabel = "帝国騎士（殴れる盾）",
                Summary = "帝国軍の聖騎士。横列かばう＋剣撃で殴り返す前衛。重装兵より柔らかいが反撃力がある。",
                Recommendation = "—",
            },
            ["imperial_atk_multi"] = new UnitGuide {
                RoleLabel = "帝国双剣士（前衛物理）",
                Summary = "帝国軍の双剣使い。2回攻撃で前列を削る。バフ・デバフが乗ると爆発力が増す。",
                Recommendation = "—",
            },
            ["imperial_samurai"] = new UnitGuide {
                RoleLabel = "帝国傭兵（前衛範囲）",
                Summary = "帝国軍に雇われた精鋭剣士。薙ぎ払いで主対象＋同列の脇を巻き込む。前列の集まりを横なぎで一気に削る。",
                Recommendation = "—",
            },
            ["imperial_assassin"] = new UnitGuide {
                RoleLabel = "帝国暗殺者（後列直撃）",
                Summary = "帝国軍直属の暗殺者。高速・疾風刃でかばうと列単位保護を貫通して後列に直撃。魔導士特攻。回復役・魔導士が先に落とされやすい。",
                Recommendation = "—",
            },
            ["imperial_archer"] = new UnitGuide {
                RoleLabel = "帝国弓兵（後衛必中）",
                Summary = "帝国軍の遠隔射手。必中の単体狙撃で回避無効化。後列から後列まで届く。",
                Recommendation = "—",
            },
            ["imperial_firemage"] = new UnitGuide {
                RoleLabel = "帝国炎魔導士（魔法アタッカー）",
                Summary = "帝国軍の魔法部隊所属。中射程・前列単体に火炎弾＋燃焼付与。物理特化の前衛は脆い。",
                Recommendation = "—",
            },
            ["imperial_aoemage"] = new UnitGuide {
                RoleLabel = "帝国大魔導士（全体魔法）",
                Summary = "帝国軍最高峰の大魔導士。3ターンチャージで全体雷撃。チャージ中は無防備だが、放たれると編成が一気に削られる。",
                Recommendation = "—",
            },
            ["imperial_shadow"] = new UnitGuide {
                RoleLabel = "帝国の影（麻痺・回避）",
                Summary = "帝国の影部隊。中射程・回避15%・命中対象を麻痺で止める。前衛タンクを行動不能にされやすい。",
                Recommendation = "—",
            },
            ["imperial_healer"] = new UnitGuide {
                RoleLabel = "帝国司祭（後衛回復）",
                Summary = "帝国軍の従軍司祭。最もHP割合の低い帝国軍を回復。長期戦で帝国軍の継戦力を支える。",
                Recommendation = "—",
            },
        };

        public static UnitGuide GetUnitGuide(string unitId)
        {
            return _unitGuides.TryGetValue(unitId, out var g) ? g : null;
        }

        // ══════════════════════════════════════════════
        // ロスター並び順
        // ══════════════════════════════════════════════

        /// <summary>
        /// ロスターパネルの並び順。固有 → 通常Normal → 通常Rare → 既存敵 → 帝国軍 の順。
        /// 同 ID（兵種強化違い等）は取得順を保つ。
        /// </summary>
        public static readonly List<string> UnitSpecOrder = new List<string>
        {
            // 固有 2
            "princess", "bridget",
            // 通常 Normal 11（前衛 5＋後衛 6）
            "tank_def", "paladin", "atk_multi", "debuffer", "mercenary",
            "archer", "firemage", "aoemage", "healer", "medic", "buffer",
            // 通常 Rare 3
            "samurai", "ninja", "tactician",
            // 既存敵 3＋ラスボス 2（通常はロスターに入らないが念のため末尾）
            "imperial_scout", "boss_baron", "boss_one_eyed_samurai",
            "boss_prince_dark", "boss_prince_light",
            // 帝国軍 10
            "imperial_tank_def", "imperial_paladin", "imperial_atk_multi", "imperial_samurai", "imperial_assassin",
            "imperial_archer", "imperial_firemage", "imperial_aoemage", "imperial_shadow", "imperial_healer",
        };

        /// <summary>仕様書順インデックスを返す。未登録 ID は末尾に並べる（int.MaxValue）。</summary>
        public static int GetSpecOrderIndex(string unitId)
        {
            int idx = UnitSpecOrder.IndexOf(unitId);
            return idx < 0 ? int.MaxValue : idx;
        }

        // ══════════════════════════════════════════════
        // ラベル化ヘルパー
        // ══════════════════════════════════════════════

        public static string AttackKindLabel(AttackKind k)
        {
            switch (k)
            {
                case AttackKind.Melee:  return "近接";
                case AttackKind.Ranged: return "遠隔";
                default:                return "—";
            }
        }

        public static string TargetingDirectionLabel(TargetingDirection d)
        {
            switch (d)
            {
                case TargetingDirection.FromFront: return "前から狙う";
                case TargetingDirection.FromBack:  return "後ろから狙う";
                default:                           return "—";
            }
        }

        public static string TargetingLabel(TargetingType t)
        {
            switch (t)
            {
                case TargetingType.SingleEnemy: return "敵単体";
                case TargetingType.AllEnemies:  return "敵全体";
                case TargetingType.SingleAlly:  return "味方単体";
                case TargetingType.AllAllies:   return "味方全体";
                case TargetingType.Self:        return "自分";
                default:                        return t.ToString();
            }
        }

        public static string ElementLabel(Element e)
        {
            switch (e)
            {
                case Element.Fire:      return "火";
                case Element.Water:     return "水";
                case Element.Ice:       return "氷";
                case Element.Lightning: return "雷";
                case Element.Wind:      return "風";
                case Element.Earth:     return "土";
                case Element.Light:     return "光";
                case Element.Dark:      return "闇";
                case Element.None:      return "無";
                default:                return e.ToString();
            }
        }

        /// <summary>技1つを「名前: ターゲット種別・HitCount・CD」の1行に要約する。</summary>
        public static string WazaOneLineSummary(Waza w)
        {
            if (w == null) return "";
            var sb = new System.Text.StringBuilder();
            sb.Append(w.Name);
            sb.Append("（");
            sb.Append(TargetingLabel(w.TargetingType));
            if (w.HitCount > 1) sb.Append($"・{w.HitCount}回");
            if (w.Cooldown > 0) sb.Append($"・CD{w.Cooldown}");
            sb.Append("）");
            return sb.ToString();
        }
    }
}
