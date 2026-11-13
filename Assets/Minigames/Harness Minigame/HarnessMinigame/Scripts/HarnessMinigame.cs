using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// "Rebuild the Booth Page" - AI website builder harness mini-game (image-based version).
// Each screen = one background Image + separate button/card Images on top.
// Card + phone sprites are loaded by name from  Assets/Resources/Harness/...
// so you only drag the Image objects in, not every sprite.
public class HarnessMinigame : MonoBehaviour
{
    [Header("Panels (one per screen)")]
    public GameObject storyPanel;
    public GameObject builderPanel;
    public GameObject recapPanel;
    public GameObject quizPanel;
    public GameObject yourTurnPanel;
    public GameObject handoffPanel;
    public GameObject hud;                       // meter fill + % (shown while the mini-game is open)

    [Header("Builder")]
    public Image[] roleCards = new Image[3];     // Image components, in order 0,1,2
    public Image[] briefCards = new Image[3];
    public Image[] rulesCards = new Image[3];
    public Image phoneScreen;                    // sits BEHIND the phone_frame image
    public TMP_Text meiText;                     // text inside the dialogue box
    public TMP_Text checklistText;
    public GameObject generateButton;            // btn_generate / btn_generate_again
    public GameObject builderContinueButton;     // btn_continue_wide (hidden at start)

    [Header("Quiz")]
    public Image[] answerCards = new Image[3];
    public TMP_Text explanationText;
    public GameObject quizContinueButton;
    public int correctAnswer = 1;
    [TextArea] public string explanation = "The AI worked fine. It just followed its instructions, which told it to act like a poet. Good instructions lead to good results.";

    [Header("Your Turn")]
    public Image[] topicCards = new Image[4];
    public Image[] vibeCards = new Image[3];
    public GameObject[] swatchRings = new GameObject[4];
    public Image[] mustCards = new Image[4];
    public TMP_Text templateText;                // inside the prompt box
    public TMP_Text yourTurnError;

    [Header("Handoff")]
    public TMP_Text handoffPromptText;
    public string websiteUrl = "https://your-friends-site.com";

    [Header("Quest")]
    public QuestCompleteTrigger questTrigger;

    // ---------- state ----------
    int role = -1, brief = -1, rules = -1, topic = -1, vibe = -1, colour = -1;
    readonly List<int> must = new List<int>();
    bool questDone, generating;
    Sprite[] pages;          // 27 preview pages, index = role*9 + brief*3 + rules
    Sprite screenEmpty, screenLoading;

    readonly string[] topicText = { "a page for my CCA: [type your CCA name]", "a page about my hobby: [type your hobby]", "a page for my dream business: [type your idea]", "a page for an event: [type the event]" };
    readonly string[] vibeText = { "clear and simple", "fun and playful", "bold and exciting" };
    readonly string[] colourText = { "blue", "purple", "red", "green" };
    readonly string[] mustText = { "a big button", "a photo section", "a contact section", "a schedule" };

    [Header("Start")]
    public bool startHidden = false;             // false = opens as soon as the Harness scene loads
    [Tooltip("Must match the quest name in QuestManager")]
    public string questName = "Harness Game";

    void Start()
    {
        if (startHidden) ShowOnly(null);
        else Open();
    }

    void Awake()
    {
        pages = Resources.LoadAll<Sprite>("Harness/Phone/Pages");
        System.Array.Sort(pages, (a, b) => string.Compare(a.name, b.name));   // page_00 ... page_26
        screenEmpty = Resources.Load<Sprite>("Harness/Phone/screen_empty");
        screenLoading = Resources.Load<Sprite>("Harness/Phone/screen_loading");
        if (pages.Length != 27) Debug.LogWarning("[Harness] Expected 27 preview pages in Resources/Harness/Phone/Pages, found " + pages.Length);
    }

    // =========================================================
    // NAVIGATION
    // =========================================================
    public void Open()                       // call from the NPC / terminal
    {
        role = brief = rules = topic = vibe = colour = -1;
        must.Clear();
        // start at the story screen, or the builder if this scene has no story panel
        if (storyPanel != null) ShowOnly(storyPanel);
        else GoToBuilder();
    }

    // "Back to the game" button: leave the Harness scene and go back to the map
    public void Close()
    {
        ShowOnly(null);
        if (QuestNavigator.Instance != null) QuestNavigator.ExitToMap();
    }

