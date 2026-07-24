using UnityEngine;

namespace Micasa
{
    public class MainWindowOnly : MonoBehaviour
    {
        [SerializeField] GameObject target;

        void Awake()
        {
            var obj = target != null ? target : gameObject;
            obj.SetActive(AppBootstrap.CameraViewIndex < 0);
        }
    }
}
