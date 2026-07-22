#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Micasa.Editor
{
    public static class DisableFlipModelSwapchain
    {
        [MenuItem("Tools/Micasa/Disable Flip Model Swapchain")]
        static void Apply()
        {
            PlayerSettings.useFlipModelSwapchain = false;
            AssetDatabase.SaveAssets();
            Debug.Log("[Micasa] Flip model swapchain disabled. Rebuild the project to apply.");
        }

        [MenuItem("Tools/Micasa/Disable Flip Model Swapchain", validate = true)]
        static bool Validate() => PlayerSettings.useFlipModelSwapchain;
    }
}
#endif
