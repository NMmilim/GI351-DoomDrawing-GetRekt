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

    [Header("Parry UI")]
    public Image parryFillImage;      // assign the circular fill image (Image.type = Filled)
    public GameObject parryActiveCue; // optional visual for when parry window is active (flash/pulse)

    [Header("Timer")]
    public bool startOnPlay = true;

    private float elapsed = 0f;
    private bool running = false;

    private int kills = 0;

    private Coroutine parryCoroutine;

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
        Debug.Log($"[UIManager] AddKill({amount}) -> kills={kills}");
        if (killsText != null) killsText.text = "Kills: " + kills.ToString();
        else Debug.LogWarning("[UIManager] killsText is not assigned in the Inspector.");
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
}
