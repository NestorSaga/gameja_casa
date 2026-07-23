using UnityEngine;
using UnityEngine.InputSystem;

namespace Micasa
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController2D : MonoBehaviour
    {
        [SerializeField] float     moveSpeed         = 5f;
        [SerializeField] float     jumpForce         = 10f;
        [SerializeField] float     coyoteTime        = 0.15f;
        [SerializeField] Transform groundCheck;
        [SerializeField] float     groundCheckRadius = 0.1f;
        [SerializeField] LayerMask groundLayer;

        Rigidbody2D rb;
        float       moveInput;
        float       coyoteTimer;
        bool        isGrounded;
        bool        jumpQueued;

        void Awake() => rb = GetComponent<Rigidbody2D>();

        void Update()
        {
            CheckGround();

            var kb = Keyboard.current;
            moveInput = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);

            if (kb.spaceKey.wasPressedThisFrame && coyoteTimer > 0f)
            {
                jumpQueued  = true;
                coyoteTimer = 0f;
            }
        }

        void FixedUpdate()
        {
            rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);

            if (jumpQueued)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                jumpQueued = false;
            }
        }

        void CheckGround()
        {
            var hit = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
            isGrounded = hit != null;

            if (isGrounded)
            {
                coyoteTimer = coyoteTime;

                var platform = hit.GetComponent<MovingPlatform>();
                if (platform != null)
                    transform.SetParent(platform.transform, true);
                else if (transform.parent != null)
                    transform.SetParent(null, true);
            }
            else
            {
                coyoteTimer -= Time.deltaTime;
                if (transform.parent != null)
                    transform.SetParent(null, true);
            }
        }

        void OnDrawGizmosSelected()
        {
            if (groundCheck == null) return;
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}
