using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem; // added for keyboard space check

// Simple UI manager: shows elapsed time and score (replaces kills) and player HP.
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }
    private int finalScore = 0;


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
    [Tooltip("When true the timer uses unscaled time (continues while game is paused via timeScale=0).")]
    public bool useUnscaledTime = true;

    [Header("Scoring")]
    [Tooltip("Base points awarded for a typical kill/action")]
    public int baseKillScore = 1;
    [Tooltip("Score multiplier applied when AddScore(..., useMultiplier=true)")]
    public int scoreMultiplier = 1;
    [Tooltip("Optional Text to display the current multiplier (assign in inspector)")]
    public Text multiplierText;

    [Header("Parry Score / Multiplier")]
    [Tooltip("Points awarded immediately for a perfect parry")]
    public int parryScoreBonus = 2;
    [Tooltip("Temporary multiplier applied to score after a perfect parry (1 = no change)")]
    public int parryScoreMultiplierOnSuccess = 2;
    [Tooltip("Duration (seconds) the parry multiplier remains active")]
    public float parryMultiplierDuration = 3f;

    [Header("Heart Rate UI")]
    [Tooltip("Optional Text to display current heart-rate (BPM)")]
    public Text heartRateText; // assign in inspector (optional)

    private float elapsed = 0f;
    private bool running = false;

    private int score = 0;

    private Coroutine parryCoroutine;

    // Keep a reference to the HeartRate instance we subscribed to so we can unsubscribe reliably.
    private HeartRate heartRateRef;

    // Parry multiplier state
    private int savedMultiplier = 1;
    private Coroutine parryMultiplierCoroutine;

    // Remember previous timescale to restore if needed
    private float previousTimeScale = 1f;

    // Whether the game is in a game-over state (only allow restart when true)
    private bool isGameOver = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);

        // Initialize running to inspector value; StartTimer will set it explicitly.
        running = startOnPlay;
        Debug.Log($"[UIManager] Awake: startOnPlay={startOnPlay} -> running={running}");
    }

    void Start()
    {
        // Make sure timerText exists before starting timer so UI updates immediately
        EnsureTimerText();
        // StartTimer kept for explicit call sites, call it to guarantee running is set
        if (startOnPlay)
            StartTimer();

        EnsureScoreText(); // make sure scoreText exists so AddScore updates visible UI
        EnsureHealthText(); // ensure healthText exists so UpdateHealth works
        EnsureHeartRateText(); // ensure heartRateText exists so HR is visible
        UpdateUI();

        // ensure game over text is hidden initially
        if (gameOverText != null)
            gameOverText.gameObject.SetActive(false);

        if (parryFillImage != null)
            parryFillImage.gameObject.SetActive(false);

        if (parryActiveCue != null)
            parryActiveCue.SetActive(false);

        // Subscribe to HeartRate updates if available.
        // Do NOT assign to HeartRate.Instance (its setter is inaccessible). Instead store the found reference.
        heartRateRef = HeartRate.Instance;
        if (heartRateRef == null)
            heartRateRef = FindObjectOfType<HeartRate>();

        if (heartRateRef != null)
        {
            heartRateRef.OnHeartRateChanged += OnHeartRateChanged;
            UpdateHeartRateText(heartRateRef.GetCurrentRate());
        }
    }

    void OnDestroy()
    {
        if (heartRateRef != null)
            heartRateRef.OnHeartRateChanged -= OnHeartRateChanged;
    }

    void Update()
    {
        // Allow the Update loop to still check for restart input when the game is over.
        // Previously Update returned early when 'running' was false which prevented restart input after StopTimer() was called.
        if (!running && !isGameOver) return;

        // Respect user's choice to count with unscaled time (useful if timeScale is set to 0)
        float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (running) // only accumulate elapsed while running
            elapsed += delta;
        UpdateTimerText();

        // Quick restart: only allow pressing Space to restart when game is over
        if (isGameOver)
        {
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                RestartGame();
            }
            else if (Keyboard.current == null && Input.GetKeyDown(KeyCode.Space))
            {
                // fallback to legacy Input if new InputSystem not present
                RestartGame();
            }
        }
    }

    void UpdateUI()
    {
        UpdateTimerText();
        UpdateScoreText();
        if (heartRateRef != null)
            UpdateHeartRateText(heartRateRef.GetCurrentRate());
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
        if (!running)
        {
            running = true;
            Debug.Log("[UIManager] StartTimer() called -> running=true");
        }
        else
        {
            Debug.Log("[UIManager] StartTimer() called but timer already running");
        }
    }           

    public void StopTimer()
    {
        if (running)
        {
            running = false;
            Debug.Log("[UIManager] StopTimer() called -> running=false");
        }
        else
        {
            Debug.Log("[UIManager] StopTimer() called but timer already stopped");
        }
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

    // Award immediate parry bonus and optionally enable a temporary multiplier
    public void OnPerfectParry()
    {
        // award immediate bonus (multiplied by current multiplier)
        AddScore(parryScoreBonus, true);

        // apply temporary multiplier if configured (>1)
        if (parryScoreMultiplierOnSuccess > 1 && parryMultiplierDuration > 0f)
        {
            TriggerParryMultiplier(parryScoreMultiplierOnSuccess, parryMultiplierDuration);
        }
    }

    // Start temporary parry multiplier (saves and restores previous multiplier)
    public void TriggerParryMultiplier(int multiplier, float duration)
    {
        if (multiplier <= 1 || duration <= 0f) return;

        // stop existing coroutine and restore before applying new one
        if (parryMultiplierCoroutine != null)
        {
            StopCoroutine(parryMultiplierCoroutine);
            // restore saved multiplier (in case it was overridden)
            SetScoreMultiplier(savedMultiplier);
            parryMultiplierCoroutine = null;
        }

        savedMultiplier = Mathf.Max(1, scoreMultiplier);
        SetScoreMultiplier(multiplier);
        parryMultiplierCoroutine = StartCoroutine(ParryMultiplierRoutine(duration));
    }

    private System.Collections.IEnumerator ParryMultiplierRoutine(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            yield return null;
        }

        // restore previous multiplier
        SetScoreMultiplier(Mathf.Max(1, savedMultiplier));
        parryMultiplierCoroutine = null;
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
            }
            else
            {
                // append multiplier info inline when no separate multiplier text exists
                scoreText.text = "Score: " + score.ToString();
                if (scoreMultiplier > 1)
                {
                }
            }
            return;
        }

        Debug.LogWarning("[UIManager] No scoreText assigned. A runtime fallback should have been created.");
    }

    // Show the "YOU LOSE" message and stop the timer
    public void PreserveFinalScore()
    {
        finalScore = score;
        Debug.Log("[UIManager] Final score preserved at " + finalScore);
    }

    public void ShowLose()
    {
        // Use preserved finalScore, not live score
        if (gameOverText != null)
        {
            gameOverText.text = "YOU LOSE\nFinal Score: " + finalScore.ToString();
            gameOverText.gameObject.SetActive(true);
        }

        StopTimer();
    }



    // Stops gameplay: pause time, stop shurikens/enemies/hitboxes and pause music.
    private void StopGameplay()
    {
        // already stopped?
        if (Mathf.Approximately(Time.timeScale, 0f)) return;

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f; // freeze physics and time-based updates

        // Pause music if BeatHit audio is present
        if (BeatHit.Instance != null && BeatHit.Instance.musicSource != null)
        {
            try { BeatHit.Instance.musicSource.Pause(); } catch { }
        }

        // Disable enemy controllers
        var enemies = FindObjectsOfType<EnemyController>();
        foreach (var e in enemies)
        {
            if (e != null) e.enabled = false;
        }

        // Disable strike hitboxes
        var strikes = FindObjectsOfType<StrikeHitbox>();
        foreach (var s in strikes)
        {
            if (s != null) s.enabled = false;
        }

        // Stop all shurikens (zero velocity + disable script)
        var shurikens = FindObjectsOfType<Shuriken>();
        foreach (var s in shurikens)
        {
            if (s != null) s.StopMotion();
        }

        Debug.Log("[UIManager] StopGameplay() called: frozen time, paused music, disabled enemies/hitboxes, stopped shurikens.");
    }

    // Restart the current scene (clear state). Call from UI button or press Space when Game Over shown.
    public void RestartGame()
    {
        if (!isGameOver)
        {
            Debug.Log("[UIManager] RestartGame() ignored because game is not over.");
            return;
        }

        // prevent double-restart
        isGameOver = false;

        // restore time scale before reload to ensure scene loads normally
        Time.timeScale = 1f;

        // Unfreeze heart rate (if it was frozen) and reset to baseline
        if (HeartRate.Instance != null)
        {
            HeartRate.Instance.UnfreezeAndResetToBaseline();
        }

        // Unpause music if needed
        if (BeatHit.Instance != null && BeatHit.Instance.musicSource != null)
        {
            try { BeatHit.Instance.musicSource.UnPause(); } catch { }
        }

        // reload active scene to get a clean state
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // Optional helper to resume (not typically used; RestartGame preferred)
    public void ResumeGameplay()
    {
        Time.timeScale = previousTimeScale == 0f ? 1f : previousTimeScale;

        if (BeatHit.Instance != null && BeatHit.Instance.musicSource != null)
        {
            try { BeatHit.Instance.musicSource.UnPause(); } catch { }
        }

        var enemies = FindObjectsOfType<EnemyController>();
        foreach (var e in enemies)
        {
            if (e != null) e.enabled = true;
        }

        var strikes = FindObjectsOfType<StrikeHitbox>();
        foreach (var s in strikes)
        {
            if (s != null) s.enabled = true;
        }

        Debug.Log("[UIManager] ResumeGameplay() called: restored timeScale and re-enabled scripts.");
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
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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

    // Create a fallback timerText in case it's not assigned in the Inspector
    private void EnsureTimerText()
    {
        if (timerText != null) return;

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        GameObject go = new GameObject("TimerText", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(canvas.transform, false);
        Text t = go.GetComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 18;
        t.alignment = TextAnchor.UpperCenter;
        t.color = Color.white;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1);
        rt.anchorMax = new Vector2(0.5f, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(0, -10);
        timerText = t;

        Debug.Log("[UIManager] Created fallback timerText at runtime.");
        UpdateTimerText();
    }

    // Create a fallback heartRateText in case it's not assigned in the Inspector
    private void EnsureHeartRateText()
    {
        if (heartRateText != null) return;

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        GameObject go = new GameObject("HeartRateText", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(canvas.transform, false);
        Text t = go.GetComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 14;
        t.alignment = TextAnchor.UpperCenter;
        t.color = Color.red;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1);
        rt.anchorMax = new Vector2(0.5f, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(0, -32); // below timer
        heartRateText = t;

        Debug.Log("[UIManager] Created fallback heartRateText at runtime.");
        UpdateHeartRateText(heartRateRef != null ? heartRateRef.GetCurrentRate() : 0f);
    }

    // Called by HeartRate when value changes
    private void OnHeartRateChanged(float newRate)
    {
        UpdateHeartRateText(newRate);
    }

    private void UpdateHeartRateText(float rate)
    {
        if (heartRateText == null) return;
        heartRateText.text = $"HR: {Mathf.RoundToInt(rate)} BPM";
    }
    [Header("Combo UI")]
    public Text comboText;   // assign in inspector
    private int comboCount = 0;

    // Combo multiplier settings
    [Tooltip("Maximum multiplier based on combo streak")]
    public int maxComboMultiplier = 8;   // cap at 8x

    [Tooltip("Base points per perfect press")]
    public int basePressPoints = 100;

    public void AddCombo()
    {
        comboCount++;
        UpdateComboText();

        // Calculate multiplier based on streak
        int multiplier = CalculateComboMultiplier(comboCount);

        // Award score using base points × multiplier
        AddScore(basePressPoints * multiplier, false);

        Debug.Log($"[UIManager] Combo {comboCount} -> Multiplier {multiplier}x -> +{basePressPoints * multiplier} points");
    }

    public void ResetCombo()
    {
        comboCount = 0;
        UpdateComboText();

        // Reset live score only
        score = 0;
        UpdateScoreText();

        Debug.Log("[UIManager] Combo reset -> score dropped to 0 (finalScore preserved)");
    }





    private void UpdateComboText()
    {
        if (comboText != null)
            comboText.text = "Combo: " + comboCount.ToString();
    }

    private int CalculateComboMultiplier(int combo)
    {
        // Simple progression: 1x at start, then 2x, 4x, 8x...
        if (combo < 2) return 1;
        if (combo < 4) return 2;
        if (combo < 8) return 4;
        return maxComboMultiplier; // cap at max
    }

   




}
