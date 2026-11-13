using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [System.Serializable]
    private class DirectionSprites
    {
        public Sprite idle;
        public Sprite[] walkFrames = new Sprite[3];
    }

    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField, Min(1f)] private float walkFramesPerSecond = 8f;

    [SerializeField] private DirectionSprites down = new DirectionSprites();
    [SerializeField] private DirectionSprites left = new DirectionSprites();
    [SerializeField] private DirectionSprites right = new DirectionSprites();
    [SerializeField] private DirectionSprites up = new DirectionSprites();

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private bool movementBlocked;

    private Vector2 previousPhysicsPosition;
    private bool movedLastPhysicsStep;

    private DirectionSprites facing;
    private bool wasWalking;
    private float walkTime;

    public bool IsMovementBlocked => movementBlocked;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        previousPhysicsPosition = rb.position;
        facing = down;
    }

    private void Update()
    {
        moveInput = Vector2.zero;
        Keyboard keyboard = Keyboard.current;

        if (movementBlocked || keyboard == null)
            return;

        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            moveInput.y += 1f;

        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            moveInput.y -= 1f;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            moveInput.x -= 1f;

        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            moveInput.x += 1f;

        // keep diagonal movement at the same speed
        moveInput = moveInput.normalized;
    }

    private void FixedUpdate()
    {
        // check how far the player moved during the last physics step
        Vector2 movement = rb.position - previousPhysicsPosition;
        float minimumDistance = 0.01f * Time.fixedDeltaTime;
        movedLastPhysicsStep = movement.sqrMagnitude > minimumDistance * minimumDistance;
        previousPhysicsPosition = rb.position;

        rb.linearVelocity = moveInput * moveSpeed;
    }

    private void LateUpdate()
    {
        if (spriteRenderer == null)
            return;

        bool hasMoveInput = moveInput != Vector2.zero;
        DirectionSprites nextFacing = facing;

        // face the direction the player is trying to move
        if (moveInput.x != 0f)
            nextFacing = moveInput.x > 0f ? right : left;
        else if (moveInput.y != 0f)
            nextFacing = moveInput.y > 0f ? up : down;

        bool changedDirection = nextFacing != facing;
        facing = nextFacing;

        // show idle when a wall stops the player
        if (!hasMoveInput || !movedLastPhysicsStep)
        {
            wasWalking = false;
            ShowSprite(facing.idle);
            return;
        }

        if (!wasWalking || changedDirection)
            walkTime = 0f;
        else
            walkTime += Time.deltaTime;

        wasWalking = true;
        Sprite[] frames = facing.walkFrames;

        if (frames == null || frames.Length == 0)
        {
            ShowSprite(facing.idle);
            return;
        }

        int frameIndex = Mathf.FloorToInt(walkTime * walkFramesPerSecond) % frames.Length;
        ShowSprite(frames[frameIndex] != null ? frames[frameIndex] : facing.idle);
    }

    private void ShowSprite(Sprite sprite)
    {
        if (sprite != null && spriteRenderer.sprite != sprite)
            spriteRenderer.sprite = sprite;
    }

    public void SetMovementBlocked(bool blocked)
    {
        movementBlocked = blocked;
        moveInput = Vector2.zero;
        movedLastPhysicsStep = false;

        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        rb.linearVelocity = Vector2.zero;
        previousPhysicsPosition = rb.position;
    }
}