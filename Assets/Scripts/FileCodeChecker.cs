using System.IO;
using UnityEngine;
using UnityEngine.Events;

namespace Micasa
{
    public class FileCodeChecker : MonoBehaviour
    {
        [SerializeField] private string fileName      = "output.txt";
        [SerializeField] private string expectedCode  = "";
        [SerializeField] private float  checkInterval = 1f;

        public UnityEvent OnCodeCorrect = new();

        private const string LinePrefix = "Contraseña para desactivar virus hongformático:";

        private float _timer;
        private bool  _triggered;

        void Awake()
        {
            if (AppBootstrap.CameraViewIndex >= 0) { enabled = false; return; }
            ClearOutputFile();
        }

        void Update()
        {
            if (_triggered) return;
            _timer += Time.deltaTime;
            if (_timer < checkInterval) return;
            _timer = 0f;
            if (GameManager.Instance == null || !GameManager.Instance.StageReady) return;
            CheckFile();
        }

        private void ClearOutputFile()
        {
            foreach (var path in CandidatePaths())
            {
                try
                {
                    if (File.Exists(path))
                        File.WriteAllText(path, string.Empty, System.Text.Encoding.UTF8);
                }
                catch { }
            }
        }

        private void CheckFile()
        {
            foreach (var path in CandidatePaths())
            {
                if (!File.Exists(path)) continue;

                string[] lines = null;
                try
                {
                    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var sr = new StreamReader(fs, System.Text.Encoding.UTF8);
                    lines = sr.ReadToEnd().Split('\n');
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[FileCodeChecker] No se pudo leer '{path}': {e.Message}");
                    continue;
                }

                foreach (var raw in lines)
                {
                    string line = raw.TrimEnd('\r');
                    int idx = line.IndexOf(LinePrefix, System.StringComparison.Ordinal);
                    if (idx < 0) continue;

                    string value = line.Substring(idx + LinePrefix.Length).Trim();
                    if (value == expectedCode)
                    {
                        _triggered = true;
                        OnCodeCorrect.Invoke();
                        GameManager.Instance.LoadNextStage();
                    }
                    else
                    {
                        Debug.Log($"[FileCodeChecker] Código incorrecto. Leído: '{value}' | Esperado: '{expectedCode}'");
                    }
                    return;
                }
                return;
            }
        }

        private System.Collections.Generic.IEnumerable<string> CandidatePaths()
        {
            string gameDir = Path.GetDirectoryName(Application.dataPath) ?? "";
            yield return Path.Combine(gameDir, fileName);
            yield return Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop),
                fileName);
        }
    }
}
