using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

[Serializable]
public class LLM_Minigame_TelemetryEntry
{
    public string sessionId;
    public int turn;
    public string inputMode;
    public string playerInput;
    public string reasonTag;
    public string llmResponse;
    public string emotion;
    public float trustDelta;
    public float totalTrust;
    public bool isDeadEnd;
    public string deadEndReason;
    public bool isWon;
}

public class LLM_Minigame_Telemetry : MonoBehaviour
{
    [Header("google sheets webhook url")]
    [SerializeField] private string googleAppsScriptUrl = "PASTE_YOUR_EXEC_URL_HERE";

    [Header("settings")]
    [SerializeField] private bool enableLogging = true;

    private string currentSessionId;
    private int currentTurnIndex = 0;

    private void Awake()
    {
        StartNewSession();
    }

    public void StartNewSession()
    {
        // creates a short unique id for this player run
        currentSessionId = Guid.NewGuid().ToString().Substring(0, 8).ToUpper();
        currentTurnIndex = 0;
    }

    public void RecordTurn(
        string inputMode,
        string playerText,
        string reasonTag,
        string guardReply,
        string emotionKey,
        float trustDelta,
        float totalTrust,
        bool isDeadEnd,
        string deadEndReason,
        bool hasWon)
    {
        if (!enableLogging || string.IsNullOrEmpty(googleAppsScriptUrl) || googleAppsScriptUrl.Contains("PASTE_YOUR"))
        {
            return;
        }

        currentTurnIndex++;

        LLM_Minigame_TelemetryEntry entry = new LLM_Minigame_TelemetryEntry
        {
            sessionId = currentSessionId,
            turn = currentTurnIndex,
            inputMode = inputMode,
            playerInput = playerText,
            reasonTag = reasonTag,
            llmResponse = guardReply,
            emotion = emotionKey,
            trustDelta = trustDelta,
            totalTrust = totalTrust,
            isDeadEnd = isDeadEnd,
            deadEndReason = deadEndReason,
            isWon = hasWon
        };

        StartCoroutine(SendPostRequest(entry));
    }

    private IEnumerator SendPostRequest(LLM_Minigame_TelemetryEntry entry)
    {
        string json = JsonUtility.ToJson(entry);
        byte[] rawBody = Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest request = new UnityWebRequest(googleAppsScriptUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(rawBody);
            request.downloadHandler = new DownloadHandlerBuffer();

            // crucial for webgl: text/plain avoids browser cors options preflight
            request.SetRequestHeader("Content-Type", "text/plain");

            yield return request.SendWebRequest();

            // in webgl, the browser might log a redirect warning on the response, 
            // but the post request will have already executed on google's server
            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                Debug.LogWarning("telemetry connection error: " + request.error);
            }
        }
    }
}