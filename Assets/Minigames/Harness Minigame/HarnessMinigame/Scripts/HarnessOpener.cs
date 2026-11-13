using UnityEngine;

// Put on the NPC or the AI terminal in your map.
// Opens the mini-game when the player clicks/taps it, or walks into its trigger collider.
public class HarnessOpener : MonoBehaviour
{
    public HarnessMinigame minigame;
    public bool openOnClick = true;        // needs a Collider2D
    public bool openOnPlayerTouch = false; // needs a Collider2D set to Is Trigger
    public string playerTag = "Player";

    void OnMouseDown()
    {
        if (openOnClick) minigame.Open();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (openOnPlayerTouch && other.CompareTag(playerTag)) minigame.Open();
    }
}
