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
        StartPuzzle,
        StopPuzzle,
        ToggleTransparency,
        ToggleExplorerMode,
        PlaySquishAnimation,
        ToggleDVDBounce,
        SetWindowsVolume,
    }

    [Serializable]
    public class WindowStepData
    {
        public string              text;
        public float               textDuration;
        public List<EventReference> fmodPlay = new();
        public List<EventReference> fmodStop = new();
    }

    [Serializable]
    public class StageStep
    {
        public float             delay;
        public List<StageAction> actions = new();
        [Range(0f, 1f)] public float targetVolume = 1f;

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
