using System;
using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

namespace Micasa
{
    public enum GnomeAnim
    {
        None, eating, fnaf, nobitches, pog, scream, yapping, morshu, pc
    }

    public enum Gnome2Anim
    {
        None, feliz, huh, nerd, worry
    }

    public enum GnomophoneAnim
    {
        None, start, stop
    }

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
        SpawnObject,
        RestorePlayerControl,
        GnomeAppear,
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
    public class GnomeWindowStepData
    {
        public List<DialogueLine>   lines    = new();
        public List<EventReference> fmodPlay = new();
        public List<EventReference> fmodStop = new();
        public GnomeAnim            anim;
    }

    [Serializable]
    public class Gnome2WindowStepData
    {
        public List<DialogueLine>   lines    = new();
        public List<EventReference> fmodPlay = new();
        public List<EventReference> fmodStop = new();
        public Gnome2Anim           anim;
    }

    [Serializable]
    public class GnomophoneWindowStepData
    {
        public List<DialogueLine>   lines    = new();
        public List<EventReference> fmodPlay = new();
        public List<EventReference> fmodStop = new();
        public GnomophoneAnim       anim;
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

        [Header("Host (Main Window)")]
        public List<EventReference> hostFmodPlay = new();
        public List<EventReference> hostFmodStop = new();

        [Header("Client Windows")]
        public GnomeWindowStepData      gnome       = new();
        public GnomophoneWindowStepData gnomeophone = new();
        public Gnome2WindowStepData     gnome2      = new();
    }

    [CreateAssetMenu(fileName = "StageData", menuName = "Micasa/Stage Data")]
    public class StageData : ScriptableObject
    {
        public int             id;
        public int             collectiblesRequired = 3;
        public List<StageStep> steps = new();
    }
}
