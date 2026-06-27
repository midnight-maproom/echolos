// 味方または敵の「前衛3枠・後衛3枠」の2×3グリッドを管理するコンポーネント。
// Initialize(List<RuntimeUnit>) で各ユニットをスロットに配置し、
// GetCard(RuntimeUnit) でイベントハンドラから該当カードを素早く取得できるようにする。
// Clear() で全スロットの子オブジェクトを破棄してマップをリセット（バトル再開時のリセット）。

using System.Collections.Generic;
using UnityEngine;
using Echolos.Domain.Models;

using Echolos.Domain.Battle.Replay;
using Echolos.Domain.Story;
using Echolos.Presentation.Common;
using Echolos.Presentation.Battle;
using Echolos.Presentation.Story;
namespace Echolos.Presentation
{
    /// <summary>
    /// 1陣営（味方 or 敵）の2×3グリッドを管理するコンポーネント。
    /// Hierarchy 上でスロット0〜5に対応する Transform アンカーを用意し、
    /// Inspector から紐付けて使用する。
    /// </summary>
    public class BattleGridView : MonoBehaviour
    {
        // ─────────────────────────────────
        // Inspector から紐付けるUI参照
        // ─────────────────────────────────

        [SerializeField]
        [Tooltip("ユニットカードのプレハブ（UnitCardView がアタッチされた Prefab を設定する）")]
        private UnitCardView _unitCardPrefab;

        [Header("スロット配置用アンカー（前衛 左・中・右 → 後衛 左・中・右）")]

        [SerializeField]
        [Tooltip("スロット0: 前衛 左")]
        private Transform _slot0;

        [SerializeField]
        [Tooltip("スロット1: 前衛 中")]
        private Transform _slot1;

        [SerializeField]
        [Tooltip("スロット2: 前衛 右")]
        private Transform _slot2;

        [SerializeField]
        [Tooltip("スロット3: 後衛 左")]
        private Transform _slot3;

        [SerializeField]
        [Tooltip("スロット4: 後衛 中")]
        private Transform _slot4;

        [SerializeField]
        [Tooltip("スロット5: 後衛 右")]
        private Transform _slot5;

        // ─────────────────────────────────
        // 内部状態
        // ─────────────────────────────────

        /// <summary>スロットインデックス（0〜5）からTransformを引くための配列</summary>
        private Transform[] _slots;

        /// <summary>
        /// RuntimeUnit → UnitCardView のルックアップ辞書。
        /// Step 6.3 でイベントハンドラがユニットに対応するカードを O(1) で取得するために使用する。
        /// </summary>
        private readonly Dictionary<RuntimeUnit, UnitCardView> _cardMap
            = new Dictionary<RuntimeUnit, UnitCardView>();

        // ─────────────────────────────────
        // Unity ライフサイクル
        // ─────────────────────────────────

        private void Awake()
        {
            // スロットインデックス順に配列化しておく
            _slots = new Transform[] { _slot0, _slot1, _slot2, _slot3, _slot4, _slot5 };
        }

        // ─────────────────────────────────
        // 公開メソッド
        // ─────────────────────────────────

        /// <summary>
        /// ユニットリストを受け取り、各ユニットを対応するスロットに配置する。
        /// SlotIndex（0〜5）がアンカーと対応しているため、リストの並びは問わない。
        /// 呼び出し前に Clear() でスロットを空にしておくこと（バトル再開時）。
        /// </summary>
        /// <param name="units">表示するユニットのバトル用インスタンスリスト</param>
        public void Initialize(List<RuntimeUnit> units)
        {
            if (_unitCardPrefab == null)
            {
                Debug.LogError("[BattleGridView] _unitCardPrefab が未設定です。Inspector で Prefab を紐付けてください。");
                return;
            }

            foreach (RuntimeUnit unit in units)
            {
                int index = unit.SlotIndex;

                // スロットインデックスが範囲外の場合はスキップ
                if (index < 0 || index >= _slots.Length)
                {
                    Debug.LogWarning($"[BattleGridView] {unit.BaseUnit.Name} の SlotIndex={index} は範囲外です（0〜5）。");
                    continue;
                }

                Transform slotAnchor = _slots[index];

                // アンカーが未設定の場合はスキップ
                if (slotAnchor == null)
                {
                    Debug.LogWarning($"[BattleGridView] スロット{index} のアンカーが未設定です。Inspector で Transform を紐付けてください。");
                    continue;
                }

                // UnitCardView のプレハブをスロットアンカーの子として生成する
                UnitCardView card = Instantiate(_unitCardPrefab, slotAnchor);

                // RectTransform のローカル位置・スケールをリセット（スロット中央に収まるよう）
                RectTransform rt = card.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchoredPosition = Vector2.zero;
                    rt.localScale = Vector3.one;
                }

                // ユニットデータを渡して初期表示を設定する
                card.Initialize(unit);

                // Step 6.3: ルックアップ辞書に登録する（RuntimeUnit → UnitCardView）
                _cardMap[unit] = card;
            }
        }

        /// <summary>
        /// RuntimeUnit に対応する UnitCardView を返す。
        /// Step 6.3 のイベントハンドラから HP 更新・死亡グレーアウトに使用する。
        /// 対応するカードが存在しない場合は null を返す。
        /// </summary>
        /// <param name="unit">カードを取得したいユニット</param>
        /// <returns>対応する UnitCardView。存在しない場合は null</returns>
        public UnitCardView GetCard(RuntimeUnit unit)
        {
            if (unit == null) return null;
            _cardMap.TryGetValue(unit, out UnitCardView card);
            return card;
        }

        /// <summary>
        /// 全スロットに生成した UnitCardView の子オブジェクトをすべて破棄し、
        /// ルックアップ辞書をリセットする。
        /// バトルを再実行する際（ボタン連打等）、Initialize() の前に呼ぶこと。
        /// </summary>
        public void Clear()
        {
            // 辞書をクリア
            _cardMap.Clear();

            // 各スロットアンカーの子オブジェクトをすべて破棄する
            if (_slots == null) return;
            foreach (Transform slot in _slots)
            {
                if (slot == null) continue;
                // 子オブジェクトを末尾から逆順で破棄（インデックスのズレ防止）
                for (int i = slot.childCount - 1; i >= 0; i--)
                    Destroy(slot.GetChild(i).gameObject);
            }
        }
    }
}
