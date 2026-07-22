using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Micasa.Editor
{
    public static class TwoWindowSetup
    {
        [MenuItem("Micasa/Setup Two-Window System")]
        public static void Setup()
        {
            CreateHostScene();
            CreateClientScene();
            AddBootstrapToSampleScene();
            UpdateBuildSettings();
            ConfigurePlayerSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[TwoWindowSetup] Done.");
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
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
            light.color = Color.white;

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

        // ── Bootstrap ─────────────────────────────────────────────────────

        private static void AddBootstrapToSampleScene()
        {
            const string path = "Assets/Scenes/SampleScene.unity";
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

            foreach (var go in scene.GetRootGameObjects())
                if (go.GetComponent<AppBootstrap>() != null) { EditorSceneManager.CloseScene(scene, false); return; }

            Make(scene, "AppBootstrap").AddComponent<AppBootstrap>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, false);
        }

        // ── Shared ────────────────────────────────────────────────────────

        private static void UpdateBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/HostScene.unity",   true),
                new EditorBuildSettingsScene("Assets/Scenes/ClientScene.unity", true),
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
