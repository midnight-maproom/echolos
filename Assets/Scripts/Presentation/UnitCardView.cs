// ユニット1体分の情報（名前・HP・HPバー・シールド・属性・リーダーマーク）を表示する
// UIプレハブのルートにアタッチするコンポーネント。
// Initialize(RuntimeUnit) で初期状態を反映し、
// UpdateHp / UpdateShield / SetDead で戦闘中のリアルタイム更新を行う。

using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Echolos.Domain.Models;

using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Story;
using Echolos.Presentation.Common;
using Echolos.Presentation.Battle;
using Echolos.Presentation.Story;
namespace Echolos.Presentation
{
    /// <summary>
    /// ユニット1体分のUIカード。
    /// プレハブのルートにアタッチし、Inspector で各UIパーツを紐付けて使用する。
    /// 配置編成のため、クリック検知（IPointerClickHandler）と選択ハイライトも担う。
    /// </summary>
    public class UnitCardView : MonoBehaviour, IPointerClickHandler
    {
        // ─────────────────────────────────
        // 配置編成用（クリック検知・選択状態）
        // ─────────────────────────────────

        /// <summary>このカードが表すユニット。Initializeで設定される。</summary>
        public RuntimeUnit Unit { get; private set; }

        /// <summary>カードがクリックされたときに発火する（配置編成のスワップ操作に使用）。</summary>
        public event Action<UnitCardView> Clicked;

        private void Awake()
        {
            // クリックを確実に拾うため、レイキャスト対象のGraphicを保証する。
            // ルートにGraphicが無ければ透明なImageを追加（カード全面がクリック可能になる）。
            var graphic = GetComponent<Graphic>();
            if (graphic == null)
            {
                var img = gameObject.AddComponent<Image>();
                img.color = new Color(1f, 1f, 1f, 0f); // 透明
            }
            else
            {
                graphic.raycastTarget = true;
            }
        }

        /// <summary>EventSystemから呼ばれる。クリックをClickedイベントとして通知する。</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            Clicked?.Invoke(this);
        }

        /// <summary>選択状態の見た目を切り替える（選択中はカードを少し拡大）。</summary>
        public void SetSelected(bool selected)
        {
            transform.localScale = selected ? Vector3.one * 1.12f : Vector3.one;
        }

        // ─────────────────────────────────
        // Inspector から紐付けるUI参照（Step 6.2 から継続）
        // ─────────────────────────────────

        [SerializeField]
        [Tooltip("ユニット名を表示する Text コンポーネント")]
        private Text _nameText;

        [SerializeField]
        [Tooltip("HP数値（現在HP / 最大HP）を表示する Text コンポーネント")]
        private Text _hpText;

        [SerializeField]
        [Tooltip("HPバーの塗りつぶし部分の Image コンポーネント（Image Type を Filled に設定すること）")]
        private Image _hpBarFill;

        [SerializeField]
        [Tooltip("属性（火・水・氷など）を表示する Text コンポーネント")]
        private Text _elementText;

        [SerializeField]
        [Tooltip("リーダーマーク（★）を表示する GameObject。IsLeader が false の場合は非表示になる")]
        private GameObject _leaderBadge;

        [SerializeField]
        [Tooltip("前衛 / 後衛 のラベルを表示する Text コンポーネント")]
        private Text _rowLabelText;

        // ─────────────────────────────────
        // Inspector から紐付けるUI参照（Step 6.3 追加）
        // ─────────────────────────────────

        [Header("Step 6.3: 動的更新用UI")]

        [SerializeField]
        [Tooltip("【Step 6.3 新規】死亡時にカード全体を暗転させるオーバーレイ Image。\n" +
                 "プレハブ内にフルサイズの子Imageを作成し、初期状態は非アクティブにしておくこと。")]
        private Image _deadOverlay;

        [SerializeField]
        [Tooltip("【Step 6.3 新規】シールド量を表示する Text コンポーネント（任意）。\n" +
                 "未設定の場合、シールド表示は行われない。")]
        private Text _shieldText;

        // ─────────────────────────────────
        // 公開メソッド（Step 6.2 から継続）
        // ─────────────────────────────────

