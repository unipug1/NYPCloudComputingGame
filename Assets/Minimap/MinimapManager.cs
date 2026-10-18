using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class MinimapManager : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject mapUIPanel;
    public TMP_Dropdown roomDropdown;

    [Header("Camera Settings")]
    public Camera minimapCamera;
    public float zoomStep = 1.0f;

    [Header("Room Configuration")]
    public List<RoomData> allRooms;
    public RoomData currentRoom;

    private int activeRoomIndex = 0;

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

            if (isActive)
            {
                UpdateDropdownToCurrentRoom();
            }
        }
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
        if (index < 0 || index >= allRooms.Count)
            return;

        RoomData targetRoom = allRooms[index];

        if (targetRoom == null || minimapCamera == null)
            return;

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