using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class FitToCamera : MonoBehaviour
{
    private Camera       cam;
    private SpriteRenderer sr;
    private Vector2      spriteSize;

    void Awake()
    {
        cam        = Camera.main;
        sr         = GetComponent<SpriteRenderer>();
        spriteSize = sr.sprite.bounds.size;
    }

    void LateUpdate()
    {
        float worldHeight = cam.orthographicSize * 2f;
        float worldWidth  = worldHeight * cam.aspect;

        transform.localScale = new Vector3(
            worldWidth  / spriteSize.x,
            worldHeight / spriteSize.y,
            1f
        );

        transform.position = new Vector3(
            cam.transform.position.x,
            cam.transform.position.y,
            transform.position.z
        );
    }
}
