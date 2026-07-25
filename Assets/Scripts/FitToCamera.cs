using Micasa;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class FitToCamera : MonoBehaviour
{
    private Camera           cam;
    private SpriteRenderer   sr;
    private Vector2          spriteSize;
    private HostWindowCamera hostCam;

    void Awake()
    {
        cam        = Camera.main;
        sr         = GetComponent<SpriteRenderer>();
        spriteSize = sr.sprite.bounds.size;
        hostCam    = Object.FindAnyObjectByType<HostWindowCamera>();
    }

    void LateUpdate()
    {
        float worldWidth, worldHeight;
        Vector3 center;

        if (hostCam != null && hostCam.ExplorerMode)
        {
            worldHeight = Display.main.systemHeight / HostWindowCamera.PPU;
            worldWidth  = Display.main.systemWidth  / HostWindowCamera.PPU;
            center      = new Vector3(worldWidth * 0.5f, worldHeight * 0.5f, transform.position.z);
        }
        else
        {
            worldHeight = cam.orthographicSize * 2f;
            worldWidth  = worldHeight * cam.aspect;
            center      = new Vector3(cam.transform.position.x, cam.transform.position.y, transform.position.z);
        }

        transform.localScale = new Vector3(worldWidth / spriteSize.x, worldHeight / spriteSize.y, 1f);
        transform.position   = center;
    }
}
