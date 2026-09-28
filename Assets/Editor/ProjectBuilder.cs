// Editor tooling: creates the (single, nearly empty) scene, applies player settings and
// builds the Windows player. Menu: Wish Extractor/…   Batch: -executeMethod WishExtractor.EditorTools.ProjectBuilder.BuildWindowsCI
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace WishExtractor.EditorTools
{
    public static class ProjectBuilder
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string BuildPath = "Builds/Windows/WishExtractor.exe";

        [MenuItem("Wish Extractor/1. Set Up Scene And Settings")]
        public static void Setup()
        {
            ApplyPlayerSettings();
            CreateScene();
            Debug.Log("[WishExtractor] scene and settings ready");
        }

        [MenuItem("Wish Extractor/2. Build Windows Player")]
        public static void BuildWindows()
        {
            Setup();
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = BuildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            Debug.Log($"[WishExtractor] build {s.result}: {s.totalSize / (1024 * 1024)} MB, {s.totalErrors} errors, {s.totalTime}");
            if (s.result != BuildResult.Succeeded) throw new System.Exception("Build failed: " + s.result);
        }

        /// <summary>Batch-mode entry point: exits with 0 on success, 1 on failure.</summary>
        public static void BuildWindowsCI()
        {
            try
            {
                BuildWindows();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[WishExtractor] " + e);
                EditorApplication.Exit(1);
            }
        }

        static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "Nico Macaraig";
            PlayerSettings.productName = "Wish Extractor";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.allowFullscreenSwitch = true;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.SetApiCompatibilityLevel(UnityEditor.Build.NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
        }

        static void CreateScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Fog and trilight ambient are set here so shader stripping keeps the variants the game uses.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.01f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            var root = new GameObject("Wish Extractor");
            root.AddComponent<WishExtractor.Game.GameRoot>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
        }
    }
}
