using System;

[Serializable]
public class LLM_Minigame_PresetOption
{
    // short 2 word text shown on the phone button
    public string summaryText;

    // full sentence spoken out loud in the player speech bubble
    public string fullDialogue;
}

[Serializable]
public class LLM_Minigame_ResponseData
{
    public string guardDialogue;
    public string emotionKey;
    public float trustChange;

    // array of the 3 presets with summary and full text
    public LLM_Minigame_PresetOption[] presetOptions;
}