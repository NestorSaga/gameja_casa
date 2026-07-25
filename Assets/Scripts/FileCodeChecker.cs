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

        void Update()
        {
            if (_triggered) return;
            _timer += Time.deltaTime;
            if (_timer < checkInterval) return;
            _timer = 0f;
            CheckFile();
        }

        private void CheckFile()
        {
            string path = Path.Combine(
                Path.GetDirectoryName(Application.dataPath) ?? "",
                fileName);

            if (!File.Exists(path)) return;

            foreach (var line in File.ReadAllLines(path, System.Text.Encoding.UTF8))
            {
                int idx = line.IndexOf(LinePrefix, System.StringComparison.Ordinal);
                if (idx < 0) continue;

                string value = line.Substring(idx + LinePrefix.Length).Trim();
                if (value == expectedCode)
                {
                    if (GameManager.Instance == null)
                    {
                        Debug.LogWarning("[FileCodeChecker] Código correcto pero GameManager.Instance es null.");
                        return;
                    }
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
        }
    }
}
