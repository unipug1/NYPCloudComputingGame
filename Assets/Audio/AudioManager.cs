using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    [SerializeField] private AudioDataSO audioSettings;
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioClip backgroundMusic;
    [SerializeField] private AudioSource sfxSource;

    private const string MasterParameter = "MasterVolume";
    private const string MusicParameter = "MusicVolume";
    private const string SFXParameter = "SFXVolume";

    private void Start()
    {
        ApplySavedVolumes();
        if (backgroundMusic != null)
        {
            PlayMusic(backgroundMusic);
        }
    }

    public void SetMasterVolume(float value)
    {
        if (audioSettings == null) return;
        audioSettings.masterVolume = value;
        SetMixerVolume(MasterParameter, value);
    }

    public void SetMusicVolume(float value)
    {
        if (audioSettings == null) return;
        audioSettings.musicVolume = value;
        SetMixerVolume(MusicParameter, value);
    }

    public void SetSFXVolume(float value)
    {
        if (audioSettings == null) return;
        audioSettings.sfxVolume = value;
        SetMixerVolume(SFXParameter, value);
    }

    private void ApplySavedVolumes()
    {
        if (audioSettings == null) return;

        SetMixerVolume(MasterParameter, audioSettings.masterVolume);
        SetMixerVolume(MusicParameter, audioSettings.musicVolume);
        SetMixerVolume(SFXParameter, audioSettings.sfxVolume);
    }

    private void SetMixerVolume(string parameterName, float value)
    {
        if (audioMixer == null) return;
        value = Mathf.Clamp(value, 0f, 1f);

        if (value <= 0.0001f)
        {
            audioMixer.SetFloat(parameterName, -80f);
            return;
        }

        float decibels = Mathf.Log10(value) * 20f;
        audioMixer.SetFloat(parameterName, decibels);
    }

    public void PlayMusic(AudioClip clip)
    {
        if (musicSource == null || clip == null)
        {
            return;
        }
        if (musicSource.clip == clip && musicSource.isPlaying)
        {
            return;
        }

        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource != null)
        {
            musicSource.Stop();
        }
    }

    public void PlaySFX(AudioClip clip)
    {
        if (sfxSource == null || clip == null)
        {
            return;
        }
        sfxSource.PlayOneShot(clip);
    }
}