using UnityEngine;

namespace Micasa
{
    public class LevelScaler : MonoBehaviour
    {
        [SerializeField] private Vector2 referenceResolution = new(1920, 1080);
        [SerializeField] private bool uniformScale = false;

        void Awake()
        {
            float sx = Display.main.systemWidth  / referenceResolution.x;
            float sy = Display.main.systemHeight / referenceResolution.y;

            if (uniformScale)
            {
                float s = Mathf.Min(sx, sy);
                transform.localScale = new Vector3(s, s, 1f);
            }
            else
            {
                transform.localScale = new Vector3(sx, sy, 1f);
            }

            transform.position = new Vector3(
                Display.main.systemWidth  * 0.5f / HostWindowCamera.PPU,
                Display.main.systemHeight * 0.5f / HostWindowCamera.PPU,
                0f);
        }
    }
}