    public void GoToBuilder()
    {
        ShowOnly(builderPanel);
        SetRow(roleCards, "Builder/role", -1, "selected");
        SetRow(briefCards, "Builder/brief", -1, "selected");
        SetRow(rulesCards, "Builder/rules", -1, "selected");
        generating = false;
        if (phoneScreen) phoneScreen.sprite = screenEmpty;
        if (checklistText) checklistText.text = "";
        if (generateButton) generateButton.SetActive(true);
        if (builderContinueButton) builderContinueButton.SetActive(false);
        if (meiText) meiText.text = "Think of it like briefing a new helper: who should they be, what should they make, and what rules should they follow?";
    }

    public void GoToRecap() => ShowOnly(recapPanel);

    public void GoToQuiz()
    {
        ShowOnly(quizPanel);
        for (int i = 0; i < answerCards.Length; i++) SetCard(answerCards[i], "Quiz/answer_" + "ABC"[i], "normal");
        if (explanationText) explanationText.text = "";
        if (quizContinueButton) quizContinueButton.SetActive(false);
    }

    public void GoToYourTurn()
    {
        if (!questDone)   // quest complete -> Cloud Network bar goes up
        {
            if (QuestManager.Instance != null) QuestManager.Instance.CompleteQuest(questName);
            else if (questTrigger != null) questTrigger.Complete();
            questDone = true;
        }
        ShowOnly(yourTurnPanel);
        SetRow(topicCards, "YourTurn/topic", -1, "selected");
        SetRow(vibeCards, "YourTurn/vibe", -1, "selected");
        SetRow(mustCards, "YourTurn/must", -1, "selected");
        foreach (var r in swatchRings) if (r) r.SetActive(false);
        if (yourTurnError) yourTurnError.text = "";
        UpdateTemplate();
    }

    void ShowOnly(GameObject panel)
    {
        foreach (var p in new[] { storyPanel, builderPanel, recapPanel, quizPanel, yourTurnPanel, handoffPanel })
            if (p != null) p.SetActive(p == panel);
        if (hud != null) hud.SetActive(panel != null);
    }

    // =========================================================
    // BUILDER (card Button OnClick with int 0/1/2)
    // =========================================================
    public void SelectRole(int i)  { role = i;  SetRow(roleCards, "Builder/role", i, "selected"); }
    public void SelectBrief(int i) { brief = i; SetRow(briefCards, "Builder/brief", i, "selected"); }
    public void SelectRules(int i) { rules = i; SetRow(rulesCards, "Builder/rules", i, "selected"); }

    public void Generate()
    {
        if (generating) return;
        if (role < 0 || brief < 0 || rules < 0) { if (meiText) meiText.text = "Pick one card in every row first!"; return; }
        generating = true;
        StartCoroutine(GenerateRoutine());
    }

    IEnumerator GenerateRoutine()
    {
        if (phoneScreen) phoneScreen.sprite = screenLoading;
        yield return new WaitForSeconds(1f);
        int page = role * 9 + brief * 3 + rules;
        if (phoneScreen && pages != null && page < pages.Length) phoneScreen.sprite = pages[page];
        generating = false;

        bool roleOk = role == 0, briefOk = brief == 0, rulesOk = rules == 0;
        SetRow(roleCards, "Builder/role", role, roleOk ? "correct" : "wrong");
        SetRow(briefCards, "Builder/brief", brief, briefOk ? "correct" : "wrong");
        SetRow(rulesCards, "Builder/rules", rules, rulesOk ? "correct" : "wrong");

        if (checklistText) checklistText.text = Mark(briefOk) + " Shows booth info\n" + Mark(roleOk) + " Short, friendly words\n" + Mark(rulesOk) + " Readable on a phone";

        if (roleOk && briefOk && rulesOk)
        {
            if (meiText) meiText.text = "Perfect, visitors can find us now!";
            generateButton.SetActive(false);
            builderContinueButton.SetActive(true);
        }
        else
        {
            var bad = new List<string>();
            if (!roleOk) bad.Add("ROLE");
            if (!briefOk) bad.Add("BRIEF");
            if (!rulesOk) bad.Add("RULES");
            if (meiText) meiText.text = "Hmm, not quite. Check the " + string.Join(" and ", bad) + " row, then generate again.";
        }
    }

