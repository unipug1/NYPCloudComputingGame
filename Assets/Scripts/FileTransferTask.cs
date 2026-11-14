using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class FileTransferTask : MonoBehaviour
{
    [Header("UI References")]
    public Slider progressBar;
    public TextMeshProUGUI percentageText;
    public TextMeshProUGUI statusText;
    public Button startButton;
    public GameObject taskWindow;

    [Header("Wireframe Visuals")]
    public Image middleIcon;
    public Sprite neutralArrowSprite; // State 04
    public Sprite activeArrowSprite;  // State 05 (Green arrow)
    public Sprite checkmarkSprite;    // State 06 (Done tick)

    [Header("Audio Settings")]
    public AudioSource audioSource;
    public AudioClip buttonClickClip;
    public AudioClip transferLoopClip;
    public AudioClip completeClip;

    [Header("Settings")]
    public float transferDuration = 4.0f;

    private bool isTransferring = false;

    void OnEnable()
    {
        // Reset the UI every time this window opens
        ResetUI();
    }

    void Start()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (startButton != null) startButton.onClick.AddListener(OnStartButtonClicked);
    }

    void ResetUI()
    {
        isTransferring = false;

        if (progressBar != null) progressBar.value = 0f;
        if (percentageText != null) percentageText.text = "0%";
        if (statusText != null) statusText.text = "";

        // Reset to State 04
        if (middleIcon != null && neutralArrowSprite != null) middleIcon.sprite = neutralArrowSprite;

        // Show start button
        if (startButton != null)
        {
            startButton.gameObject.SetActive(true);
            startButton.interactable = true;
        }
    }

    void OnStartButtonClicked()
    {
        if (isTransferring) return;

        if (audioSource != null && buttonClickClip != null) audioSource.PlayOneShot(buttonClickClip);

        // Hide start button to match State 05
        startButton.gameObject.SetActive(false);
        StartCoroutine(TransferRoutine());
    }

    IEnumerator TransferRoutine()
    {
        isTransferring = true;
        float elapsedTime = 0f;

        // Transition to State 05: In Progress
        if (middleIcon != null && activeArrowSprite != null) middleIcon.sprite = activeArrowSprite;
        if (statusText != null) statusText.text = "Processing...";

        if (audioSource != null && transferLoopClip != null)
        {
            audioSource.clip = transferLoopClip;
            audioSource.loop = true;
            audioSource.Play();
        }

        while (elapsedTime < transferDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / transferDuration);

            if (progressBar != null) progressBar.value = progress;
            if (percentageText != null) percentageText.text = Mathf.RoundToInt(progress * 100f) + "%";

            yield return null;
        }

        // Transition to State 06: Final Done
        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.loop = false;
            if (completeClip != null) audioSource.PlayOneShot(completeClip);
        }

        if (progressBar != null) progressBar.value = 1f;
        if (percentageText != null) percentageText.text = "100%";
        if (statusText != null) statusText.text = "Transfer Complete!";
        if (middleIcon != null && checkmarkSprite != null) middleIcon.sprite = checkmarkSprite;

        isTransferring = false;

        // Optional: Wait 1.5 seconds so the player can see the completion screen, then close
        yield return new WaitForSeconds(1.5f);
        if (taskWindow != null) taskWindow.SetActive(false);
    }
}