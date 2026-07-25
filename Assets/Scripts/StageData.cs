using System;
using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

namespace Micasa
{
    public enum StageAction
    {
        OpenGnomeWindow,
        OpenGnomophoneWindow,
        OpenGnome2Window,
        CloseGnomeWindow,
        CloseGnomophoneWindow,
        CloseGnome2Window,
        StartPuzzle,
        StopPuzzle,
        ToggleTransparency,
        ToggleExplorerMode,
        PlaySquishAnimation,
        ToggleDVDBounce,
        SetWindowsVolume,
        SelfDestruct,
        StretchFull,
        StretchWide,
        StretchTall,
        StretchLoop,
        StopStretch,
        HideLoadingScreen,
        LockGame,
        OpenURL,
        GenerateFile,
        ToggleLoadingScreen,
        TogglePhysics,
    }

    [Serializable]
    public class DialogueLine
    {
        public string text;
        public float  delay;
    }

    [Serializable]
    public class DialogueSequence
    {
        public List<DialogueLine> lines;
    }

    [Serializable]
    public class WindowStepData
    {
        public List<DialogueLine>   lines    = new();
        public List<EventReference> fmodPlay = new();
        public List<EventReference> fmodStop = new();
    }

    [Serializable]
    public class StageStep
    {
        public float             delay;
        public List<StageAction> actions = new();
        [Range(0f, 1f)] public float targetVolume = 1f;
        public string            url;
        public TextAsset         fileTemplate;
        public string            outputFileName;

        public WindowStepData gnome       = new();
        public WindowStepData gnomeophone = new();
        public WindowStepData gnome2      = new();
    }

    [CreateAssetMenu(fileName = "StageData", menuName = "Micasa/Stage Data")]
    public class StageData : ScriptableObject
    {
        public int             id;
        public int             collectiblesRequired = 3;
        public List<StageStep> steps = new();
    }
}
