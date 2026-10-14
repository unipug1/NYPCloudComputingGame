using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class DialogueChoiceOption
{
    public string choiceText = "Option";
    public int jumpToLineIndex = -1; // set to target line index or -1 for the very next line
}

[System.Serializable]
public class DialogueLine
{
    [Header("Speaker Info")]
    public string speakerName = "Name";
    public Sprite speakerPortrait;

    [Header("Dialogue Text")]
    [TextArea(2, 5)]
    public string sentence = "Enter dialogue text here...";
    public float typingSpeed = 0.03f;

    [Header("Line Event (Triggers right after typing finishes)")]
    public UnityEvent onLineEndEvent;

    [Header("Optional Choices")]
    public bool hasChoices = false;
    public List<DialogueChoiceOption> choices = new List<DialogueChoiceOption>();
}