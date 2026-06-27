// Unity 6 で Assets メニューから Reserialize All が削除されたため、
// AssetDatabase.ForceReserializeAssets() を Echolos メニュー配下に提供する。
// namespace / class rename 後の `m_EditorClassIdentifier` キャッシュ最新化に使う。
using UnityEditor;

namespace Echolos.Data.Editor
{
    public static class ForceReserializeMenu
    {
        [MenuItem("Echolos/Tools/Reserialize All Assets")]
        public static void ReserializeAll()
        {
            AssetDatabase.ForceReserializeAssets();
        }
    }
}
