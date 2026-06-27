// ISaveStore の PlayerPrefs 実装。
//
// 【役割】
// - VSプロト範囲ではメタ進行（vsproto_meta_progress キー）の永続化に使用。
// - 別実装に差し替え可能（複数スロット・チェックサム・Steam Cloud 連携時）。
//
// 【設計方針】
// - PlayerPrefs.Save() を Save 毎に呼んで即時 flush（VSプロトは保存タイミングが少ないため安全側）。
using UnityEngine;
using Echolos.Domain.Save;

namespace Echolos.Data
{
    /// <summary>ISaveStore の PlayerPrefs 実装。</summary>
    public sealed class PlayerPrefsSaveStore : ISaveStore
    {
        public string Load(string key)
        {
            return PlayerPrefs.GetString(key, string.Empty);
        }

        public void Save(string key, string content)
        {
            PlayerPrefs.SetString(key, content ?? string.Empty);
            PlayerPrefs.Save();
        }

        public bool Has(string key)
        {
            return PlayerPrefs.HasKey(key);
        }

        public void Delete(string key)
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }
}
