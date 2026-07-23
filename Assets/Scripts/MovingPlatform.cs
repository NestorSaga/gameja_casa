using UnityEngine;

namespace Micasa
{
    public class MovingPlatform : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] float speed = 2f;

        Vector3 origin;
        bool    goingToTarget = true;

        void Start()
        {
            origin = transform.position;
        }

        void FixedUpdate()
        {
            if (target == null) return;

            Vector3 destination = goingToTarget ? target.position : origin;
            transform.position  = Vector3.MoveTowards(transform.position, destination, speed * Time.deltaTime);

            if (transform.position == destination)
                goingToTarget = !goingToTarget;
        }
    }
}
