using UnityEngine;
using UnityEngine.InputSystem;

namespace CoinCatcher
{
    /// <summary>
    /// The player's basket. Moves left/right with A/D or the arrow keys, or follows the pointer
    /// (mouse, and later touch) while it is pressed. Stays inside the camera's visible width.
    /// </summary>
    // No Rigidbody2D here: a trigger only needs a Rigidbody2D on ONE of the two colliders, and the
    // falling coin already has one. The basket is just a collider the coins fly into.
    [RequireComponent(typeof(BoxCollider2D))]
    public class Basket : MonoBehaviour
    {
        [SerializeField] private Camera gameCamera;
        [Tooltip("Keyboard speed in world units per second.")]
        [SerializeField] private float keyboardSpeed = 8f;

        private InputAction moveAction;
        private InputAction pointAction;
        private InputAction pressAction;
        private float halfWidth;

        private void Awake()
        {
            // Actions are built in code so all the bindings are visible in one place. "<Pointer>" matches
            // both a mouse and a touchscreen, so touch works without extra code.
            moveAction = new InputAction("Move", InputActionType.Value);
            moveAction.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/a")
                .With("Positive", "<Keyboard>/d");
            moveAction.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/leftArrow")
                .With("Positive", "<Keyboard>/rightArrow");

            pointAction = new InputAction("Point", InputActionType.Value, "<Pointer>/position");
            pressAction = new InputAction("Press", InputActionType.Button, "<Pointer>/press");

            halfWidth = GetComponent<BoxCollider2D>().size.x * 0.5f * transform.lossyScale.x;
        }

        // Input actions only report values while enabled, so they follow the component: the
        // GameManager disables the basket outside a run, and the basket stops listening.
        private void OnEnable()
        {
            moveAction.Enable();
            pointAction.Enable();
            pressAction.Enable();
        }

        private void OnDisable()
        {
            moveAction.Disable();
            pointAction.Disable();
            pressAction.Disable();
        }

        private void OnDestroy()
        {
            // Actions created with "new" hold native resources until disposed.
            moveAction.Dispose();
            pointAction.Dispose();
            pressAction.Dispose();
        }

        private void Update()
        {
            var x = transform.position.x;

            if (pressAction.IsPressed())
            {
                var pointer = pointAction.ReadValue<Vector2>();
                x = gameCamera.ScreenToWorldPoint(pointer).x;
            }
            else
            {
                // Speed * deltaTime = distance this frame, so the basket moves equally fast at 30 or 120 fps.
                x += moveAction.ReadValue<float>() * keyboardSpeed * Time.deltaTime;
            }

            MoveTo(x);
        }

        /// <summary>Puts the basket back in the middle of the screen, e.g. when a level starts.</summary>
        public void Recenter()
        {
            MoveTo(gameCamera.transform.position.x);
        }

        private void MoveTo(float x)
        {
            var min = CameraBounds.Left(gameCamera) + halfWidth;
            var max = CameraBounds.Right(gameCamera) - halfWidth;

            var position = transform.position;
            position.x = Mathf.Clamp(x, min, max);
            transform.position = position;
        }
    }
}
