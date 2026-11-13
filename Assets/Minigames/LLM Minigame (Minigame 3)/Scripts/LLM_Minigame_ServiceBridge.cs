using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class LLM_Minigame_ChatMessage
{
    public string role;
    public string content;
}

[Serializable]
public class LLM_Minigame_BackendConfig
{
    public string backendUrl;
    public string gameKey;
}

[Serializable]
public class LLM_Minigame_TurnRequest
{
    public string requestId;
    public string sessionId;
    public string playerText;
    public float currentTrust;
    public string lockedReason;
    public List<LLM_Minigame_ChatMessage> history;
}

[Serializable]
public class LLM_Minigame_HistoryReply
{
    public string guardDialogue;
    public string reasonTag;
    public float trustChange;
    public bool isDeadEnd;
}

[Serializable]
public class LLM_Minigame_StreamEvent
{
    public string type;
    public string text;
    public string code;
    public string provider;
    public LLM_Minigame_ResponseData response;
}

// Decode network chunks without corrupting UTF-8 characters split across packets.
public class LLM_Minigame_StreamDownloadHandler : DownloadHandlerScript
{
    private readonly Decoder decoder = Encoding.UTF8.GetDecoder();
    private readonly StringBuilder pending = new StringBuilder();
    private readonly Action<LLM_Minigame_StreamEvent> onEvent;
    private int receivedBytes;
    public bool Failed { get; private set; }

    public LLM_Minigame_StreamDownloadHandler(Action<LLM_Minigame_StreamEvent> callback)
        : base(new byte[4096]) { onEvent = callback; }

    protected override bool ReceiveData(byte[] data, int dataLength)
    {
        if (data == null || dataLength <= 0) return true;
        receivedBytes += dataLength;
        if (receivedBytes > 131072) { Failed = true; return false; }
        char[] chars = new char[Encoding.UTF8.GetMaxCharCount(dataLength)];
        int count = decoder.GetChars(data, 0, dataLength, chars, 0, false);
        for (int i = 0; i < count; i++)
        {
            if (chars[i] == '\n')
            {
                string line = pending.ToString().Trim(); pending.Clear();
                if (line.Length == 0) continue;
                try
                {
                    LLM_Minigame_StreamEvent streamEvent = JsonUtility.FromJson<LLM_Minigame_StreamEvent>(line);
                    if (streamEvent != null) onEvent?.Invoke(streamEvent);
                }
                catch (Exception) { Failed = true; return false; }
            }
            else pending.Append(chars[i]);
        }
        return true;
    }

    protected override void CompleteContent()
    {
        if (pending.Length != 0) Failed = true;
    }
}

public class LLM_Minigame_ServiceBridge : MonoBehaviour
{
    [Header("AI backend (Vertex primary, OpenRouter backup)")]
    [SerializeField] private string backendUrl = "";
    [SerializeField] private int timeoutSeconds = 40;

    private readonly List<LLM_Minigame_ChatMessage> chatHistory = new List<LLM_Minigame_ChatMessage>();
    private string sessionId = Guid.NewGuid().ToString();
    private string gameKey = "";
    private UnityWebRequest activeRequest;
    private int generation;

    private void Awake()
    {
        // Kiosk credentials live outside Assets, never in scenes or source control.
        string path = Path.Combine(Application.persistentDataPath, "nyp-ai-config.json");
#if UNITY_EDITOR
        string editorPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../UserSettings/nyp-ai-config.json"));
        if (File.Exists(editorPath)) path = editorPath;
#endif
        if (File.Exists(path))
        {
            try
            {
                LLM_Minigame_BackendConfig config = JsonUtility.FromJson<LLM_Minigame_BackendConfig>(File.ReadAllText(path));
                if (config != null) { backendUrl = config.backendUrl; gameKey = config.gameKey; }
            }
            catch (Exception) { Debug.LogWarning("AI configuration could not be read; using offline dialogue."); }
        }
        string environmentUrl = Environment.GetEnvironmentVariable("NYP_AI_URL");
        string environmentKey = Environment.GetEnvironmentVariable("NYP_AI_GAME_KEY");
        if (!string.IsNullOrEmpty(environmentUrl)) backendUrl = environmentUrl;
        if (!string.IsNullOrEmpty(environmentKey)) gameKey = environmentKey;
    }

    public LLM_Minigame_ResponseData GetInstantOpeningLine()
    {
        chatHistory.Clear();
        return new LLM_Minigame_ResponseData
        {
            guardDialogue = "The server room is <color=#E60012>off limits</color> to visitors. Did your <color=#00A3FF>tour guide</color> send you here, or did someone leave their <color=#00A3FF>bag</color> inside?",
            emotionKey = "Neutral", trustChange = 0f, reasonTag = "", isDeadEnd = false, deadEndReason = "",
            presetOptions = DefaultPresets()
        };
    }

    public void SendPlayerMessage(string playerText, float currentTrust, string lockedReason,
        Action<LLM_Minigame_ResponseData> onComplete, Action<string> onProgress = null, Action onStreamReset = null)
    {
        if (activeRequest != null) return;
        StartCoroutine(SendBackendRequest(playerText, currentTrust, lockedReason, onComplete, onProgress, onStreamReset));
    }

