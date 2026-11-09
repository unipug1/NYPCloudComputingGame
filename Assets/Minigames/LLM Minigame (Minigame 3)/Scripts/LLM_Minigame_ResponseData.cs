using System;

[Serializable]
public class LLM_Minigame_PresetOption
{
    public string summaryText;
    public string fullDialogue;
}

[Serializable]
public class LLM_Minigame_ResponseData
{
    public string guardDialogue;
    public string emotionKey;
    public float trustChange;

    // core reason tag for the rainbow text
    public string reasonTag;

    // dead end state and explanation
    public bool isDeadEnd;
    public string deadEndReason;

    public LLM_Minigame_PresetOption[] presetOptions;
}