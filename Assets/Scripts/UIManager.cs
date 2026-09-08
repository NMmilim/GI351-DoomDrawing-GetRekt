using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    private int score = 0;
    private int bestScore = 0;


    [Header("UI References")]
    public Text timerText;
    public Text scoreText;
    public Text gameOverText;
    public Text healthText;
    public Text comboText;

    [Header("Timer")]
    public bool startOnPlay = true;
    public bool useUnscaledTime = true;

    [Header("Heart Rate UI")]
    public Text heartRateText; // new: assign in inspector (optional)

    [Header("Audio")]
    [Tooltip("Optional sound to play when game over")]
    public AudioClip gameOverClip;
    [Tooltip("Volume for one-shot SFX (0..1)")]
    [Range(0f, 1f)]
    public float sfxVolume = 0.9f;

    // --- Color / Thresholds for contextual text tinting ----------
    [Header("Contextual Text Colors")]
    [Tooltip("Default color for UI texts")]
    public Color defaultTextColor = Color.white;

    [Header("HP Colors & Thresholds")]
    [Tooltip("Color when player HP is very low (<= hpLowThreshold)")]
    public Color hpLowColor = Color.red;
    [Tooltip("Color when player HP is mid (<= hpMidThreshold)")]
    public Color hpMidColor = Color.yellow;
    [Tooltip("Color when player HP is normal (>)")]
    public Color hpNormalColor = Color.white;
    [Tooltip("HP threshold considered 'low' (inclusive)")]
    public int hpLowThreshold = 1;
    [Tooltip("HP threshold considered 'mid' (inclusive)")]
    public int hpMidThreshold = 3;

    [Header("Heart Rate Colors & Thresholds")]
    [Tooltip("Color when BPM is considered normal")]
    public Color hrNormalColor = Color.white;
    [Tooltip("Color when BPM is fast")]
    public Color hrFastColor = Color.yellow;
    [Tooltip("Color when BPM is very high")]
    public Color hrHighColor = Color.red;
    [Tooltip("Color when BPM is zero (player dead)")]
    public Color hrZeroColor = Color.black;
    [Tooltip("BPM min for normal range (unused lower bound)")]
    public int hrNormalMin = 70;
    [Tooltip("BPM max for normal range")]
    public int hrNormalMax = 90;
    [Tooltip("BPM max for fast range (<= this is fast). Above this is high)")]
    public int hrFastMax = 130;
    // --------------------------------------------------------------

    private float elapsed = 0f;
    private bool running = false;

    private int comboCount = 0;

    [Tooltip("Maximum multiplier based on combo streak")]
    public int maxComboMultiplier = 8;
    [Tooltip("Base points per perfect press")]
    public int basePressPoints = 100;

    // HeartRate reference (optional)
    private HeartRate heartRateRef;

    // track last known health so UI can react
    private int currentHealth = 0;
    private int currentMaxHealth = 1;

    // Allow restart only after game over
    private bool isGameOver = false;

    // expose read-only for other systems (EnemyController) to check game-over state
    public bool IsGameOver => isGameOver;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);

        running = startOnPlay;
    }

    void Start()
    {
        //bestScore = PlayerPrefs.GetInt("BestScore", 0); saved progress even closed the game, but for now we want to reset every time you closed the game
        // hook heart rate UI if present
        heartRateRef = HeartRate.Instance;
        if (heartRateRef == null)
            heartRateRef = FindObjectOfType<HeartRate>();
        if (heartRateRef != null)
        {
            heartRateRef.OnHeartRateChanged += OnHeartRateChanged;
            // initialize display
            OnHeartRateChanged(heartRateRef.GetCurrentRate());
        }

        UpdateUI();

        // ensure non-changing UI texts use default color
        if (scoreText != null) scoreText.color = defaultTextColor;
        if (comboText != null) comboText.color = defaultTextColor;
        if (timerText != null) timerText.color = defaultTextColor;

        if (gameOverText != null)
            gameOverText.gameObject.SetActive(false);

        if (startOnPlay)
            StartTimer();
    }

    void OnDestroy()
    {
        if (heartRateRef != null)
            heartRateRef.OnHeartRateChanged -= OnHeartRateChanged;
    }

    void Update()
    {
        // Allow restart input to be processed when game is over even if the timer is stopped.
        if (!running && !isGameOver) return;

        float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (running) elapsed += delta;
        UpdateTimerText();

        // Process restart when game is over
        if (isGameOver)
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current.spaceKey.wasPressedThisFrame)
                    RestartGame();
            }
            else if (Input.GetKeyDown(KeyCode.Space))
            {
                RestartGame();
            }
        }
    }

    // --- Timer ---
    public void StartTimer() => running = true;
    public void StopTimer() => running = false;
    public void ResetTimer() { elapsed = 0f; UpdateTimerText(); }

    private void UpdateTimerText()
    {
        if (timerText == null) return;
        int minutes = (int)(elapsed / 60f);
        int seconds = (int)(elapsed % 60f);
        timerText.text = $"{minutes:00}:{seconds:00}";
    }

    // --- Score ---
    public void AddScore(int amount = 1)
    {
        score += amount;

        // Save the highest score reached during this run.
        if (score > bestScore)
        {
            bestScore = score;

            //PlayerPrefs.SetInt("BestScore", bestScore); this for saving progress
            //  PlayerPrefs.Save(); this for saving progress, i want it reset every time you closed the game, so i commented it out

            Debug.Log("[UIManager] NEW BEST SCORE: " + bestScore);
        }

        UpdateScoreText();
    }

 

    private void UpdateScoreText()
    {
        if (scoreText != null)
            scoreText.text = "Score: " + score.ToString();
        // Score text color does not change
    }

    // --- Combo ---
    public void AddCombo()
    {
        comboCount++;
        UpdateComboText();

        int multiplier = CalculateComboMultiplier(comboCount);
        AddScore(basePressPoints * multiplier);

        Debug.Log($"[UIManager] Combo {comboCount} -> Multiplier {multiplier}x -> +{basePressPoints * multiplier} points");
    }

    public void ResetCombo()
    {
        comboCount = 0;
        UpdateComboText();

        score = 0;
        UpdateScoreText();

        Debug.Log("[UIManager] Combo reset -> score dropped to 0");
    }

    private void UpdateComboText()
    {
        if (comboText != null)
            comboText.text = "Combo: " + comboCount.ToString();
    }

    private int CalculateComboMultiplier(int combo)
    {
        if (combo < 2) return 1;
        if (combo < 4) return 2;
        if (combo < 8) return 4;
        return maxComboMultiplier;
    }



    public void ShowLose()
    {
        if (gameOverText != null)
        {
            gameOverText.text =
                "YOU LOSE\n\n" +
                "Best Score: " + bestScore;

            gameOverText.gameObject.SetActive(true);
        }

        // Stop background music
        if (BeatHit.Instance != null && BeatHit.Instance.musicSource != null)
        {
            try
            {
                BeatHit.Instance.musicSource.Stop();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"UIManager: failed to stop BGM: {ex.Message}");
            }
        }

        // Game over SFX
        if (gameOverClip != null)
        {
            Vector3 pos = Vector3.zero;

            if (Camera.main != null)
                pos = Camera.main.transform.position;

            AudioSource.PlayClipAtPoint(gameOverClip, pos, sfxVolume);
        }

        isGameOver = true;
        StopTimer();
    }

    // Restart helper: reloads active scene
    public void RestartGame()
    {
        // guard
        if (!isGameOver) return;

        // reset flag to avoid duplicate calls
        isGameOver = false;

        // ensure timeScale is normal
        Time.timeScale = 1f;

        // Unfreeze heart rate if necessary
        if (HeartRate.Instance != null)
            HeartRate.Instance.UnfreezeAndResetToBaseline();

        // reload scene
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // Heart-rate change handler (subscribed in Start)
    private void OnHeartRateChanged(float rate)
    {
        UpdateHeartRateText(rate);
        ApplyContextualTextColor();
    }

    // --- Heart Rate UI ---
    private void UpdateHeartRateText(float rate)
    {
        if (heartRateText == null) return;
        heartRateText.text = $"HR: {Mathf.RoundToInt(rate)} BPM";
    }

    // Apply color independently to HP and Heartbeat UI based on specified thresholds
    private void ApplyContextualTextColor()
    {
        // Determine HP color
        Color hpColor = hpNormalColor;
        if (currentHealth <= hpLowThreshold) hpColor = hpLowColor;
        else if (currentHealth <= hpMidThreshold) hpColor = hpMidColor;
        else hpColor = hpNormalColor;

        // Determine HR color
        float rate = -1f;
        if (heartRateRef != null)
            rate = heartRateRef.GetCurrentRate();
        else if (heartRateText != null)
        {
            // fallback parsing (extract digits)
            string txt = heartRateText.text;
            string digits = new string(System.Array.FindAll(txt.ToCharArray(), c => char.IsDigit(c)));
            if (!string.IsNullOrEmpty(digits))
            {
                int parsed;
                if (int.TryParse(digits, out parsed)) rate = parsed;
            }
        }

        Color hrColor = hrNormalColor;
        if (rate <= 0f) hrColor = hrZeroColor;               // 0 BPM -> special color (dead)
        else if (rate >= hrFastMax) hrColor = hrHighColor;  // 130+
        else if (rate > hrNormalMax && rate <= hrFastMax) hrColor = hrFastColor; // 91-130
        else hrColor = hrNormalColor; // 70-90 and below

        // APPLY colors independently
        if (healthText != null) healthText.color = hpColor;
        if (heartRateText != null) heartRateText.color = hrColor;
    }

    // --- Health ---
    public void UpdateHealth(int current, int max)
    {
        currentHealth = current;
        currentMaxHealth = Mathf.Max(1, max);

        if (healthText != null)
            healthText.text = $"HP: {current}/{max}";

        // update HP color whenever health changes
        ApplyContextualTextColor();
    }

    private void UpdateUI()
    {
        UpdateTimerText();
        UpdateScoreText();
        UpdateComboText();
        if (heartRateRef != null)
            UpdateHeartRateText(heartRateRef.GetCurrentRate());
        ApplyContextualTextColor();
    }
}