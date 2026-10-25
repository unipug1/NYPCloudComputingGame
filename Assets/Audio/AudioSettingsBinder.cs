using UnityEngine;
using UnityEngine.UI;

public class AudioSettingsBinder : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AudioDataSO audioSettings;
    [SerializeField] private AudioManager audioManager;

    [Header("Sliders")]
    [SerializeField] private Slider masterVolSlider;
    [SerializeField] private Slider musicVolSlider;
    [SerializeField] private Slider sfxVolSlider;

    private void Start()
    {
        if (audioSettings == null) return;
        if (audioManager == null) return;

        masterVolSlider.value = audioSettings.masterVolume;
        musicVolSlider.value = audioSettings.musicVolume;
        sfxVolSlider.value = audioSettings.sfxVolume;

        masterVolSlider.onValueChanged.AddListener(audioManager.SetMasterVolume);
        musicVolSlider.onValueChanged.AddListener(audioManager.SetMusicVolume);
        sfxVolSlider.onValueChanged.AddListener(audioManager.SetSFXVolume);
    }

    private void OnDestroy()
    {
        if (audioManager == null) return;

        masterVolSlider.onValueChanged.RemoveListener(audioManager.SetMasterVolume);
        musicVolSlider.onValueChanged.RemoveListener(audioManager.SetMusicVolume);
        sfxVolSlider.onValueChanged.RemoveListener(audioManager.SetSFXVolume);
    }
}