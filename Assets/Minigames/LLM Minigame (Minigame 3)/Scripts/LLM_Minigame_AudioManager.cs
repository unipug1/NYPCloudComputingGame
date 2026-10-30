using System.Collections;
using UnityEngine;

public class LLM_Minigame_AudioManager : MonoBehaviour
{
    public static LLM_Minigame_AudioManager Instance { get; private set; }

    [Header("audio settings")]
    [SerializeField] private bool isAudioEnabled = true;
    public bool IsAudioEnabled => isAudioEnabled;

    [Header("audio sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource voiceSource;
    [SerializeField] private AudioSource typingSource;

    [Header("background music")]
    [SerializeField] private AudioClip backgroundMusic;
    [Range(0f, 1f)] [SerializeField] private float bgmVolume = 0.5f;

    [Header("prop transition clips")]
    [SerializeField] private AudioClip clipboardSwapIn;
    [SerializeField] private AudioClip clipboardSwapOut;
    [SerializeField] private AudioClip phoneSwapIn;
    [SerializeField] private AudioClip phoneSwapOut;

    [Header("typing and voice clips")]
    [SerializeField] private AudioClip playerKeystroke;
    [SerializeField] private AudioClip aiVoiceBlip;

    [Header("dialogue bubble clips")]
    [SerializeField] private AudioClip playerBubbleOpen;
    [SerializeField] private AudioClip playerBubbleClose;

    [Header("gameplay feedback clips")]
    [SerializeField] private AudioClip playerTurnStart;
    [SerializeField] private AudioClip trustGoUp;
    [SerializeField] private AudioClip trustGoDown;
    [SerializeField] private AudioClip deadEnd;
    [SerializeField] private AudioClip warningBar;
    [SerializeField] private AudioClip gameWin;
    [SerializeField] private AudioClip uiButtonClick;

    [Header("typing audio limiter")]
    [SerializeField] private float minKeystrokeInterval = 0.055f; // limits rapid sound stacking
    private float lastKeystrokeTime = 0f;

    private Coroutine bgmFadeRoutine;
    private int voiceBlipCounter = 0;

    public void SetAudioEnabled(bool isEnabled)
    {
        isAudioEnabled = isEnabled;

        if (bgmSource != null) bgmSource.mute = !isEnabled;
        if (sfxSource != null) sfxSource.mute = !isEnabled;
        if (voiceSource != null) voiceSource.mute = !isEnabled;
        if (typingSource != null) typingSource.mute = !isEnabled;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        SetupSources();
    }

    private void Start()
    {
        if (backgroundMusic != null)
        {
            PlayBGM(backgroundMusic, bgmVolume);
        }
    }

    private void SetupSources()
    {
        if (bgmSource == null) bgmSource = gameObject.AddComponent<AudioSource>();
        if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
        if (voiceSource == null) voiceSource = gameObject.AddComponent<AudioSource>();
        if (typingSource == null) typingSource = gameObject.AddComponent<AudioSource>();

        // force full 2d sound so canvas ui audio is never silenced
        bgmSource.spatialBlend = 0f;
        sfxSource.spatialBlend = 0f;
        voiceSource.spatialBlend = 0f;
        typingSource.spatialBlend = 0f;

        bgmSource.loop = true;
        bgmSource.playOnAwake = false;

        sfxSource.playOnAwake = false;
        voiceSource.playOnAwake = false;
        typingSource.playOnAwake = false;
    }

    public void PlayVoiceBlip()
    {
        if (aiVoiceBlip == null || voiceSource == null) return;

        voiceBlipCounter++;
        // triggers every 2 characters so it doesn't sound like machine gun noise
        if (voiceBlipCounter % 2 == 0)
        {
            voiceSource.pitch = Random.Range(0.95f, 1.05f);
            voiceSource.PlayOneShot(aiVoiceBlip, 1f);
        }
    }

    // --- background music ---

    public void PlayBGM(AudioClip clip, float volume = 0.5f)
    {
        if (clip == null) return;

        if (bgmFadeRoutine != null) StopCoroutine(bgmFadeRoutine);
        bgmSource.clip = clip;
        bgmSource.volume = volume;
        bgmSource.Play();
    }

    public void StopBGM(float fadeDuration = 0.5f)
    {
        if (bgmFadeRoutine != null) StopCoroutine(bgmFadeRoutine);
        bgmFadeRoutine = StartCoroutine(FadeOutBGMRoutine(fadeDuration));
    }

    private IEnumerator FadeOutBGMRoutine(float duration)
    {
        float startVol = bgmSource.volume;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            bgmSource.volume = Mathf.Lerp(startVol, 0f, timer / duration);
            yield return null;
        }

        bgmSource.Stop();
        bgmSource.volume = startVol;
    }

    // rate-limited keystroke prevents audio summing and ear-bleeding volume spikes
    public void PlayKeystroke(float volume = 0.35f)
    {
        if (playerKeystroke == null || typingSource == null) return;

        // drop sounds that fire too close together
        if (Time.unscaledTime - lastKeystrokeTime < minKeystrokeInterval)
        {
            return;
        }

        lastKeystrokeTime = Time.unscaledTime;
        typingSource.pitch = Random.Range(0.93f, 1.07f);
        typingSource.PlayOneShot(playerKeystroke, volume);
    }

    // --- sound effect triggers ---

    public void PlayClipboardSwapIn() => PlayOneShot(clipboardSwapIn);
    public void PlayClipboardSwapOut() => PlayOneShot(clipboardSwapOut);
    public void PlayPhoneSwapIn() => PlayOneShot(phoneSwapIn);
    public void PlayPhoneSwapOut() => PlayOneShot(phoneSwapOut);

    public void PlayPlayerBubbleOpen() => PlayOneShot(playerBubbleOpen);
    public void PlayPlayerBubbleClose() => PlayOneShot(playerBubbleClose);

    public void PlayPlayerTurnStart() => PlayOneShot(playerTurnStart);
    public void PlayTrustGoUp() => PlayOneShot(trustGoUp);
    public void PlayTrustGoDown() => PlayOneShot(trustGoDown);
    public void PlayDeadEnd() => PlayOneShot(deadEnd);
    public void PlayWarningBar() => PlayOneShot(warningBar);
    public void PlayGameWin() => PlayOneShot(gameWin);
    public void PlayButtonClick() => PlayOneShot(uiButtonClick);

    // subtle pitch modulation prevents repetitive typing fatigue
    public void PlayKeystroke()
    {
        if (playerKeystroke == null) return;
        typingSource.pitch = Random.Range(0.92f, 1.08f);
        typingSource.PlayOneShot(playerKeystroke, 0.45f);
    }

    private void PlayOneShot(AudioClip clip, float volume = 1f)
    {
        if (clip != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(clip, volume);
        }
    }
}