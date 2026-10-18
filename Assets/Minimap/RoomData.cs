using UnityEngine;
using UnityEngine.UIElements;

public class RoomData : MonoBehaviour
{
    public string roomName;
    public Vector3 cameraPosition;
    public float minZoom = 5.0f;
    public float maxZoom = 15.0f;
    public float defaultZoom = 5.0f;
}