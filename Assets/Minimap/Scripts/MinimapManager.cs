using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class MinimapManager : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject mapUIPanel;
    public TMP_Dropdown roomDropdown;
    public RectTransform minimapRect; // ui panel
    public GameObject smallMinimap;

    [Header("Camera Settings")]
    public Camera minimapCamera;
    public Transform mapCamera;
    public float zoomStep = 1.0f;

    [Header("Room Configuration")]
    public List<RoomData> allRooms;
    public RoomData currentRoom;

    [Header("Map Bounds")]
    public Vector2 worldMin, worldMax;

    private int activeRoomIndex = 0;
    private bool isDragging = false;

    private void Start()
    {
        mapUIPanel.SetActive(false); // hide map by default
        SetupDropdown();
    }

    private void Update()
    {
        // toggle map UI with 'M' key
        if (Keyboard.current.mKey.wasPressedThisFrame)
        {
            bool isActive = !mapUIPanel.activeSelf;
            mapUIPanel.SetActive(isActive);
            smallMinimap.SetActive(!isActive);

            if (isActive)
            {
                UpdateDropdownToCurrentRoom();
            }
        }

        // Only process map movement if the map is open
        if (mapUIPanel.activeSelf && minimapRect != null)
        {
            HandleMapDragging();
        }
    }

    private void HandleMapDragging()
    {
        Vector2 mousePosition = Mouse.current.position.ReadValue();

        // check if mouse is inside rect transform boundaries
        bool isInsideMinimap = RectTransformUtility.RectangleContainsScreenPoint(
            minimapRect,
            mousePosition,
            null // null since overlay
        );

        // detect left-click down inside mini,ap
        if (Mouse.current.leftButton.wasPressedThisFrame && isInsideMinimap)
        {
            isDragging = true;
        }

        // detect left-click release to stop dragging
        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            isDragging = false;
        }

        // while holding down left-click and dragging inside the map
        if (isDragging && Mouse.current.leftButton.isPressed)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            PanCamera(mouseDelta);
        }
    }

    private void PanCamera(Vector2 delta)
    {
        if (minimapCamera == null || minimapRect == null) return;

        // calculate world movement based on screen drag delta and current camera zoom
        // can add in sensitivity later
        float scaleFactor = minimapCamera.orthographicSize / minimapRect.rect.height;
        Vector3 move = new Vector3(-delta.x * scaleFactor, -delta.y * scaleFactor, 0f);

        Vector3 newPos = minimapCamera.transform.position + move;

        // clamp position to world bounds
        newPos.x = Mathf.Clamp(newPos.x, worldMin.x, worldMax.x);
        newPos.y = Mathf.Clamp(newPos.y, worldMin.y, worldMax.y);

        minimapCamera.transform.position = newPos;
    }

    void SetupDropdown()
    {
        roomDropdown.ClearOptions();
        List<string> options = new List<string>();

        foreach (RoomData roomData in allRooms)
        {
            options.Add(roomData.name);
        }

        roomDropdown.AddOptions(options);
        roomDropdown.onValueChanged.AddListener(OnDropdownRoomChanged);
    }

    // when player enters a new room physically
    public void SetCurrentPhysicalRoom(RoomData newRoom)
    {
        currentRoom = newRoom;
    }

    void UpdateDropdownToCurrentRoom()
    {
        if (currentRoom == null) return;
        int index = allRooms.IndexOf(currentRoom);
        if (index != -1)
        {
            roomDropdown.value = index;
            roomDropdown.RefreshShownValue();
            FocusCameraOnRoom(index);
        }
    }


    void OnDropdownRoomChanged(int index)
    {
        Debug.Log("Dropdown selected index: " + index);
        FocusCameraOnRoom(index);
    }


    void FocusCameraOnRoom(int index)
    {
        if (index < 0 || index >= allRooms.Count) return;

        RoomData targetRoom = allRooms[index];

        if (targetRoom == null || minimapCamera == null) return;

        Vector3 targetPosition = targetRoom.cameraPosition;

        // Preserve the camera's current Z position.
        targetPosition.z = minimapCamera.transform.position.z;

        minimapCamera.transform.position = targetPosition;
        minimapCamera.orthographicSize = targetRoom.defaultZoom;

        Debug.Log(
            "Selected room: " + targetRoom.name +
            " | Camera position: " + minimapCamera.transform.position +
            " | Zoom: " + minimapCamera.orthographicSize
        );
    }


    // zoom in button
    public void ZoomIn()
    {
        RoomData activeRoom = allRooms[activeRoomIndex];
        minimapCamera.orthographicSize =
            Mathf.Max(minimapCamera.orthographicSize - zoomStep,
            activeRoom.minZoom);
    }

    // zoom out button
    public void ZoomOut()
    {
        RoomData activeRoom = allRooms[activeRoomIndex];
        minimapCamera.orthographicSize =
            Mathf.Min(minimapCamera.orthographicSize + zoomStep,
            activeRoom.maxZoom);
    }
}