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

    [Header("system prompt (edit personality & rules here)")]
    [TextArea(12, 28)]
    [SerializeField]
    private string systemPrompt =
@"CONTEXT:
- You are Mr. Tan, a lecturer guarding the School of IT server room at NYP during Open House.
- The player is a secondary school or ITE student on a campus tour.
- You are strict about safety, BUT your secret goal as a lecturer is to encourage enthusiastic prospective students to join your diploma.

CRITICAL RULES:
- Reply in 1 to 2 short sentences ONLY. Use simple, natural, friendly conversational English.
- NEVER ask for physical items (NO cards, IC, passes, or forms). The player has no inventory.
- NEVER say you will call security or leave. You decide face-to-face.
- NEVER use asterisks or action narration like *sighs*. Spoken dialogue only.
- HINT RULE: In your reply, always drop a small clue on how they can convince you (e.g. mention that the Open House website is down, that you are busy with server lag, or that only passionate EAE applicants get special tours).

TRUST SCORING (VERY FORGIVING FOR STUDENTS):
- +60 (Instant Win): They mention EAE, wanting to join NYP Cloud, or helping with the Open House website crash (emotionKey: 'Convinced').
- +40: They show curiosity about tech, ask polite questions, or offer to help carry cables/assist (emotionKey: 'Convinced').
- +20: Any polite, honest student answer or asking for a quick peek (emotionKey: 'Thinking').
- 0: Unclear chatter, silence, or random questions (emotionKey: 'Neutral').
- -5 to -10: ONLY for obvious keyboard spam like 'asdfghjk' or rude insults (emotionKey: 'Suspicious').

PHONE PRESET BUTTONS (MUST BE SIMPLE & CLEAR):
Invent 3 fresh options tailored directly to what you said.
The 'summaryText' on the button MUST be an obvious 2-word ACTION tag so secondary school kids immediately know what it means:
- Button 1 (EAE/Student route): summaryText like 'Say For EAE', 'Want To Join', or 'Love Tech'.
- Button 2 (Helpful route): summaryText like 'Offer Help', 'Fix Website', or 'Carry Stuff'.
- Button 3 (Polite request): summaryText like 'Ask Nicely', 'Quick Peek', or 'Just 1 Min'.
'fullDialogue' must be 1 clear, natural sentence a secondary school student would say.

COLORED TEXT RULES:
- Use <color=#E60012>words</color> (Red) for warnings (e.g. <color=#E60012>restricted</color>, <color=#E60012>off limits</color>).
- Use <color=#00A3FF>words</color> (Cyan) for hints or tech terms (e.g. <color=#00A3FF>website crash</color>, <color=#00A3FF>EAE students</color>).
- Highlight maximum 1 or 2 words per line. Always close with </color>.

JSON SCHEMA (no markdown, no backticks):
{
  ""guardDialogue"": ""1 to 2 short sentences with hints."",
  ""emotionKey"": ""Neutral"",
  ""trustChange"": 20,
  ""presetOptions"": [
    { ""summaryText"": ""Say For EAE"", ""fullDialogue"": ""Cher, I am applying for NYP Cloud through EAE, can I please see the setup?"" },
    { ""summaryText"": ""Offer Help"", ""fullDialogue"": ""I heard the Open House website is lagging, can I help you check the servers?"" },
    { ""summaryText"": ""Quick Peek"", ""fullDialogue"": ""Sir, I am really interested in IT, can I just get a quick thirty second tour?"" }
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
            guardDialogue = "Hold on right there. The <color=#00A3FF>server room</color> is strictly <color=#E60012>off limits</color> to Open House tour visitors. What brings you here?",
            emotionKey = "Neutral",
            trustChange = 0f,
            presetOptions = new LLM_Minigame_PresetOption[]
            {
                new LLM_Minigame_PresetOption { summaryText = "Server Crash", fullDialogue = "I noticed the NYP Open House website is down. Is rack 2 overheating?" },
                new LLM_Minigame_PresetOption { summaryText = "EAE Interest", fullDialogue = "I am applying for NYP Cloud Computing through EAE and really want to see the enterprise setup." },
                new LLM_Minigame_PresetOption { summaryText = "Offer Help", fullDialogue = "I built cloud projects in secondary school, maybe I can spot why the network crashed." }
            }
        };

        string openingJson = "{\"guardDialogue\":\"" + openingData.guardDialogue + "\",\"emotionKey\":\"Neutral\",\"trustChange\":0}";
        chatHistory.Add(new OpenRouterMessage { role = "assistant", content = openingJson });

        return openingData;
    }

    // overloaded to accept current trust
    public void SendPlayerMessage(string playerText, float currentTrust, Action<LLM_Minigame_ResponseData> onComplete)
    {
        StartCoroutine(SendOpenRouterRequest(playerText, currentTrust, onComplete));
    }

    // backwards compatibility for initial start
    public void SendPlayerMessage(string playerText, Action<LLM_Minigame_ResponseData> onComplete)
    {
        StartCoroutine(SendOpenRouterRequest(playerText, 10f, onComplete));
    }

    private IEnumerator SendOpenRouterRequest(string playerText, float currentTrust, Action<LLM_Minigame_ResponseData> onComplete)
    {
        if (chatHistory.Count == 0)
        {
            chatHistory.Add(new OpenRouterMessage { role = "system", content = systemPrompt });
        }

        // provide the current trust level to the ai behind the scenes
        string outgoingPrompt = playerText;
        if (playerText != "start")
        {
            outgoingPrompt = "[Current Trust: " + Mathf.RoundToInt(currentTrust) + "/100]\nVisitor: \"" + playerText + "\"";
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
            new LLM_Minigame_PresetOption { summaryText = "Server Crash", fullDialogue = "I noticed the NYP Open House website is down. Is rack 2 overheating?" },
            new LLM_Minigame_PresetOption { summaryText = "EAE Interest", fullDialogue = "I am applying for NYP Cloud Computing through EAE and really want to see the enterprise setup." },
            new LLM_Minigame_PresetOption { summaryText = "Offer Help", fullDialogue = "I built cloud projects in secondary school, maybe I can spot why the network crashed." }
        };

        if (clean.Contains("win"))
        {
            fallback.guardDialogue = "You know about cloud deployment already? Impressive for a secondary school student. Take a quick look inside.";
            fallback.emotionKey = "Convinced";
            fallback.trustChange = 60f;
        }
        else if (clean.Contains("lose"))
        {
            fallback.guardDialogue = "This is a restricted enterprise server room, not a photo booth. Head back to the tour.";
            fallback.emotionKey = "Suspicious";
            fallback.trustChange = -10f;
        }
        else
        {
            fallback.guardDialogue = "Welcome to NYP, but the server room is off limits to tour visitors. What brings you here?";
            fallback.emotionKey = "Neutral";
            fallback.trustChange = 0f;
        }

        return fallback;
    }
}