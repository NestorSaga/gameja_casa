using System.Collections;
using System.Collections.Generic;
using FMOD.Studio;
using Micasa.Bridge;
using TMPro;
using UnityEngine;

namespace Micasa
{
    public class ClientSceneManager : MonoBehaviour
    {
        [SerializeField] TMP_Text textDisplay;

        private readonly Dictionary<string, EventInstance> playing = new();
        private Coroutine hideText;

        void Start()
        {
            var bridge = WindowBridge.Instance;
            if (bridge == null) return;

            bridge.OnMessageReceived.AddListener(OnMessage);
        }

        private void OnMessage(BridgeMessage msg)
        {
            switch (msg.type)
            {
                case "show-text": ShowText(msg.payload); break;
                case "fmod-play": PlayFmod(msg.payload); break;
                case "fmod-stop": StopFmod(msg.payload); break;
            }
        }

        private void ShowText(string text)
        {
            if (textDisplay == null) return;
            if (hideText != null) StopCoroutine(hideText);
            textDisplay.text = TextResolver.Resolve(text);
            textDisplay.gameObject.SetActive(true);
        }

        private void PlayFmod(string guidStr)
        {
            if (string.IsNullOrEmpty(guidStr) || playing.ContainsKey(guidStr)) return;
            var evRef = new FMODUnity.EventReference { Guid = FMOD.GUID.Parse(guidStr) };
            var inst  = FMODUnity.RuntimeManager.CreateInstance(evRef);
            inst.start();
            playing[guidStr] = inst;
        }

        private void StopFmod(string guidStr)
        {
            if (!playing.TryGetValue(guidStr, out var inst)) return;
            inst.stop(STOP_MODE.ALLOWFADEOUT);
            inst.release();
            playing.Remove(guidStr);
        }

        void OnDestroy()
        {
            foreach (var inst in playing.Values)
            {
                inst.stop(STOP_MODE.IMMEDIATE);
                inst.release();
            }
            playing.Clear();
        }
    }
}
