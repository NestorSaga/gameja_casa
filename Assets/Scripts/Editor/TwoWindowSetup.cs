using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Micasa.Editor
{
    public static class TwoWindowSetup
    {
        [MenuItem("Micasa/Setup Two-Window System")]
        public static void Setup()
        {
            CreateBootstrapScene();
            CreateHostScene();
            CreateClientScene();
            UpdateBuildSettings();
            ConfigurePlayerSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[TwoWindowSetup] Done.");
        }

        // ── Bootstrap ─────────────────────────────────────────────────────

        private static void CreateBootstrapScene()
        {
            const string path = "Assets/Scenes/BootstrapScene.unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Make(scene, "AppBootstrap").AddComponent<AppBootstrap>();
            EditorSceneManager.SaveScene(scene, path);
        }

        // ── Host ─────────────────────────────────────────────────────────

        private static void CreateHostScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = Make(scene, "Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<HostWindowCamera>();

            var lightGo = Make(scene, "Global Light 2D");
            var light2DType = System.Type.GetType("UnityEngine.Rendering.Universal.Light2D, Unity.RenderPipelines.Universal.2D.Runtime");
            if (light2DType != null) lightGo.AddComponent(light2DType);

            var hostMgr = Make(scene, "HostWindowManager");
            hostMgr.AddComponent<HostWindowManager>();
            hostMgr.AddComponent<DVDBounce>();

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/HostScene.unity");
        }

        // ── Client ────────────────────────────────────────────────────────

        private static void CreateClientScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = Make(scene, "Main Camera");
            camGo.tag = "MainCamera";
            camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();

            var managerGo = Make(scene, "ClientWindowManager");
            managerGo.AddComponent<ClientWindowManager>();
            managerGo.AddComponent<ClientTransparentWindow>();

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/ClientScene.unity");
        }

        // ── Build Settings ────────────────────────────────────────────────

        private static void UpdateBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/BootstrapScene.unity",    true),
                new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity",          true),
                new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity",       true),
                new EditorBuildSettingsScene("Assets/Scenes/HostScene.unity",         true),
                new EditorBuildSettingsScene("Assets/Scenes/ClientScene.unity",       true),
                new EditorBuildSettingsScene("Assets/Scenes/GnomeScene.unity",        false),
                new EditorBuildSettingsScene("Assets/Scenes/GnomophoneScene.unity",   false),
                new EditorBuildSettingsScene("Assets/Scenes/Gnome2Scene.unity",       false),
            };
        }

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = false;
            PlayerSettings.runInBackground = true;
        }

        private static GameObject Make(Scene scene, string name)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }
    }
}
