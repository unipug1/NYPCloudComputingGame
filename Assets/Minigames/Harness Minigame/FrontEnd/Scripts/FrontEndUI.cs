using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// Home screen (Login / Play as guest / Credits) and the in-game sign-up pop-up.
// Sign-up is NOT on the home screen: guests are asked to sign up on the map after finishing
// their 1st quest (save progress) and their 3rd quest (claim the reward). Built by Tools > Front End > Build.
// Saves sign-ups to Firebase when GameSystems > FirebaseService has a Project ID + API key,
// otherwise falls back to saving on this device only.
public class FrontEndUI : MonoBehaviour
{
    [Header("Roots")]
    public GameObject frontEndRoot;     // whole home screen (background + panels)
    public GameObject gameHudRoot;      // progress bar + quest panel (shown after Play)

    [Header("Panels")]
    public GameObject homePanel;
    public GameObject loginPanel;
    public GameObject signupPanel;
    public GameObject creditsPanel;

    [Header("Home buttons")]
    public GameObject loggedOutButtons; // LOGIN / SIGN UP / PLAY AS GUEST / CREDITS
    public GameObject loggedInButtons;  // PLAY / CREDITS / LOG OUT
    public TMP_Text welcomeText;

    [Header("Login")]
    public TMP_InputField loginEmail;
    public TMP_Text loginError;

    [Header("Sign up")]
    public TMP_InputField signupName;
    public TMP_InputField signupEmail;
    public TMP_Text signupError;

    [Header("HUD")]
    public TMP_Text playerNameText;

    [Header("Sign-up pop-up (guests, on the map, after finishing quests)")]
    public GameObject homeBackground;           // hidden while the pop-up shows over the game
    public TMP_Text signupTitle;
    public TMP_Text signupReason;
    public TMP_Text loginAltLabel;              // bottom button on the login panel
    [Tooltip("Ask guests to sign up after this many quests are done. The last number is the reward prompt.")]
    public int[] askToSignUpAfter = { 1, 3 };
    const string ASKED_KEY = "nyp_oh_signup_asked";
    bool inGamePopup;                            // sign-up / login opened from the game, not the home screen

    bool busy;
    static bool startedThisSession;     // don't show the home screen again when coming back from a minigame

    FirebaseService Fb => FirebaseService.Instance != null && FirebaseService.Instance.IsConfigured ? FirebaseService.Instance : null;

    void Start()
    {
        if (QuestManager.Instance != null) QuestManager.Instance.OnQuestsChanged += CheckSignupPrompt;
        SceneManager.sceneLoaded += OnSceneLoaded;

        if (startedThisSession) { HideFrontEnd(); return; }

        // testing a minigame scene directly (not the map): skip the home screen, play as the last player / guest
        if (!InMap())
        {
            AccountManager.RestoreSession();
            if (!AccountManager.LoggedIn) AccountManager.PlayAsGuest();
            StartGame(null);
            return;
        }
        AccountManager.RestoreSession();
        frontEndRoot.SetActive(true);
        if (gameHudRoot) gameHudRoot.SetActive(false);
        BlockPlayer(true);
        ShowHome();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (QuestManager.Instance != null) QuestManager.Instance.OnQuestsChanged -= CheckSignupPrompt;
    }

    static bool InMap()
    {
        var nav = QuestNavigator.Instance;
        return nav == null || SceneManager.GetActiveScene().name == nav.mapSceneName;
    }

    // ---------------- navigation ----------------
    void Show(GameObject panel)
    {
        homePanel.SetActive(panel == homePanel);
        loginPanel.SetActive(panel == loginPanel);
        signupPanel.SetActive(panel == signupPanel);
        creditsPanel.SetActive(panel == creditsPanel);
    }

    public void ShowHome()
    {
        Show(homePanel);
        bool inGame = AccountManager.LoggedIn;
        loggedOutButtons.SetActive(!inGame);
        loggedInButtons.SetActive(inGame);
        welcomeText.text = inGame ? "Welcome back, " + AccountManager.DisplayName + "!" : "";
    }

    public void ShowLogin()
    {
        Show(loginPanel);
        if (loginAltLabel) loginAltLabel.text = inGamePopup ? "NEW HERE? SIGN UP" : "NEW HERE? PLAY AS GUEST";
        loginError.text = "";
        loginEmail.text = "";
        loginEmail.Select();
    }

    public void ShowSignup()
    {
        Show(signupPanel);
        signupError.text = "";
        signupName.text = ""; signupEmail.text = "";
        signupName.Select();
    }

    public void ShowCredits() => Show(creditsPanel);

    // login panel: back arrow
    public void LoginBack()
    {
        if (inGamePopup) Show(signupPanel); else ShowHome();
    }

    // login panel: bottom button ("play as guest" on the home screen, "sign up" during the game)
    public void LoginAlt()
    {
        if (inGamePopup) Show(signupPanel); else PlayAsGuest();
    }