    public void SendPlayerMessage(string playerText, float currentTrust, Action<LLM_Minigame_ResponseData> onComplete)
    { SendPlayerMessage(playerText, currentTrust, "", onComplete); }

    public void SendPlayerMessage(string playerText, Action<LLM_Minigame_ResponseData> onComplete)
    { SendPlayerMessage(playerText, 10f, "", onComplete); }

    private IEnumerator SendBackendRequest(string playerText, float currentTrust, string lockedReason,
        Action<LLM_Minigame_ResponseData> onComplete, Action<string> onProgress, Action onStreamReset)
    {
        Uri endpoint;
        if (string.IsNullOrEmpty(gameKey) || !Uri.TryCreate(backendUrl, UriKind.Absolute, out endpoint) ||
            (endpoint.Scheme != "https" && !(endpoint.IsLoopback && endpoint.Scheme == "http")))
        {
            Debug.LogWarning("AI backend is not configured; using offline dialogue.");
            onComplete?.Invoke(GetOfflineResponse()); yield break;
        }
        int requestGeneration = generation;
        LLM_Minigame_ResponseData completedResponse = null;
        string errorCode = "";
        LLM_Minigame_TurnRequest payload = new LLM_Minigame_TurnRequest
        {
            requestId = Guid.NewGuid().ToString(), sessionId = sessionId,
            playerText = playerText, currentTrust = Mathf.Clamp(currentTrust, 0f, 100f),
            lockedReason = lockedReason ?? "", history = chatHistory
        };
        string url = backendUrl.TrimEnd('/') + "/v1/turn";
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            activeRequest = request;
            request.timeout = Mathf.Clamp(timeoutSeconds, 10, 60);
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload)));
            LLM_Minigame_StreamDownloadHandler handler = new LLM_Minigame_StreamDownloadHandler(streamEvent =>
            {
                if (requestGeneration != generation) return;
                if (streamEvent.type == "dialogue") onProgress?.Invoke(streamEvent.text);
                else if (streamEvent.type == "reset") onStreamReset?.Invoke();
                else if (streamEvent.type == "result") completedResponse = streamEvent.response;
                else if (streamEvent.type == "error") { errorCode = streamEvent.code; completedResponse = null; }
            });
            request.downloadHandler = handler;
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("X-Game-Key", gameKey);
            yield return request.SendWebRequest();
            activeRequest = null;
            if (requestGeneration != generation) yield break;
            if (request.result != UnityWebRequest.Result.Success || handler.Failed || !IsValid(completedResponse))
            {
                Debug.LogWarning("AI unavailable (" + request.responseCode + ", " + errorCode + "); using offline dialogue.");
                completedResponse = GetOfflineResponse();
            }
        }
        chatHistory.Add(new LLM_Minigame_ChatMessage { role = "user", content = playerText });
        // Choices need not be resent on later turns. Keep the narrative context compact.
        LLM_Minigame_HistoryReply historyReply = new LLM_Minigame_HistoryReply
        {
            guardDialogue = completedResponse.guardDialogue, reasonTag = completedResponse.reasonTag,
            trustChange = completedResponse.trustChange, isDeadEnd = completedResponse.isDeadEnd
        };
        chatHistory.Add(new LLM_Minigame_ChatMessage { role = "assistant", content = JsonUtility.ToJson(historyReply) });
        while (chatHistory.Count > 8) chatHistory.RemoveRange(0, 2);
        onComplete?.Invoke(completedResponse);
    }

    private static bool IsValid(LLM_Minigame_ResponseData response)
    {
        return response != null && !string.IsNullOrWhiteSpace(response.guardDialogue) &&
            response.guardDialogue.Length <= 600 && response.presetOptions != null && response.presetOptions.Length == 3 &&
            !float.IsNaN(response.trustChange) && !float.IsInfinity(response.trustChange);
    }

    public void ResetSteps()
    {
        generation++;
        if (activeRequest != null) { activeRequest.Abort(); activeRequest.Dispose(); activeRequest = null; }
        StopAllCoroutines();
        chatHistory.Clear();
        sessionId = Guid.NewGuid().ToString();
    }

    private void OnDisable() { ResetSteps(); }

    private static LLM_Minigame_PresetOption[] DefaultPresets()
    {
        return new[]
        {
            new LLM_Minigame_PresetOption { summaryText = "Tour Guide", fullDialogue = "Yes, our tour guide said we could ask you for a quick look inside!" },
            new LLM_Minigame_PresetOption { summaryText = "Lost Bag", fullDialogue = "My friend left their bag by the door during the morning tour, I just need to grab it." },
            new LLM_Minigame_PresetOption { summaryText = "Looks Cool", fullDialogue = "Neither, Sir, the glowing servers just look really cool and I wanted to see them." }
        };
    }

    private static LLM_Minigame_ResponseData GetOfflineResponse()
    {
        return new LLM_Minigame_ResponseData
        {
            guardDialogue = "The connection is taking a break. Stay with your tour guide and try me again in a moment.",
            emotionKey = "Neutral", trustChange = 0f, reasonTag = "", isDeadEnd = false,
            deadEndReason = "", presetOptions = DefaultPresets()
        };
    }
}
