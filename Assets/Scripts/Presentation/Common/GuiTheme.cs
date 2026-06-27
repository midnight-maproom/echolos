// OnGUI スタイル磨きの中央集約テーマ。
//
// 設計方針：
// - GUIStyle は GUI.skin が初期化された後（最初の OnGUI 内）でないと組み立てられないため、
//   静的フィールドに直接持たず、遅延初期化プロパティで提供する。
// - 色パレットと書体サイズはここで一元管理し、ビュー側からは GuiTheme.Title 等で参照する。
// - フェーズの色分け（内政＝青、配置＝橙、戦闘観戦＝赤など）も Palette として提供する。
// - GUIStyle の deepcopy は重いため、ビューが頻繁に書き換える場合（フォントカラー等）は
//   呼び出し側で都度コピーする。
using UnityEngine;

using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Story;
using Echolos.Presentation.Common;
using Echolos.Presentation.Battle;
using Echolos.Presentation.Story;
namespace Echolos.Presentation.Common
{
    /// <summary>OnGUI 共通テーマ。色とスタイルを集中管理する。</summary>
    public static class GuiTheme
    {
        //
        // 色パレット
        //

        /// <summary>パネル背景：濃い青みグレー（やや透過）。</summary>
        public static readonly Color PanelBg = new Color(0.10f, 0.12f, 0.16f, 0.92f);
        /// <summary>強調パネル背景：少し明るい青みグレー。</summary>
        public static readonly Color PanelBgRaised = new Color(0.18f, 0.22f, 0.28f, 0.95f);
        /// <summary>本文文字色：明るいオフホワイト。</summary>
        public static readonly Color TextPrimary = new Color(0.95f, 0.96f, 0.98f, 1f);
        /// <summary>補助文字色：薄いグレー（説明・サブテキスト）。</summary>
        public static readonly Color TextMuted = new Color(0.70f, 0.74f, 0.80f, 1f);

        /// <summary>アクセント（主操作の青）。</summary>
        public static readonly Color AccentPrimary = new Color(0.30f, 0.62f, 0.95f, 1f);
        /// <summary>成功色（緑）。偵察済・完勝等。</summary>
        public static readonly Color AccentSuccess = new Color(0.40f, 0.80f, 0.45f, 1f);
        /// <summary>警告色（橙）。配置フェーズ・辛勝等。</summary>
        public static readonly Color AccentWarning = new Color(0.95f, 0.65f, 0.25f, 1f);
        /// <summary>危険色（赤）。観戦中・完敗・休養中等。</summary>
        public static readonly Color AccentDanger = new Color(0.90f, 0.35f, 0.40f, 1f);

        /// <summary>各フェーズに対応する色。フェーズタグやヘッダのアクセントに使う。</summary>
        public static Color PhaseColor(string phaseLabel)
        {
            switch (phaseLabel)
            {
                case "初期ドラフト":
                case "ラウンドドラフト": return AccentPrimary;
                case "内政":           return new Color(0.55f, 0.85f, 0.95f, 1f); // 水色
                case "配置":           return AccentWarning;
                case "戦闘観戦":       return AccentDanger;
                case "戦闘結果":       return new Color(0.85f, 0.75f, 0.40f, 1f); // 黄金
                case "終了":           return TextMuted;
                default:               return TextPrimary;
            }
        }

        // 日本語フォント（WebGL ビルド対応）

        // Editor 上ではデフォルトフォントが OS のフォントフォールバックで日本語を表示できるが、
        // WebGL ビルドではそれが効かず日本語が「□豆腐」になる。これを防ぐため、
        // `Assets/Resources/Fonts/NotoSansJP-Regular.ttf` を遅延ロードして
        // GUI.skin.font とキャッシュ済 GUIStyle 全てに適用する。
        //
        // フォントが未配置（Resources/Fonts/ にファイルなし）の場合は何もせず素通りするので、
        // Editor 上での既存挙動は維持される。
        private static Font _japaneseFont;
        private static bool _japaneseFontTried;

        /// <summary>
        /// 日本語表示用フォント。`Assets/Resources/Fonts/NotoSansJP-Regular.ttf` を期待する。
        /// 配置がなければ null。最初のアクセス時に1度だけロードを試みる。
        /// </summary>
        public static Font JapaneseFont
        {
            get
            {
                if (!_japaneseFontTried)
                {
                    _japaneseFontTried = true;
                    _japaneseFont = Resources.Load<Font>("Fonts/NotoSansJP-Regular");
                    // フォールバック：別名で配置されている場合の救済
                    if (_japaneseFont == null)
                        _japaneseFont = Resources.Load<Font>("Fonts/Japanese");
                }
                return _japaneseFont;
            }
        }

