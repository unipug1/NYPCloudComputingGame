using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float smoothTime = 0.15f;
    [SerializeField] private BoxCollider2D areaBounds;

    private Camera followCamera;
    private Vector3 followVelocity;

    private void Awake()
    {
        followCamera = GetComponent<Camera>();
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 destination = ClampToArea(GetTargetPosition());

        Vector3 nextPosition = Vector3.SmoothDamp(
            transform.position,
            destination,
            ref followVelocity,
            smoothTime
        );

        // keep the view inside the area while the camera follows
        transform.position = ClampToArea(nextPosition);
    }

    public void SwitchArea(BoxCollider2D newAreaBounds)
    {
        areaBounds = newAreaBounds;
        followVelocity = Vector3.zero;

        // snap to the new area after the player teleports
        if (target != null)
            transform.position = ClampToArea(GetTargetPosition());
    }

    private Vector3 GetTargetPosition()
    {
        return new Vector3(
            target.position.x,
            target.position.y,
            transform.position.z
        );
    }

    private Vector3 ClampToArea(Vector3 position)
    {
        if (areaBounds == null)
            return position;

        Bounds bounds = areaBounds.bounds;
        float halfHeight = followCamera.orthographicSize;
        float halfWidth = halfHeight * followCamera.aspect;

        position.x = ClampAxis(position.x, bounds.min.x, bounds.max.x, halfWidth);
        position.y = ClampAxis(position.y, bounds.min.y, bounds.max.y, halfHeight);

        return position;
    }

    private float ClampAxis(float position, float min, float max, float halfViewSize)
    {
        float minCenter = min + halfViewSize;
        float maxCenter = max - halfViewSize;

        // center the view when the area is smaller than the camera
        if (minCenter > maxCenter)
            return (min + max) * 0.5f;

        return Mathf.Clamp(position, minCenter, maxCenter);
    }
}