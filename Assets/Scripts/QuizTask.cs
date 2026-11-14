using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class QuizTask : MonoBehaviour
{
    [Header("UI References")]
    public GameObject quizWindow;
    public TextMeshProUGUI questionText;
    public Button[] answerButtons;
    public Image statusIcon; // The cloud icon

    [Header("Feedback Visuals")]
    public Sprite neutralSprite;
    public Sprite correctSprite;
    public Sprite wrongSprite;
    public Color normalButtonColor = Color.white;
    public Color correctButtonColor = new Color(0.1f, 0.8f, 0.4f); // Green
    public Color wrongButtonColor = new Color(0.9f, 0.2f, 0.3f); // Red

    [Header("Next Task Link")]
    public GameObject fileTransferWindow;

    [Header("Settings")]
    public bool openOnStart = true;
    [Tooltip("How long the correct-answer feedback stays up when the question has an explanation to read.")]
    public float explanationReadTime = 3.5f;
    [Tooltip("Play the pixel wipe when moving from the quiz to the file transfer.")]
    public bool useWipeTransition = true;

    [System.Serializable]
    public struct Question
    {
        [TextArea(2, 3)]
        public string questionPrompt;
        public string[] options;
        public int correctIndex;
        [Tooltip("Optional. One short sentence shown after a correct answer explaining why it is right.")]
        [TextArea(2, 3)]
        public string explanation;
    }

    [Header("Quiz Data")]
    public Question[] questions;
    private int currentQuestionIndex = -1;
    private bool isAnswered = false;

    void Start()
    {
        if (fileTransferWindow != null) fileTransferWindow.SetActive(false);
        if (quizWindow != null) quizWindow.SetActive(false);

        for (int i = 0; i < answerButtons.Length; i++)
        {
            int index = i;
            answerButtons[i].onClick.AddListener(() => OnAnswerSelected(index));
        }

        if (openOnStart)
        {
            OpenQuiz();
        }
    }

    public void OpenQuiz()
    {
        if (questions.Length == 0) return;

        // Pick a random question, but never the same one twice in a row
        if (questions.Length == 1)
        {
            currentQuestionIndex = 0;
        }
        else
        {
            int nextIndex;
            do
            {
                nextIndex = Random.Range(0, questions.Length);
            } while (nextIndex == currentQuestionIndex);
            currentQuestionIndex = nextIndex;
        }
        isAnswered = false;

        DisplayCurrentQuestion();

        if (quizWindow != null) quizWindow.SetActive(true);
    }

    void DisplayCurrentQuestion()
    {
        Question q = questions[currentQuestionIndex];
        if (questionText != null) questionText.text = q.questionPrompt;

        // Reset the status icon to neutral
        if (statusIcon != null && neutralSprite != null)
        {
            statusIcon.sprite = neutralSprite;
        }

        // Populate options and reset button colors
        for (int i = 0; i < answerButtons.Length; i++)
        {
            if (i < q.options.Length)
            {
                answerButtons[i].gameObject.SetActive(true);

                // Reset color
                Image btnImage = answerButtons[i].GetComponent<Image>();
                if (btnImage != null) btnImage.color = normalButtonColor;

                // Set text
                TextMeshProUGUI btnText = answerButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null) btnText.text = q.options[i];
            }
            else
            {
                answerButtons[i].gameObject.SetActive(false);
            }
        }
    }

    void OnAnswerSelected(int selectedIndex)
    {
        if (isAnswered) return;
        isAnswered = true;

        Question q = questions[currentQuestionIndex];

        if (selectedIndex == q.correctIndex)
        {
            StartCoroutine(HandleCorrectAnswer(selectedIndex));
        }
        else
        {
            StartCoroutine(HandleWrongAnswer(selectedIndex));
        }
    }

    IEnumerator HandleCorrectAnswer(int btnIndex)
    {
        // 1. Change question text to feedback text (plus the explanation, if one was written)
        string explanation = questions[currentQuestionIndex].explanation;
        bool hasExplanation = !string.IsNullOrWhiteSpace(explanation);
        if (questionText != null)
        {
            questionText.text = hasExplanation
                ? "Access Granted! " + explanation
                : "Access Granted, Unlocking Transfer";
        }

        // 2. Change icon to happy/verified state
        if (statusIcon != null && correctSprite != null) statusIcon.sprite = correctSprite;

        // 3. Change button color and text
        Image btnImage = answerButtons[btnIndex].GetComponent<Image>();
        if (btnImage != null) btnImage.color = correctButtonColor;

        TextMeshProUGUI btnText = answerButtons[btnIndex].GetComponentInChildren<TextMeshProUGUI>();
        if (btnText != null) btnText.text = "Correct";

        // Wait briefly before transitioning (longer if there is an explanation to read)
        yield return new WaitForSeconds(hasExplanation ? explanationReadTime : 1.2f);
        TransitionToFileTransfer();
    }

    IEnumerator HandleWrongAnswer(int btnIndex)
    {
        // 1. Change question text to feedback text
        if (questionText != null) questionText.text = "Access Denied, Try Again";

        // 2. Change icon to error/sleepy state
        if (statusIcon != null && wrongSprite != null) statusIcon.sprite = wrongSprite;

        // 3. Change button color and text
        Image btnImage = answerButtons[btnIndex].GetComponent<Image>();
        if (btnImage != null) btnImage.color = wrongButtonColor;

        TextMeshProUGUI btnText = answerButtons[btnIndex].GetComponentInChildren<TextMeshProUGUI>();
        if (btnText != null) btnText.text = "X Wrong";

        // Wait 3 seconds as requested
        yield return new WaitForSeconds(3.0f);

        // Load a brand new question
        OpenQuiz();
    }

    void TransitionToFileTransfer()
    {
        if (useWipeTransition)
        {
            // The wipe covers the screen, swaps the windows while hidden, then uncovers.
            PixelWipe.Play(this, SwapToFileTransfer);
        }
        else
        {
            SwapToFileTransfer();
        }
    }

    void SwapToFileTransfer()
    {
        if (quizWindow != null) quizWindow.SetActive(false);

        if (fileTransferWindow != null)
        {
            fileTransferWindow.SetActive(true);
        }
    }
}