using UnityEngine;

namespace Micasa
{
    public class ExploreWaypatroller : MonoBehaviour
    {
        [SerializeField] Transform[] waypoints           = new Transform[3];
        [SerializeField] float       speed               = 2f;
        [SerializeField] float       visibilityThreshold = 0.5f;

        Camera           cam;
        Renderer         rend;
        readonly Plane[] frustumPlanes = new Plane[6];

        int   waypointIndex;
        float visibleTime;
        bool  active;
        bool  atWaypoint;

        void Awake()
        {
            cam  = Camera.main;
            rend = GetComponentInChildren<Renderer>(true);
            var anim = GetComponentInChildren<Animator>();
            if (anim != null) anim.applyRootMotion = false;
        }

        public void Activate()   { enabled = true; active = true; }
        public void Deactivate() { active = false; visibleTime = 0f; atWaypoint = false; }
        public void Vanish()
        {
            GameManager.Instance?.RestorePlayerControl();
            Destroy(gameObject);
        }

        void Update()
        {
            if (!active) return;

            if (!atWaypoint)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    waypoints[waypointIndex].position,
                    speed * Time.deltaTime
                );

                if (transform.position == waypoints[waypointIndex].position)
                {
                    atWaypoint  = true;
                    visibleTime = 0f;
                }
            }
            else
            {
                if (cam != null)
                {
                    GeometryUtility.CalculateFrustumPlanes(cam, frustumPlanes);
                    var  bounds = rend != null ? rend.bounds : new Bounds(transform.position, Vector3.zero);
                    bool inView = GeometryUtility.TestPlanesAABB(frustumPlanes, bounds);
                    visibleTime = inView ? visibleTime + Time.deltaTime : 0f;
                }

                if (visibleTime >= visibilityThreshold)
                {
                    if (waypointIndex == waypoints.Length - 1)
                    {
                        GameManager.Instance?.LoadNextStageNoLoadingScreen();
                        active = false;
                        return;
                    }
                    waypointIndex++;
                    atWaypoint  = false;
                    visibleTime = 0f;
                }
            }
        }
    }
}
