using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas))]
public class ParryUIBuilder : MonoBehaviour
{
    // Optional: anchor position and size for the parry UI (tweak in inspector)
    public Vector2 anchoredPosition = new Vector2(0, 100);
    public Vector2 sizeDelta = new Vector2(64, 64);
    public string resourceSpriteName = "ParryFill"; // put ParryFill.png in Assets/Resources/

    void Awake()
    {
        if (UIManager.Instance == null)
        {
            Debug.LogWarning("[ParryUIBuilder] UIManager.Instance not found. Make sure UIManager exists in scene.");
            return;
        }

        // If already assigned in inspector, just ensure configuration
        if (UIManager.Instance.parryFillImage != null)
        {
            ConfigureFillImage(UIManager.Instance.parryFillImage);
            return;
        }

        // Find or create Canvas
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        // Create parent for parry UI
        GameObject parryParent = new GameObject("ParryUI");
        parryParent.transform.SetParent(canvas.transform, false);
        RectTransform parentRect = parryParent.AddComponent<RectTransform>();
        parentRect.anchorMin = new Vector2(0.5f, 0f);
        parentRect.anchorMax = new Vector2(0.5f, 0f);
        parentRect.anchoredPosition = anchoredPosition;
        parentRect.sizeDelta = sizeDelta;

        // Create Image
        GameObject imgGO = new GameObject("ParryFillImage", typeof(RectTransform), typeof(Image));
        imgGO.transform.SetParent(parryParent.transform, false);
        Image img = imgGO.GetComponent<Image>();

        // Load sprite from Resources
        Sprite s = Resources.Load<Sprite>(resourceSpriteName);
        if (s == null)
        {
            Debug.LogError("[ParryUIBuilder] Sprite not found at Resources/" + resourceSpriteName + ".png");
            return;
        }

        img.sprite = s;
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Radial360;
        img.fillOrigin = 0; // tweak if you want a different start angle
        img.fillClockwise = true;
        img.fillAmount = 0f; // start empty

        // optional: disable by default
        img.gameObject.SetActive(false);

        // assign to UIManager
        UIManager.Instance.parryFillImage = img;

        // ensure parryActiveCue exists (create small indicator) if null
        if (UIManager.Instance.parryActiveCue == null)
        {
            GameObject cue = new GameObject("ParryActiveCue", typeof(RectTransform), typeof(Image));
            cue.transform.SetParent(parryParent.transform, false);
            var cueImg = cue.GetComponent<Image>();
            cueImg.color = new Color(1f, 1f, 1f, 0.5f);
            RectTransform cueRect = cue.GetComponent<RectTransform>();
            cueRect.anchorMin = new Vector2(0.5f, 0.5f);
            cueRect.anchorMax = new Vector2(0.5f, 0.5f);
            cueRect.sizeDelta = sizeDelta * 1.2f;
            cue.SetActive(false);
            UIManager.Instance.parryActiveCue = cue;
        }

        // final configuration
        ConfigureFillImage(img);
        Debug.Log("[ParryUIBuilder] Parry UI created and assigned to UIManager.parryFillImage");
    }

    private void ConfigureFillImage(Image img)
    {
        if (img == null) return;
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Radial360;
        img.fillOrigin = 0;
        img.fillClockwise = true;
        img.fillAmount = 0f;
    }

    private void Die()
    {
        // simple death: play animation and destroy
        // disable collider and script
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
        enabled = false;

        Debug.Log($"[Enemy2] Die() called on {gameObject.name}");

        // notify UI
        if (UIManager.Instance != null)
            UIManager.Instance.AddKill(1);

        Destroy(gameObject, 0.5f);
    }
}