    // ---------------- sign-up pop-up during the game ----------------
    void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        if (isActiveAndEnabled) StartCoroutine(CheckSoon());
    }

    IEnumerator CheckSoon()
    {
        yield return new WaitForSeconds(0.6f);   // let the map load and the player arrive first
        CheckSignupPrompt();
    }

    // guests only, only on the map, once per milestone
    void CheckSignupPrompt()
    {
        if (AccountManager.LoggedIn || !startedThisSession || frontEndRoot.activeSelf || !InMap()) return;
        var qm = QuestManager.Instance;
        if (qm == null || askToSignUpAfter == null || askToSignUpAfter.Length == 0) return;

        int done = qm.CompletedNames().Count;
        int asked = PlayerPrefs.GetInt(ASKED_KEY, 0);
        int due = 0, last = 0;
        foreach (var n in askToSignUpAfter)
        {
            last = Mathf.Max(last, n);
            if (done >= n && n > asked) due = Mathf.Max(due, n);
        }
        if (due == 0) return;

        PlayerPrefs.SetInt(ASKED_KEY, due); PlayerPrefs.Save();
        if (due >= last)
            OpenSignupPopup("CLAIM YOUR REWARD",
                "You cleared " + done + " quests! Sign up so we can send your e-certificate. Your progress comes with you.");
        else
            OpenSignupPopup("SAVE YOUR PROGRESS",
                "Quest complete! Sign up to keep your progress and get your e-certificate when you finish.");
    }

    [ContextMenu("Test: show sign-up pop-up")]
    public void TestSignupPopup() => OpenSignupPopup("SAVE YOUR PROGRESS", "Quest complete! Sign up to keep your progress and get your e-certificate when you finish.");

    public void OpenSignupPopup(string title, string reason)
    {
        inGamePopup = true;
        frontEndRoot.SetActive(true);
        if (homeBackground) homeBackground.SetActive(false);   // show the game behind the dimmed pop-up
        if (signupTitle) signupTitle.text = title;
        if (signupReason) signupReason.text = reason;
        BlockPlayer(true);
        ShowSignup();
    }

    // "Maybe later" / back arrow on the sign-up panel
    public void SkipSignup()
    {
        if (inGamePopup) CloseGamePopup(); else ShowHome();
    }

    void CloseGamePopup()
    {
        inGamePopup = false;
        if (homeBackground) homeBackground.SetActive(true);
        HideFrontEnd();
    }

    // after signing up / logging in from the pop-up: keep the quests done as a guest
    void JoinFromGame(List<string> onlineProgress)
    {
        var qm = QuestManager.Instance;
        if (qm != null)
        {
            var guestDone = qm.CompletedNames();
            qm.LoadProgress(AccountManager.Current.email);
            qm.MergeCompleted(onlineProgress);
            qm.MergeCompleted(guestDone);         // saves to this device + Firebase
        }
        if (playerNameText) playerNameText.text = AccountManager.DisplayName;
        CloseGamePopup();
    }

    // ---------------- sign up ----------------
    public void SubmitSignup()
    {
        if (busy) return;
        string name = signupName.text.Trim(), email = signupEmail.text.Trim();
        if (name.Length == 0) { signupError.text = "Please enter your name."; return; }
        if (!AccountManager.ValidEmail(email)) { signupError.text = "Please enter a valid email."; return; }

        if (Fb == null)                         // no Firebase set up yet: device only
        {
            var err = AccountManager.SignUp(name, email);
            if (err != null) { signupError.text = err; return; }
            if (inGamePopup) JoinFromGame(null); else StartGame(null);
            return;
        }

        busy = true; signupError.text = "Creating your account...";
        Fb.SignUp(name, email, err =>
        {
            busy = false;
            if (err != null) { signupError.text = err; return; }
            AccountManager.UseAccount(name, email);
            if (inGamePopup) JoinFromGame(null); else StartGame(null);
        });
    }

    // ---------------- login ----------------
    public void SubmitLogin()
    {
        if (busy) return;
        string email = loginEmail.text.Trim();
        if (!AccountManager.ValidEmail(email)) { loginError.text = "Please enter a valid email."; return; }

        if (Fb == null)
        {
            var err = AccountManager.Login(email);
            if (err != null) { loginError.text = err; return; }
            if (inGamePopup) JoinFromGame(null); else StartGame(null);
            return;
        }

        busy = true; loginError.text = "Logging in...";
        Fb.Login(email, (err, name, done) =>
        {
            busy = false;
            if (err != null) { loginError.text = err; return; }
            AccountManager.UseAccount(name, email);
            if (inGamePopup) JoinFromGame(done); else StartGame(done);
        });
    }

    public void PlayAsGuest()
    {
        AccountManager.PlayAsGuest();
        StartGame(null);
    }

    public void Play() => StartGame(null);   // already logged in

    public void LogOut()
    {
        AccountManager.LogOut();
        ShowHome();
    }

    void StartGame(List<string> onlineProgress)
    {
        startedThisSession = true;
        // quick fade from the home screen into the game
        if (ScreenFader.Instance != null && frontEndRoot.activeSelf) ScreenFader.Instance.Cover(HideFrontEnd);
        else HideFrontEnd();
        if (playerNameText) playerNameText.text = AccountManager.LoggedIn ? AccountManager.DisplayName : "Guest";
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.LoadProgress(AccountManager.LoggedIn ? AccountManager.Current.email : "guest");
            QuestManager.Instance.MergeCompleted(onlineProgress);
        }
    }

    void HideFrontEnd()
    {
        frontEndRoot.SetActive(false);
        if (gameHudRoot) gameHudRoot.SetActive(true);
        BlockPlayer(false);
        CheckSignupPrompt();       // e.g. a guest coming back with quests already done
    }

    // call this to go back to the home screen from the game (e.g. a pause menu)
    public void BackToHome()
    {
        startedThisSession = false;
        frontEndRoot.SetActive(true);
        if (gameHudRoot) gameHudRoot.SetActive(false);
        BlockPlayer(true);
        ShowHome();
    }

    // stop the map player walking around behind the home screen
    static void BlockPlayer(bool blocked)
    {
        var pm = FindFirstObjectByType<PlayerMovement>();
        if (pm != null) pm.SetMovementBlocked(blocked);
    }

    void Update()
    {
        // Enter submits whichever form is open
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        bool enter = kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame);
#else
        bool enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
        if (!enter || !frontEndRoot.activeInHierarchy) return;
        if (loginPanel.activeInHierarchy) SubmitLogin();
        else if (signupPanel.activeInHierarchy) SubmitSignup();
    }
}
