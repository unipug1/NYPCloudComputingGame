using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

[Serializable]
public class OpenRouterMessage
{
    public string role;
    public string content;
}

[Serializable]
public class OpenRouterRequest
{
    public string model;
    public List<OpenRouterMessage> messages;
    public float temperature = 0.75f;
}

[Serializable]
public class OpenRouterChoice
{
    public OpenRouterMessage message;
}

[Serializable]
public class OpenRouterResponse
{
    public List<OpenRouterChoice> choices;
}

public class LLM_Minigame_ServiceBridge : MonoBehaviour
{
    [Header("openrouter settings")]
    [SerializeField] private string apiKey = "sk-or-v1-YOUR-KEY-HERE";
    [SerializeField] private string modelName = "meta-llama/llama-3.3-70b-instruct:free";

    [Header("system prompt")]
    [TextArea(12, 30)]
    [SerializeField]
    private string systemPrompt =
    @"CONTEXT:
- You are Mr. Tan, an approachable but observant lecturer guarding the School of IT server room at NYP during Open House.
- The player is a secondary school or ITE student on a tour trying to persuade you to let them inside.

WHAT YOU MUST DO:
- Reply in 1 to 2 short sentences ONLY. Use simple, natural conversational English.
- Sentence 1: React, lightly roast, or question the player's excuse.
- Sentence 2: Provide an everyday non-technical opening they can latch onto (e.g. tour guide instructions, lost belongings, curious about glowing lights).
- Output ONLY raw valid JSON matching the schema below.

REASON TAG RULES (2 to 5 WORDS IN CAPITALS):
Compare what the visitor just said against [Previous Topic]:
- CASE 1 (SAME STORY): If the visitor is still continuing or defending the previous excuse, keep the exact same 'reasonTag'.
- CASE 2 (TOPIC SWITCH - MANDATORY UPDATE): If the visitor switches to an entirely different excuse (e.g. from tour guide to lost bag):
  * YOU MUST CHANGE 'reasonTag' to summarise the new topic (e.g. 'LOST BAG').
  * In 'guardDialogue', call them out for changing their story ('First you said the tour guide sent you, now you lost a bag? Which is it?').
  * Deduct -5 to -10 trust.

DEAD END RULE:
- If the player's story becomes an impossible contradiction or an absolute logical dead end:
  * Set 'isDeadEnd': true
  * In 'deadEndReason', explain in 1 simple sentence why their story failed.
  * In 'guardDialogue', call out their contradiction directly.
- Otherwise, set 'isDeadEnd': false and 'deadEndReason': ''.

TURN 1 INVALID REASON RULE:
- On Turn 1, do NOT trigger a dead end. Deliver a sarcastic roast, penalise -5 to -10 trust, and give an everyday clue.

TRUST SCORING:
- +40 to +60: Persuasive reasoning, genuine student interest, or solving your doubt (emotionKey: 'Convinced').
- +20: Plausible answer or agreeing with your hint (emotionKey: 'Thinking').
- 0: Neutral chatter or vague reply (emotionKey: 'Neutral').
- -5 to -10: Story inconsistency, blatant lie, or silly bribe (emotionKey: 'Suspicious').

MAX TRUST / ACCESS GRANTED:
- If trust reaches 100, verbally unlock the door. Set emotionKey to 'Convinced'.

JSON SCHEMA (no markdown, no backticks):
{
  ""guardDialogue"": ""1 to 2 short sentences."",
  ""emotionKey"": ""Thinking"",
  ""trustChange"": 20,
  ""reasonTag"": ""TWO TO FIVE WORDS"",
  ""isDeadEnd"": false,
  ""deadEndReason"": """",
  ""presetOptions"": [
    { ""summaryText"": ""Two Words"", ""fullDialogue"": ""One sentence."" },
    { ""summaryText"": ""Two Words"", ""fullDialogue"": ""One sentence."" },
    { ""summaryText"": ""Two Words"", ""fullDialogue"": ""One sentence."" }
  ]
}";

    private const string ApiUrl = "https://openrouter.ai/api/v1/chat/completions";
    private List<OpenRouterMessage> chatHistory = new List<OpenRouterMessage>();

    public LLM_Minigame_ResponseData GetInstantOpeningLine()
    {
        chatHistory.Clear();
        chatHistory.Add(new OpenRouterMessage { role = "system", content = systemPrompt });
        chatHistory.Add(new OpenRouterMessage { role = "user", content = "A secondary school student on the NYP Open House tour walks up to the server room door." });

        LLM_Minigame_ResponseData openingData = new LLM_Minigame_ResponseData
        {
            guardDialogue = "The server room is <color=#E60012>off limits</color> to visitors. Did your <color=#00A3FF>tour guide</color> send you here, or did someone leave their <color=#00A3FF>bag</color> inside?",
            emotionKey = "Neutral",
            trustChange = 0f,
            reasonTag = "",
            isDeadEnd = false,
            deadEndReason = "",
            presetOptions = new LLM_Minigame_PresetOption[]
            {
                new LLM_Minigame_PresetOption { summaryText = "Tour Guide", fullDialogue = "Yes, our tour guide said we could ask you for a quick look inside!" },
                new LLM_Minigame_PresetOption { summaryText = "Lost Bag", fullDialogue = "My friend left their bag by the door during the morning tour, I just need to grab it." },
                new LLM_Minigame_PresetOption { summaryText = "Looks Cool", fullDialogue = "Neither, Sir, the glowing servers just look really cool and I wanted to see them." }
            }
        };

        string openingJson = "{\"guardDialogue\":\"" + openingData.guardDialogue + "\",\"emotionKey\":\"Neutral\",\"trustChange\":0}";
        chatHistory.Add(new OpenRouterMessage { role = "assistant", content = openingJson });

        return openingData;
    }

    public void SendPlayerMessage(string playerText, float currentTrust, string lockedReason, Action<LLM_Minigame_ResponseData> onComplete)
    {
        StartCoroutine(SendOpenRouterRequest(playerText, currentTrust, lockedReason, onComplete));
    }

    // backwards compatibility overloads
    public void SendPlayerMessage(string playerText, float currentTrust, Action<LLM_Minigame_ResponseData> onComplete)
    {
        StartCoroutine(SendOpenRouterRequest(playerText, currentTrust, "", onComplete));
    }

    public void SendPlayerMessage(string playerText, Action<LLM_Minigame_ResponseData> onComplete)
    {
        StartCoroutine(SendOpenRouterRequest(playerText, 10f, "", onComplete));
    }


    private IEnumerator SendOpenRouterRequest(string playerText, float currentTrust, string lockedReason, Action<LLM_Minigame_ResponseData> onComplete)
    {
        if (chatHistory.Count == 0)
        {
            chatHistory.Add(new OpenRouterMessage { role = "system", content = systemPrompt });
        }

        string outgoingPrompt = playerText;
        if (playerText != "start")
        {
            string reasonContext = string.IsNullOrEmpty(lockedReason) ? "NONE" : lockedReason;
            outgoingPrompt = "[Current Trust: " + Mathf.RoundToInt(currentTrust) + "/100 | Previous Topic: \"" + reasonContext + "\"]\nVisitor: \"" + playerText + "\"";
        }
        else
        {
            outgoingPrompt = "A secondary school student on the NYP Open House tour walks up to the server room door.";
        }

        chatHistory.Add(new OpenRouterMessage { role = "user", content = outgoingPrompt });

        OpenRouterRequest requestPayload = new OpenRouterRequest
        {
            model = modelName,
            messages = chatHistory,
            temperature = 0.75f
        };

        string jsonPayload = JsonUtility.ToJson(requestPayload);

        using (UnityWebRequest request = new UnityWebRequest(ApiUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + apiKey.Trim());
            request.SetRequestHeader("HTTP-Referer", "http://localhost");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("openrouter api error: " + request.error + "\n" + request.downloadHandler.text);
                onComplete?.Invoke(GetFallbackResponse(playerText));
                yield break;
            }

            string rawResponse = request.downloadHandler.text;
            string cleanJson;
            LLM_Minigame_ResponseData parsedData = ParseOpenRouterResponse(rawResponse, out cleanJson);

            if (parsedData != null)
            {
                chatHistory.Add(new OpenRouterMessage { role = "assistant", content = cleanJson });
                onComplete?.Invoke(parsedData);
            }
            else
            {
                Debug.LogWarning("failed to parse json from llm, using fallback");
                onComplete?.Invoke(GetFallbackResponse(playerText));
            }
        }
    }

    private LLM_Minigame_ResponseData ParseOpenRouterResponse(string apiJson, out string cleanJson)
    {
        cleanJson = "";
        try
        {
            OpenRouterResponse response = JsonUtility.FromJson<OpenRouterResponse>(apiJson);
            if (response == null || response.choices == null || response.choices.Count == 0)
            {
                return null;
            }

            string content = response.choices[0].message.content;
            content = content.Replace("```json", "").Replace("```", "").Trim();

            int firstBrace = content.IndexOf('{');
            int lastBrace = content.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                content = content.Substring(firstBrace, (lastBrace - firstBrace) + 1);
            }

            cleanJson = content;
            LLM_Minigame_ResponseData data = JsonUtility.FromJson<LLM_Minigame_ResponseData>(content);
            return data;
        }
        catch (Exception e)
        {
            Debug.LogError("json parse exception: " + e.Message);
            return null;
        }
    }

    public void ResetSteps()
    {
        chatHistory.Clear();
    }

    private LLM_Minigame_ResponseData GetFallbackResponse(string playerText)
    {
        LLM_Minigame_ResponseData fallback = new LLM_Minigame_ResponseData();
        string clean = playerText.ToLower().Trim();

        fallback.presetOptions = new LLM_Minigame_PresetOption[]
        {
            new LLM_Minigame_PresetOption { summaryText = "Lost Bag", fullDialogue = "My friend left their bag near the door during the morning tour." },
            new LLM_Minigame_PresetOption { summaryText = "Tour Guide", fullDialogue = "Our tour guide said we could ask to see the cloud racks." },
            new LLM_Minigame_PresetOption { summaryText = "Looks Cool", fullDialogue = "The glowing lights look like a sci-fi film, can I just look for ten seconds?" }
        };

        fallback.reasonTag = "GENERAL INQUIRY";
        fallback.isDeadEnd = false;
        fallback.deadEndReason = "";

        if (clean.Contains("win"))
        {
            fallback.guardDialogue = "You know about cloud servers already? Impressive. Quick look only, do not touch anything.";
            fallback.emotionKey = "Convinced";
            fallback.trustChange = 60f;
            fallback.reasonTag = "CLOUD KNOWLEDGE";
        }
        else if (clean.Contains("lose"))
        {
            fallback.guardDialogue = "That story has completely fallen apart. You are not making sense.";
            fallback.emotionKey = "Suspicious";
            fallback.trustChange = -15f;
            fallback.isDeadEnd = true;
            fallback.deadEndReason = "Contradicted earlier statements.";
        }
        else
        {
            fallback.guardDialogue = "The server room is off limits. What brings you over here?";
            fallback.emotionKey = "Neutral";
            fallback.trustChange = 0f;
        }

        return fallback;
    }
}