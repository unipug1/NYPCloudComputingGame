using UnityEngine;

[CreateAssetMenu(fileName = "AudioDataSO", 
    menuName = "Scriptable Objects/AudioDataSO")]
public class AudioDataSO : ScriptableObject
{
    [Range(0.0f, 1.0f)] public float masterVolume = 1.0f;
    [Range(0.0f, 1.0f)] public float musicVolume = 1.0f;
    [Range(0.0f, 1.0f)] public float sfxVolume = 1.0f;
}