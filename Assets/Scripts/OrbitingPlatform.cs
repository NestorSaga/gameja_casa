using UnityEngine;

namespace Micasa
{
    public class OrbitingPlatform : MonoBehaviour
    {
        [SerializeField] Transform pivot;
        [SerializeField] float radius = 3f;
        [SerializeField] float speed  = 90f; // degrees per second

        float angle;

        void Start()
        {
            if (pivot == null) return;

            // Calcula el ángulo inicial desde la posición actual respecto al pivot
            Vector2 offset = transform.position - pivot.position;
            if (offset.magnitude > 0.001f)
                radius = offset.magnitude;

            angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
        }

        void FixedUpdate()
        {
            if (pivot == null) return;

            angle += speed * Time.fixedDeltaTime;

            float rad = angle * Mathf.Deg2Rad;
            transform.position = pivot.position + new Vector3(
                Mathf.Cos(rad) * radius,
                Mathf.Sin(rad) * radius,
                0f
            );
        }
    }
}
