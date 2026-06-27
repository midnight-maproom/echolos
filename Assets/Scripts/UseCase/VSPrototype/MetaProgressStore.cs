// MetaProgressState の永続化 Wrapper（ISaveStore 抽象経由）。
//
// 【方針】
// - ISaveStore（Domain 抽象）に PlayerPrefs / JsonSaveStore 等の実装を注入して使う。
// - JSON 文字列化／復元は MetaProgressSerializer に委譲し、本クラスは
//   ISaveStore とのアダプタに徹する。
// - 永続化キーは `vsproto_meta_progress` 固定。
// - ロード失敗（不正 JSON など）時は初期状態の新規 State を返す（=ロスト時のリセット）。
using System;
using Echolos.Domain.Save;

namespace Echolos.UseCase.VSPrototype
{
    /// <summary>MetaProgressState の永続化（ISaveStore 経由・ラン外で呼ぶ）。</summary>
    public sealed class MetaProgressStore
    {
        /// <summary>永続化キー。</summary>
        public const string PrefsKey = "vsproto_meta_progress";

        private readonly ISaveStore _saveStore;

        public MetaProgressStore(ISaveStore saveStore)
        {
            _saveStore = saveStore ?? throw new ArgumentNullException(nameof(saveStore));
        }

        /// <summary>
        /// 永続化された State を復元する。未保存・空文字・不正 JSON の場合は
        /// 初期状態の新規 State を返す。常に非 null。
        /// </summary>
        public MetaProgressState Load()
        {
            var state = new MetaProgressState();
            string json = _saveStore.Load(PrefsKey);
            if (string.IsNullOrEmpty(json)) return state;

            // ApplyJson は失敗時 false を返すが、State には何も書き込まれないので
            // そのまま初期状態の state を返すだけでよい。
            MetaProgressSerializer.ApplyJson(json, state);
            return state;
        }

        /// <summary>State を永続化する。null は何もしない。</summary>
        public void Save(MetaProgressState state)
        {
            if (state == null) return;
            string json = MetaProgressSerializer.ToJson(state);
            _saveStore.Save(PrefsKey, json);
        }

        /// <summary>
        /// セーブデータが存在するか（タイトル画面でゲーム開始時に「Hub へ進む」か
        /// 「初回プレイとして即ラン開始」かの分岐に使う）。
        /// PrefsKey に対する SaveStore 値が空でなければセーブ済とみなす。
        /// </summary>
        public bool HasSaveData()
        {
            return !string.IsNullOrEmpty(_saveStore.Load(PrefsKey));
        }

        /// <summary>
        /// 永続化された State を削除する（開発用・トラブルシュート時のリセット）。
        /// 通常のゲーム進行では呼ばない。
        /// </summary>
        public void DeleteAll()
        {
            _saveStore.Delete(PrefsKey);
        }
    }
}
