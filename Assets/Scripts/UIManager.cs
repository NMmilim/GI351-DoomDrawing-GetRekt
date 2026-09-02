using UnityEngine;
using UnityEngine.UI;

// Simple UI manager: shows elapsed time and enemies killed counter.
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("UI References")]
    public Text timerText;    // assign in inspector
    public Text killsText;    // assign in inspector
    public Text gameOverText; // assign in inspector (disabled by default)
    public Text healthText;   // assign in inspector (player HP)

    [Header("Timer")]
    public bool startOnPlay = true;

    private float elapsed = 0f;
    private bool running = false;

    private int kills = 0;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);
    }

    void Start()
    {
        if (startOnPlay) StartTimer();
        UpdateUI();

        // ensure game over text is hidden initially
        if (gameOverText != null)
            gameOverText.gameObject.SetActive(false);
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
        if (killsText != null) killsText.text = "Kills: " + kills.ToString();
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

    public void AddKill(int amount = 1)
    {
        kills += amount;
        if (killsText != null) killsText.text = "Kills: " + kills.ToString();
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
}
