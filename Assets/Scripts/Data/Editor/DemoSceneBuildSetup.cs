// 試遊モード／通常モードのビルド前 Scenes 切替メニュー。
//
// Build Settings の Scenes In Build を、通常版／試遊版ビルド用にワンクリックで切り替える。
// 切替後に File > Build Settings... → Build... で実ビルドを実行する。
//
// シーンファイル：
// - 通常版：EcholosProto_VS.unity（既存）
// - 試遊版：EcholosProto_Demo.unity（要：EcholosProto_VS.unity を複製＋ VSPrototypeTitleGUI の
//   _showDemoModeButtons=true に設定）
using UnityEditor;
using UnityEngine;

namespace Echolos.Data.Editor
{
    public static class DemoSceneBuildSetup
    {
        private const string NormalScenePath = "Assets/Scenes/EcholosProto_VS.unity";
        private const string DemoScenePath   = "Assets/Scenes/EcholosProto_Demo.unity";

        [MenuItem("Echolos/Build/通常版 Scenes に設定")]
        public static void ConfigureNormalBuild()
        {
            if (!System.IO.File.Exists(NormalScenePath))
            {
                Debug.LogWarning($"通常版シーンが存在しません: {NormalScenePath}");
                return;
            }
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(NormalScenePath, enabled: true),
            };
            Debug.Log($"通常版ビルド用 Scenes に設定しました: {NormalScenePath}");
        }

        [MenuItem("Echolos/Build/試遊版 Scenes に設定")]
        public static void ConfigureDemoBuild()
        {
            if (!System.IO.File.Exists(DemoScenePath))
            {
                Debug.LogWarning(
                    $"試遊版シーンが存在しません: {DemoScenePath}\n" +
                    "手順: EcholosProto_VS.unity を複製して EcholosProto_Demo.unity にリネーム → " +
                    "VSPrototypeTitleGUI の _showDemoModeButtons を true に設定。");
                return;
            }
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(DemoScenePath, enabled: true),
            };
            Debug.Log($"試遊版ビルド用 Scenes に設定しました: {DemoScenePath}");
        }
    }
}
