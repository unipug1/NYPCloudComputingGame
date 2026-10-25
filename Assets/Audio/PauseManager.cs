using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PauseManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject overlay;

    public bool IsPaused { get; private set; }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (overlay != null) overlay.SetActive(false);
        pausePanel.SetActive(false);
        settingsPanel.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        if (Keyboard.current.escapeKey.wasPressedThisFrame && !IsPaused)
        {
            PauseGame();
        }
    }

    public void PauseGame()
    {
        IsPaused = true;
        if (overlay != null) overlay.SetActive(true);
        pausePanel.SetActive(true);
        settingsPanel.SetActive(false);
        Time.timeScale = 0.0f;
    }

    public void ResumeGame()
    {
        IsPaused = false;
        if (overlay != null) overlay.SetActive(false);
        pausePanel.SetActive(false);
        settingsPanel.SetActive(false);
        Time.timeScale = 1.0f;
    }

    public void OpenSettings()
    {
        pausePanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        settingsPanel.SetActive(false);
        pausePanel.SetActive(true);
    }

    public void ExitToMainMenu()
    {
        Time.timeScale = 1.0f;
        // load main menu screen
        //SceneManager.LoadScene("MenuScene");
    }
}