    string Mark(bool ok) => ok ? "<color=#2EAA64>✔</color>" : "<color=#E24C46>✘</color>";

    // =========================================================
    // QUIZ (answer Button OnClick with int 0/1/2)
    // =========================================================
    public void Answer(int i)
    {
        SetCard(answerCards[correctAnswer], "Quiz/answer_" + "ABC"[correctAnswer], "correct");
        if (i != correctAnswer) SetCard(answerCards[i], "Quiz/answer_" + "ABC"[i], "wrong");
        if (explanationText) explanationText.text = (i == correctAnswer ? "<color=#2EAA64>Correct!</color> " : "<color=#B8780F>Not quite.</color> ") + explanation;
        if (quizContinueButton) quizContinueButton.SetActive(true);
    }

    // =========================================================
    // YOUR TURN
    // =========================================================
    public void SelectTopic(int i) { topic = i; SetRow(topicCards, "YourTurn/topic", i, "selected"); UpdateTemplate(); }
    public void SelectVibe(int i)  { vibe = i;  SetRow(vibeCards, "YourTurn/vibe", i, "selected");  UpdateTemplate(); }

    public void SelectColour(int i)
    {
        colour = i;
        for (int k = 0; k < swatchRings.Length; k++) if (swatchRings[k]) swatchRings[k].SetActive(k == i);
        UpdateTemplate();
    }

    public void ToggleMust(int i)    // up to 2
    {
        if (must.Contains(i)) must.Remove(i);
        else { if (must.Count >= 2) must.RemoveAt(0); must.Add(i); }
        for (int k = 0; k < mustCards.Length; k++) SetCard(mustCards[k], "YourTurn/must_" + k, must.Contains(k) ? "selected" : "normal");
        UpdateTemplate();
    }

    bool TemplateDone => topic >= 0 && vibe >= 0 && colour >= 0 && must.Count > 0;

    string BuildPrompt(bool rich)
    {
        string v = vibe >= 0 ? vibeText[vibe] : "[pick a vibe]";
        string t = topic >= 0 ? topicText[topic] : "[pick what it's about]";
        string c = colour >= 0 ? colourText[colour] : "[pick a colour]";
        string m = must.Count > 0 ? string.Join(" and ", must.ConvertAll(x => mustText[x])) : "[pick up to 2]";
        if (rich) t = t.Replace("[", "<color=#8CB9EC>[").Replace("]", "]</color>");
        string R = rich ? "<color=#F2B33D>Role:</color>" : "Role:";
        string B = rich ? "<color=#F2B33D>Brief:</color>" : "Brief:";
        string U = rich ? "<color=#F2B33D>Rules:</color>" : "Rules:";
        return $"{R} You're a web designer. Write short, {v} text.\n{B} Make {t}. Include {m}.\n{U} Phone-friendly, {c} colour theme, {v} style.";
    }

    void UpdateTemplate() { if (templateText) templateText.text = BuildPrompt(true); if (yourTurnError) yourTurnError.text = ""; }

    public void BuildItForReal()
    {
        if (!TemplateDone) { if (yourTurnError) yourTurnError.text = "Pick an option in every section first."; return; }
        ShowOnly(handoffPanel);
        if (handoffPromptText) handoffPromptText.text = BuildPrompt(true);
    }

    public void OpenWebsite()
    {
        string prompt = UnityEngine.Networking.UnityWebRequest.EscapeURL(BuildPrompt(false));
        Application.OpenURL(websiteUrl + "?prompt=" + prompt);
    }

    // =========================================================
    // HELPERS - cards load their sprite by name from Resources/Harness/Cards/
    // =========================================================
    void SetCard(Image img, string baseName, string state)
    {
        if (img == null) return;
        var s = Resources.Load<Sprite>("Harness/Cards/" + baseName + "_" + state);
        if (s != null) img.sprite = s;
        else Debug.LogWarning("[Harness] Missing sprite: Harness/Cards/" + baseName + "_" + state);
    }

    // picked card gets 'state', every other card in the row goes back to normal
    void SetRow(Image[] cards, string baseName, int picked, string state)
    {
        if (cards == null) return;
        for (int k = 0; k < cards.Length; k++) SetCard(cards[k], baseName + "_" + k, k == picked ? state : "normal");
    }
}
