using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

// Simple UI manager: shows elapsed time and score (replaces kills) and player HP.
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("UI References")]
    public Text timerText;    // assign in inspector

    // Backwards-compatible: was previously named 'killsText' in inspector.
    [FormerlySerializedAs("killsText")]
    public Text scoreText;    // assign in inspector (legacy UnityEngine.UI.Text)

    public Text gameOverText; // assign in inspector (disabled by default)
    public Text healthText;   // assign in inspector (player HP)

    [Header("Parry UI")]
    public Image parryFillImage;      // assign the circular fill image (Image.type = Filled)
    public GameObject parryActiveCue; // optional visual for when parry window is active (flash/pulse)

    [Header("Timer")]
    public bool startOnPlay = true;

    [Header("Scoring")]
    [Tooltip("Base points awarded for a typical kill/action")]
    public int baseKillScore = 1;
    [Tooltip("Score multiplier applied when AddScore(..., useMultiplier=true)")]
    public int scoreMultiplier = 1;
    [Tooltip("Optional Text to display the current multiplier (assign in inspector)")]
    public Text multiplierText;

    private float elapsed = 0f;
    private bool running = false;

    private int score = 0;

    private Coroutine parryCoroutine;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);
    }

    void Start()
    {
        if (startOnPlay) StartTimer();

        EnsureScoreText(); // make sure scoreText exists so AddScore updates visible UI
        EnsureHealthText(); // ensure healthText exists so UpdateHealth works
        UpdateUI();

        // ensure game over text is hidden initially
        if (gameOverText != null)
            gameOverText.gameObject.SetActive(false);

        if (parryFillImage != null)
            parryFillImage.gameObject.SetActive(false);

        if (parryActiveCue != null)
            parryActiveCue.SetActive(false);
    }

    void Update()
    {
        if (!running) return;
        elapsed += Time.deltaTime;
        UpdateTimerText();
    }

    void UpdateUI()
    {
        UpdateTimerText();
        UpdateScoreText();
    }

    void UpdateTimerText()
    {
        if (timerText == null) return;
        int minutes = (int)(elapsed / 60f);
        int seconds = (int)(elapsed % 60f);
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    public void StartTimer()
    {
        running = true;
    }

    public void StopTimer()
    {
        running = false;
    }

    public void ResetTimer()
    {
        elapsed = 0f;
        UpdateTimerText();
    }

    // Replaces previous AddKill; adds score and updates UI.
    // amount: base points to add
    // useMultiplier: if true, amount is multiplied by scoreMultiplier
    public void AddScore(int amount = 1, bool useMultiplier = true)
    {
        int appliedMultiplier = Mathf.Max(1, scoreMultiplier);
        int applied = useMultiplier ? amount * appliedMultiplier : amount;
        score += applied;
        Debug.Log($"[UIManager] AddScore({amount}, useMultiplier={useMultiplier}) -> applied={applied} score={score}");
        UpdateScoreText();
    }

    // Convenience for awarding a typical kill using the configured baseKillScore
    public void AddKillScore(bool useMultiplier = true)
    {
        AddScore(baseKillScore, useMultiplier);
    }

    // Set the multiplier directly (clamps to at least 1)
    public void SetScoreMultiplier(int multiplier)
    {
        scoreMultiplier = Mathf.Max(1, multiplier);
        Debug.Log($"[UIManager] SetScoreMultiplier({scoreMultiplier})");
        UpdateScoreText();
    }

    // Multiply the current multiplier (e.g., temporary power-ups)
    public void MultiplyScore(int factor)
    {
        scoreMultiplier = Mathf.Max(1, scoreMultiplier * Mathf.Max(1, factor));
        Debug.Log($"[UIManager] MultiplyScore(factor={factor}) -> scoreMultiplier={scoreMultiplier}");
        UpdateScoreText();
    }

    public void ResetScoreMultiplier()
    {
        scoreMultiplier = 1;
        Debug.Log("[UIManager] ResetScoreMultiplier()");
        UpdateScoreText();
    }

    private void UpdateScoreText()
    {
        if (scoreText != null)
        {
            // if multiplierText is assigned, keep score display clean and update multiplier separately
            if (multiplierText != null)
            {
                scoreText.text = "Score: " + score.ToString();
                multiplierText.text = "x" + Mathf.Max(1, scoreMultiplier).ToString();
            }
            else
            {
                // append multiplier info inline when no separate multiplier text exists
                scoreText.text = "Score: " + score.ToString();
                if (scoreMultiplier > 1)
                {
                    scoreText.text += " (x" + scoreMultiplier.ToString() + ")";
                }
            }
            return;
        }

        Debug.LogWarning("[UIManager] No scoreText assigned. A runtime fallback should have been created.");
    }

    // Show the "YOU LOSE" message and stop the timer
    public void ShowLose()
    {
        if (gameOverText != null)
        {
            gameOverText.text = "YOU LOSE";
            gameOverText.gameObject.SetActive(true);
        }

        StopTimer();
    }

    // Update the on-screen HP display
    public void UpdateHealth(int current, int max)
    {
        if (healthText == null) return;
        healthText.text = string.Format("HP: {0}/{1}", Mathf.Max(0, current), Mathf.Max(1, max));
    }

    // Start the parry fill animation that fills over `fillDuration` seconds.
    // When it reaches full the player should attempt to release to parry.
    public void StartParryFill(float fillDuration)
    {
        if (parryFillImage == null) return;

        // stop existing coroutine
        if (parryCoroutine != null) StopCoroutine(parryCoroutine);
        parryCoroutine = StartCoroutine(ParryFillRoutine(fillDuration));
    }

    // Stop / hide the parry UI
    public void StopParryFill()
    {
        if (parryCoroutine != null) { StopCoroutine(parryCoroutine); parryCoroutine = null; }
        if (parryFillImage != null) parryFillImage.gameObject.SetActive(false);
        if (parryActiveCue != null) parryActiveCue.SetActive(false);
    }

    // Called when the parry window becomes active (hitbox enabled)
    public void ShowParryActive(float activeDuration)
    {
        if (parryActiveCue == null) return;
        StartCoroutine(ParryActiveRoutine(activeDuration));
    }

    private System.Collections.IEnumerator ParryFillRoutine(float duration)
    {
        parryFillImage.gameObject.SetActive(true);
        parryFillImage.fillAmount = 0f;

        float t = 0f;
        // Guard against zero duration
        if (duration <= 0f)
        {
            parryFillImage.fillAmount = 1f;
            yield break;
        }

        while (t < duration)
        {
            t += Time.deltaTime;
            parryFillImage.fillAmount = Mathf.Clamp01(t / duration);
            yield return null;
        }

        parryFillImage.fillAmount = 1f;
        // keep full until explicitly stopped or until ShowParryActive handles active cue
    }

    private System.Collections.IEnumerator ParryActiveRoutine(float activeDuration)
    {
        parryActiveCue.SetActive(true);
        yield return new WaitForSeconds(activeDuration);
        parryActiveCue.SetActive(false);
        // also hide the fill after active window ends
        if (parryFillImage != null) parryFillImage.gameObject.SetActive(false);
    }

    // Create a fallback scoreText in case it's not assigned in the Inspector
    private void EnsureScoreText()
    {
        if (scoreText != null) return;

        // find or create Canvas
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        // create a Text GameObject
        GameObject go = new GameObject("ScoreText", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(canvas.transform, false);
        Text t = go.GetComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.fontSize = 16;
        t.alignment = TextAnchor.UpperLeft;
        t.color = Color.white;  
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(10, -10);
        scoreText = t;

        Debug.Log("[UIManager] Created fallback scoreText at runtime.");
        UpdateScoreText();
    }

    // Create a fallback healthText in case it's not assigned in the Inspector
    private void EnsureHealthText()
    {
        if (healthText != null) return;

        // find or create Canvas
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        // create a Text GameObject below the score
        GameObject go = new GameObject("HealthText", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(canvas.transform, false);
        Text t = go.GetComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.fontSize = 16;
        t.alignment = TextAnchor.UpperLeft;
        t.color = Color.white;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(10, -30); // below score
        healthText = t;

        Debug.Log("[UIManager] Created fallback healthText at runtime.");
        healthText.text = "HP: 0/0";
    }
}
