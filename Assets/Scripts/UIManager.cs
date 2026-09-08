using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

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

    private float elapsed = 0f;
    private bool running = false;

    private int comboCount = 0;

    [Tooltip("Maximum multiplier based on combo streak")]
    public int maxComboMultiplier = 8;
    [Tooltip("Base points per perfect press")]
    public int basePressPoints = 100;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);

        running = startOnPlay;
    }

    void Start()
    {
        UpdateUI();

        if (gameOverText != null)
            gameOverText.gameObject.SetActive(false);

        if (startOnPlay)
            StartTimer();
    }

    void Update()
    {
        if (!running) return;

        float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        elapsed += delta;
        UpdateTimerText();
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

        StopTimer();
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
    }
}
