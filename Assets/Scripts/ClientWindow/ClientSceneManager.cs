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
        [SerializeField] Animator animator;

        private readonly Dictionary<string, EventInstance> playing = new();
        private Coroutine dialogueCoroutine;

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
                case "show-text-sequence": StartTextSequence(msg.payload); break;
                case "fmod-play":          PlayFmod(msg.payload);          break;
                case "fmod-stop":          StopFmod(msg.payload);          break;
                case "play-anim":          PlayAnim(msg.payload);          break;
            }
        }

        private void StartTextSequence(string json)
        {
            if (textDisplay == null) return;
            if (dialogueCoroutine != null) StopCoroutine(dialogueCoroutine);
            var seq = JsonUtility.FromJson<DialogueSequence>(json);
            dialogueCoroutine = StartCoroutine(RunDialogueSequence(seq.lines));
        }

        private IEnumerator RunDialogueSequence(List<DialogueLine> lines)
        {
            foreach (var line in lines)
            {
                if (line.delay > 0f)
                    yield return new WaitForSeconds(line.delay);
                textDisplay.text = TextResolver.Resolve(line.text);
                textDisplay.gameObject.SetActive(true);
            }
            dialogueCoroutine = null;
        }

        private void PlayAnim(string triggerName)
        {
            if (animator == null || string.IsNullOrEmpty(triggerName)) return;
            animator.SetTrigger(triggerName);
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
