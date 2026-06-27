// 永続化文字列 KVS の Domain 抽象。
//
// 【役割】
// - キー文字列に対する文字列の Load / Save / Has / Delete を提供する最小 KVS インタフェース。
// - 実装は Data 層（Echolos.Data.PlayerPrefsSaveStore など）。
//
// 【スコープ外】
// - SaveSchema / マイグレーション / スロット一覧 / オートセーブの API。
// - シリアライズ形式（JSON / Binary 等）：本抽象は文字列のみ扱う。
//   呼び出し側（MetaProgressStore など）がシリアライザを保持する。
namespace Echolos.Domain.Save
{
    /// <summary>永続化文字列 KVS の Domain 抽象（最小 API）。</summary>
    public interface ISaveStore
    {
        /// <summary>指定キーの内容を読み込む。未保存・空はそのまま空文字を返す（呼び出し側で扱う）。</summary>
        string Load(string key);

        /// <summary>指定キーに内容を保存する。即時 flush の保証は実装依存。</summary>
        void Save(string key, string content);

        /// <summary>指定キーが永続化済か。</summary>
        bool Has(string key);

        /// <summary>指定キーの保存を削除する。</summary>
        void Delete(string key);
    }
}
