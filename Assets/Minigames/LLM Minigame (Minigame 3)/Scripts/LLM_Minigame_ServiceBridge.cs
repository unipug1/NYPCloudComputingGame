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

        response.presetOptions = new LLM_Minigame_PresetOption[]
        {
            new LLM_Minigame_PresetOption
            {
                summaryText = "Fire Alarm",
                fullDialogue = "Listen to me! There is smoke coming from server rack B, open the door now!"
            },
            new LLM_Minigame_PresetOption
            {
                summaryText = "Badge Audit",
                fullDialogue = "I am with the external security audit team. Stand aside or I will log an infraction."
            },
            new LLM_Minigame_PresetOption
            {
                summaryText = "Offer Donut",
                fullDialogue = "Hey guard, I brought fresh donuts. Mind if I just step inside for five minutes?"
            }
        };

        string cleanText = string.IsNullOrEmpty(playerText) ? "" : playerText.ToLower().Trim();

        // test case win: awards +50 trust so typing it twice triggers the win screen
        if (cleanText.Contains("win"))
        {
            response.guardDialogue = "That explanation actually checks out. Proceed inside.";
            response.emotionKey = "Convinced";
            response.trustChange = 50f;
        }
        // test case lose: deducts -20 trust and shakes meter
        else if (cleanText.Contains("lose"))
        {
            response.guardDialogue = "That makes no sense at all. Who sent you here?";
            response.emotionKey = "Suspicious";
            response.trustChange = -20f;
        }
        else if (cleanText == "start")
        {
            response.guardDialogue = "Halt. State your business at the server room.";
            response.emotionKey = "Neutral";
            response.trustChange = 0f;
        }
        else
        {
            response.guardDialogue = "I hear you, but I still need to verify your claim.";
            response.emotionKey = "Thinking";
            response.trustChange = 0f;
        }

        onComplete?.Invoke(response);
    }

    public void ResetSteps()
    {
    }
}