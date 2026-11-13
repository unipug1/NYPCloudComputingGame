using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// Saves sign-ups (name + email) and quest progress to Firebase Cloud Firestore.
// Uses Firestore's REST API (UnityWebRequest), so it works in WebGL builds,
// where the official Firebase Unity SDK does not.
//
// Firestore layout:  players/{sha256(email)} = { name, email, createdAt, done[], updatedAt }
// Put your Firebase Project ID + Web API key on this component (GameSystems > FirebaseService).
public class FirebaseService : MonoBehaviour
{
    public static FirebaseService Instance;

    [Header("Firebase project (Project settings > General)")]
    public string projectId = "";
    public string webApiKey = "";
    public string collection = "players";
    public int timeoutSeconds = 10;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(projectId) && !string.IsNullOrWhiteSpace(webApiKey);

    void Awake()
    {
        if (Instance != null && Instance != this) { enabled = false; return; }
        Instance = this;
    }

    string Base => $"https://firestore.googleapis.com/v1/projects/{projectId.Trim()}/databases/(default)/documents/{collection}";

    public static string DocId(string email)
    {
        using (var sha = SHA256.Create())
        {
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant()));
            var sb = new StringBuilder();
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }

    // ------------------------------------------------------------ sign up
    // callback(error) - error is null on success
    public void SignUp(string name, string email, Action<string> callback) => StartCoroutine(SignUpRoutine(name, email, callback));

    IEnumerator SignUpRoutine(string name, string email, Action<string> callback)
    {
        string url = $"{Base}?documentId={DocId(email)}&key={webApiKey.Trim()}";
        string body = "{\"fields\":{" +
                      "\"name\":{\"stringValue\":" + Json(name.Trim()) + "}," +
                      "\"email\":{\"stringValue\":" + Json(email.Trim().ToLowerInvariant()) + "}," +
                      "\"createdAt\":{\"timestampValue\":\"" + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") + "\"}," +
                      "\"done\":{\"arrayValue\":{}}" +
                      "}}";
        using (var req = Request(url, "POST", body))
        {
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success) { callback(null); yield break; }
            if (req.responseCode == 409) { callback("That email already has an account. Log in instead."); yield break; }
            callback(NetError(req));
        }
    }

    // ------------------------------------------------------------ login
    // callback(error, name, completedQuests)
    public void Login(string email, Action<string, string, List<string>> callback) => StartCoroutine(LoginRoutine(email, callback));

    IEnumerator LoginRoutine(string email, Action<string, string, List<string>> callback)
    {
        string url = $"{Base}/{DocId(email)}?key={webApiKey.Trim()}";
        using (var req = Request(url, "GET", null))
        {
            yield return req.SendWebRequest();
            if (req.responseCode == 404) { callback("No account with that email yet. Play as guest - you can sign up after your first quest.", null, null); yield break; }
            if (req.result != UnityWebRequest.Result.Success) { callback(NetError(req), null, null); yield break; }

            var doc = JsonUtility.FromJson<Doc>(req.downloadHandler.text);
            var done = new List<string>();
            if (doc?.fields?.done?.arrayValue?.values != null)
                foreach (var v in doc.fields.done.arrayValue.values) done.Add(v.stringValue);
            callback(null, doc?.fields?.name?.stringValue ?? "Player", done);
        }
    }

    // ------------------------------------------------------------ progress
    public void SaveProgress(string email, List<string> done) => StartCoroutine(SaveRoutine(email, done));

    IEnumerator SaveRoutine(string email, List<string> done)
    {
        string url = $"{Base}/{DocId(email)}?updateMask.fieldPaths=done&updateMask.fieldPaths=updatedAt&key={webApiKey.Trim()}";
        var vals = new StringBuilder();
        for (int i = 0; i < done.Count; i++) vals.Append(i > 0 ? "," : "").Append("{\"stringValue\":").Append(Json(done[i])).Append("}");
        string body = "{\"fields\":{" +
                      "\"done\":{\"arrayValue\":{\"values\":[" + vals + "]}}," +
                      "\"updatedAt\":{\"timestampValue\":\"" + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") + "\"}" +
                      "}}";
        using (var req = Request(url, "PATCH", body))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
                Debug.LogWarning("[Firebase] Couldn't save progress: " + NetError(req));
        }
    }

    // ------------------------------------------------------------ helpers
    UnityWebRequest Request(string url, string method, string body)
    {
        var req = new UnityWebRequest(url, method);
        if (body != null)
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.SetRequestHeader("Content-Type", "application/json");
        }
        req.downloadHandler = new DownloadHandlerBuffer();
        req.timeout = timeoutSeconds;
        return req;
    }

    static string NetError(UnityWebRequest req)
    {
        if (req.result == UnityWebRequest.Result.ConnectionError) return "Can't reach the server. Check the internet connection.";
        Debug.LogWarning($"[Firebase] {req.responseCode} {req.error}\n{req.downloadHandler?.text}");
        if (req.responseCode == 403) return "Server refused the request (check Firestore rules).";
        return "Something went wrong. Please try again.";
    }

    static string Json(string s)
    {
        var sb = new StringBuilder("\"");
        foreach (char c in s ?? "")
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                    break;
            }
        }
        return sb.Append('"').ToString();
    }

    // Firestore document shape (only the fields we read)
    [Serializable] class Doc { public Fields fields; }
    [Serializable] class Fields { public Str name; public Str email; public Arr done; }
    [Serializable] class Str { public string stringValue; }
    [Serializable] class Arr { public ArrInner arrayValue; }
    [Serializable] class ArrInner { public List<Str> values; }
}