        /// <summary>
        /// 各 OnGUI / DrawGUI の冒頭で呼ぶ。日本語フォントが配置されていればそれを
        /// GUI.skin.font とキャッシュ済 GUIStyle 全てに適用する。配置がなければ何もしない。
        /// 呼び出しコスト：参照比較のみ（フォントが既に適用済みなら何も書き換えない）。
        /// </summary>
        public static void EnsureJapaneseFont()
        {
            var font = JapaneseFont;
            if (font == null) return;

            if (GUI.skin.font != font) GUI.skin.font = font;

            // 既に lazy init されたカスタム GUIStyle 群にもフォントを反映する
            // （初回フレームでフォントロードより前にスタイルが構築されたケースの保険）。
            if (_title != null         && _title.font != font)         _title.font = font;
            if (_subtitle != null      && _subtitle.font != font)      _subtitle.font = font;
            if (_body != null          && _body.font != font)          _body.font = font;
            if (_muted != null         && _muted.font != font)         _muted.font = font;
            if (_stat != null          && _stat.font != font)          _stat.font = font;
            if (_panel != null         && _panel.font != font)         _panel.font = font;
            if (_primaryButton != null && _primaryButton.font != font) _primaryButton.font = font;
        }

        //
        // GUIStyle（遅延初期化）
        //

        private static GUIStyle _title;
        /// <summary>大見出し（24pt 太字・主色）。ヘッダのラウンド表示等に使う。</summary>
        public static GUIStyle Title
        {
            get
            {
                if (_title == null)
                {
                    _title = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 24,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleLeft,
                        padding = new RectOffset(8, 8, 4, 4),
                    };
                    _title.normal.textColor = TextPrimary;
                }
                return _title;
            }
        }

        private static GUIStyle _subtitle;
        /// <summary>サブ見出し（18pt 太字）。フェーズ名・戦線名等。</summary>
        public static GUIStyle Subtitle
        {
            get
            {
                if (_subtitle == null)
                {
                    _subtitle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 18,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleLeft,
                        padding = new RectOffset(6, 6, 3, 3),
                    };
                    _subtitle.normal.textColor = TextPrimary;
                }
                return _subtitle;
            }
        }

        private static GUIStyle _body;
        /// <summary>本文（15pt）。標準ラベル代替。</summary>
        public static GUIStyle Body
        {
            get
            {
                if (_body == null)
                {
                    _body = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 15,
                        alignment = TextAnchor.MiddleLeft,
                        padding = new RectOffset(4, 4, 2, 2),
                        wordWrap = true,
                    };
                    _body.normal.textColor = TextPrimary;
                }
                return _body;
            }
        }

        private static GUIStyle _muted;
        /// <summary>補助テキスト（14pt・薄色）。注釈・未偵察等。</summary>
        public static GUIStyle Muted
        {
            get
            {
                if (_muted == null)
                {
                    _muted = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 14,
                        alignment = TextAnchor.MiddleLeft,
                        padding = new RectOffset(4, 4, 2, 2),
                    };
                    _muted.normal.textColor = TextMuted;
                }
                return _muted;
            }
        }

        private static GUIStyle _stat;
        /// <summary>数値表示（16pt 太字）。点数・行動力・HP等の強調。</summary>
        public static GUIStyle Stat
        {
            get
            {
                if (_stat == null)
                {
                    _stat = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 16,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter,
                    };
                    _stat.normal.textColor = TextPrimary;
                }
                return _stat;
            }
        }

        private static GUIStyle _panel;
        /// <summary>標準パネル背景。GUI.skin.box の代替。</summary>
        public static GUIStyle Panel
        {
            get
            {
                if (_panel == null)
                {
                    _panel = new GUIStyle(GUI.skin.box)
                    {
                        padding = new RectOffset(8, 8, 6, 6),
                        margin = new RectOffset(2, 2, 2, 2),
                    };
                    _panel.normal.textColor = TextPrimary;
                }
                return _panel;
            }
        }

        private static GUIStyle _primaryButton;
        /// <summary>主アクションボタン（18pt 太字）。戦闘実行・次のラウンドへ等の進行系。</summary>
        public static GUIStyle PrimaryButton
        {
            get
            {
                if (_primaryButton == null)
                {
                    _primaryButton = new GUIStyle(GUI.skin.button)
                    {
                        fontSize = 18,
                        fontStyle = FontStyle.Bold,
                        padding = new RectOffset(12, 12, 8, 8),
                    };
                }
                return _primaryButton;
            }
        }

        //
        // ヘルパー
        //

        /// <summary>
        /// 指定色のチップ（小さな丸角矩形ラベル）を1つ描く。フェーズタグ・状態バッジ用。
        /// 呼び出し側で GUILayout に乗せるため、GUI.color を変えて Box を描き Label を被せる。
        /// </summary>
        public static void DrawChip(string text, Color color, float width = 96f, float height = 24f)
        {
            var rect = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height));
            DrawChipAt(rect, text, color);
        }

        /// <summary>固定 Rect 位置にチップを描画する。</summary>
        public static void DrawChipAt(Rect rect, string text, Color color)
        {
            var prevBg = GUI.color;
            GUI.color = color;
            GUI.Box(rect, "");
            GUI.color = prevBg;

            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                fontStyle = FontStyle.Bold,
            };
            labelStyle.normal.textColor = Color.white;
            GUI.Label(rect, text, labelStyle);
        }

        /// <summary>指定色の塗りつぶし矩形を描く。背景・区切り用。</summary>
        public static void FillRect(Rect rect, Color color)
        {
            var prev = GUI.color;
            GUI.color = color;
            GUI.Box(rect, "");
            GUI.color = prev;
        }
    }
}
