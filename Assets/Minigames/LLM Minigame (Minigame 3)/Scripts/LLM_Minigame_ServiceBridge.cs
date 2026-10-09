using System;
using System.Collections;
using UnityEngine;

public class LLM_Minigame_ServiceBridge : MonoBehaviour
{
    [SerializeField] private float fakeWaitSeconds = 1.2f;

    public void SendPlayerMessage(string playerText, Action<LLM_Minigame_ResponseData> onComplete)
    {
        StartCoroutine(SimulateThinkingRoutine(playerText, onComplete));
    }

    private IEnumerator SimulateThinkingRoutine(string playerText, Action<LLM_Minigame_ResponseData> onComplete)
    {
        yield return new WaitForSeconds(fakeWaitSeconds);

        LLM_Minigame_ResponseData response = new LLM_Minigame_ResponseData();
        response.presetOptions = new string[] { "Answer 1", "Answer 2", "Answer 3" };

        string cleanText = string.IsNullOrEmpty(playerText) ? "" : playerText.ToLower().Trim();

        // test case 1: player types win
        if (cleanText.Contains("win"))
        {
            response.guardDialogue = "convinced emotion test.";
            response.emotionKey = "Convinced";
            response.trustChange = 25f;
        }
        // test case 2: player types lose
        else if (cleanText.Contains("lose"))
        {
            response.guardDialogue = "sus emotion test. minus trust";
            response.emotionKey = "Suspicious";
            response.trustChange = -15f;
        }
        // test case 3: normal input or game start
        else if (cleanText == "start")
        {
            response.guardDialogue = "normal input test.";
            response.emotionKey = "Neutral";
            response.trustChange = 0f;
        }
        else
        {
            response.guardDialogue = "thinking test. 123 123 123";
            response.emotionKey = "Thinking";
            response.trustChange = 0f;
        }

        onComplete?.Invoke(response);
    }

    public void ResetSteps()
    {
        // empty stub for compatibility
    }
}