        /// <summary>
        /// RuntimeUnit のデータを受け取り、このカードの各UIパーツを初期状態に設定する。
        /// 戦闘開始前に一度だけ呼ぶこと。動的な更新は UpdateHp / SetDead を使用する。
        /// </summary>
        /// <param name="unit">表示するユニットのバトル用インスタンス</param>
        public void Initialize(RuntimeUnit unit)
        {
            // このカードが表すユニットを保持（配置編成のクリック操作で参照する）
            Unit = unit;

            // 選択ハイライトを解除した状態で表示する
            SetSelected(false);

            // ユニット名
            if (_nameText != null)
                _nameText.text = unit.BaseUnit.Name;

            // HP数値テキスト（例: "100 / 100"）
            if (_hpText != null)
                _hpText.text = $"{unit.CurrentHP} / {unit.MaxHP}";

            // HPバーの塗りつぶし割合（0.0〜1.0）
            if (_hpBarFill != null)
            {
                float ratio = unit.MaxHP > 0
                    ? Mathf.Clamp01((float)unit.CurrentHP / unit.MaxHP)
                    : 0f;
                _hpBarFill.fillAmount = ratio;
            }

            // 属性ラベル
            if (_elementText != null)
                _elementText.text = ElementToJapanese(unit.BaseUnit.UnitElement);

            // リーダーマーク（IsLeader の場合のみ表示）
            if (_leaderBadge != null)
                _leaderBadge.SetActive(unit.IsLeader);

            // slot 番号ラベル（旧前衛/後衛表示の置換・6 体一列の新仕様）
            if (_rowLabelText != null)
                _rowLabelText.text = $"slot{unit.SlotIndex}";

            // シールドの初期表示（初期値は0のため非表示）
            if (_shieldText != null)
                _shieldText.text = "";

            // 死亡オーバーレイを確実に非表示にする（バトル再開時のリセット）
            if (_deadOverlay != null)
                _deadOverlay.gameObject.SetActive(false);
        }

        // ─────────────────────────────────
        // 公開メソッド（Step 6.3 追加: 動的更新API）
        // ─────────────────────────────────

        /// <summary>
        /// HP数値テキストとHPバーを現在のHP値で更新する。
        /// ActionExecutor.OnHitLanded イベントのハンドラから呼ぶこと。
        /// </summary>
        /// <param name="currentHp">ダメージ適用後の現在HP</param>
        /// <param name="maxHp">このユニットの最大HP</param>
        public void UpdateHp(int currentHp, int maxHp)
        {
            // HP数値テキスト更新
            if (_hpText != null)
                _hpText.text = $"{currentHp} / {maxHp}";

            // HPバー割合更新
            if (_hpBarFill != null)
            {
                float ratio = maxHp > 0
                    ? Mathf.Clamp01((float)currentHp / maxHp)
                    : 0f;
                _hpBarFill.fillAmount = ratio;
            }
        }

        /// <summary>
        /// シールド量テキストを更新する。
        /// ActionExecutor.OnHitLanded イベントのハンドラから呼ぶこと。
        /// </summary>
        /// <param name="shield">現在のシールド量。0の場合はテキストをクリアする。</param>
        public void UpdateShield(int shield)
        {
            if (_shieldText == null) return;

            // シールドが0のときは空文字にして非表示に近い状態にする
            _shieldText.text = shield > 0 ? $"盾: {shield}" : "";
        }

        /// <summary>
        /// ユニットが死亡したとき、カード全体を暗転させる（オーバーレイを表示する）。
        /// ActionExecutor.OnUnitDied イベントのハンドラから呼ぶこと。
        /// HPバーも0にリセットする。
        /// </summary>
        public void SetDead()
        {
            // 死亡時にHPを0表示に確定させる
            if (_hpText != null)
                _hpText.text = "0 / --";

            if (_hpBarFill != null)
                _hpBarFill.fillAmount = 0f;

            // シールドも0にクリア
            if (_shieldText != null)
                _shieldText.text = "";

            // 暗転オーバーレイを表示してカードをグレーアウト
            if (_deadOverlay != null)
                _deadOverlay.gameObject.SetActive(true);
        }

        // ─────────────────────────────────
        // 内部ユーティリティ
        // ─────────────────────────────────

        /// <summary>属性 Enum を日本語の短い文字列に変換する</summary>
        private static string ElementToJapanese(Element element)
        {
            switch (element)
            {
                case Element.Fire:      return "火";
                case Element.Water:     return "水";
                case Element.Ice:       return "氷";
                case Element.Lightning: return "雷";
                case Element.Wind:      return "風";
                case Element.Earth:     return "地";
                case Element.Light:     return "光";
                case Element.Dark:      return "闇";
                default:                return "無";
            }
        }
    }
}
