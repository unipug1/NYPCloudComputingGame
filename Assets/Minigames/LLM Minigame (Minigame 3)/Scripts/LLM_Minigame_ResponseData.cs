using System;

[Serializable]
public class LLM_Minigame_ResponseData
{
    // the text the guard says out loud
    public string guardDialogue;

    // which emotion to show on the character
    public string emotionKey;

    // how much the trust bar goes up or down
    public float trustChange;

    // the 3 preset buttons
    public string[] presetOptions;
}