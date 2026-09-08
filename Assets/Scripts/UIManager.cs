using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    private int score = 0;
    private int finalScore = 0;
    private bool preserveMode = false; // true when player is on last life

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

    private float elapsed = 0f;
    private bool running = false;

    private int comboCount = 0;

    [Tooltip("Maximum multiplier based on combo streak")]
    public int maxComboMultiplier = 8;
    [Tooltip("Base points per perfect press")]
    public int basePressPoints = 100;

    // HeartRate reference (optional)
    private HeartRate heartRateRef;

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
        // hook heart rate UI if present
        heartRateRef = HeartRate.Instance;
        if (heartRateRef == null)
            heartRateRef = FindObjectOfType<HeartRate>();
        if (heartRateRef != null)
        {
            heartRateRef.OnHeartRateChanged += UpdateHeartRateText;
            // initialize display
            UpdateHeartRateText(heartRateRef.GetCurrentRate());
        }

        UpdateUI();

        if (gameOverText != null)
            gameOverText.gameObject.SetActive(false);

        if (startOnPlay)
            StartTimer();
    }

    void OnDestroy()
    {
        if (heartRateRef != null)
            heartRateRef.OnHeartRateChanged -= UpdateHeartRateText;
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
        UpdateScoreText();
    }

    private void UpdateScoreText()
    {
        if (scoreText != null)
            scoreText.text = "Score: " + score.ToString();
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

        if (!preserveMode)
        {
            score = 0;
            UpdateScoreText();
            Debug.Log("[UIManager] Combo reset -> score dropped to 0");
        }
        else
        {
            Debug.Log("[UIManager] Combo reset ignored (preserve mode active)");
        }
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

    // --- Preserve Mode ---
    public void EnablePreserveMode()
    {
        preserveMode = true;
        Debug.Log("[UIManager] Preserve mode enabled (last life)");
    }

    // snapshot current score for display after death (call BEFORE lethal hit is applied)
    public void PreserveFinalScore()
    {
        finalScore = score;
        Debug.Log("[UIManager] Final score preserved at " + finalScore);
    }

    public void ShowLose()
    {
        if (gameOverText != null)
        {
            gameOverText.text = "YOU LOSE\nFinal Score: " + finalScore.ToString();
            gameOverText.gameObject.SetActive(true);
        }

        // Stop background music (if BeatHit present)
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

        // Play game over one-shot SFX at camera position (if assigned)
        if (gameOverClip != null)
        {
            Vector3 pos = Vector3.zero;
            if (Camera.main != null) pos = Camera.main.transform.position;
            AudioSource.PlayClipAtPoint(gameOverClip, pos, sfxVolume);
        }

        // enable game-over state so Update will handle restart input
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

    // --- Heart Rate UI ---
    private void UpdateHeartRateText(float rate)
    {
        if (heartRateText == null) return;
        heartRateText.text = $"HR: {Mathf.RoundToInt(rate)} BPM";
    }

    // --- Health ---
    public void UpdateHealth(int current, int max)
    {
        if (healthText != null)
            healthText.text = $"HP: {current}/{max}";
    }

    private void UpdateUI()
    {
        UpdateTimerText();
        UpdateScoreText();
        UpdateComboText();
        if (heartRateRef != null)
            UpdateHeartRateText(heartRateRef.GetCurrentRate());
    }
}