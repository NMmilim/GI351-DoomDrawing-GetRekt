using UnityEngine;

[RequireComponent(typeof(Collider2D), typeof(Rigidbody2D))]
public class Shuriken : MonoBehaviour
{
    public GameObject owner;
    public int damage = 1;
    public float speed = 6f;
    public float hitRange = 0.3f; // how close to player before it counts as a hit

    // When true the shuriken will scale its speed by the HeartRate multiplier at launch.
    // If you prefer controlling speed centrally in EnemyController, set this false and adjust there instead.
    public bool followHeartRate = true;

    // Rotation animation: degrees per second
    [Tooltip("Rotation speed in degrees per second (positive = clockwise)")]
    public float rotationSpeed = 720f;

    // Audio (optional)
    [Header("Audio (optional)")]
    [Tooltip("Play once when the shuriken is launched")]
    public AudioClip throwClip;
    [Tooltip("Loop while shuriken is flying (optional spin sound)")]
    public AudioClip spinClip;
    [Tooltip("Play when shuriken hits player or environment")]
    public AudioClip hitClip;
    [Tooltip("Volume for attached audio (0..1)")]
    [Range(0f, 1f)]
    public float audioVolume = 0.7f;

    private Rigidbody2D rb;
    private Transform player;
    private bool used = false; // prevent double-hit processing

    // runtime AudioSource for spin (if used)
    private AudioSource spinSource;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();               
        rb.gravityScale = 0f;
        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;

        // find player once
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;
    }

    public void Launch(Vector2 dir)
    {
        // Apply heart-rate multiplier at launch if enabled.
        float finalSpeed = speed;
        if (followHeartRate && HeartRate.Instance != null)
        {
            finalSpeed = speed * HeartRate.Instance.GetAttackSpeedMultiplier();
        }

        // use Rigidbody2D.velocity to set linear velocity
        if (rb != null)
            rb.linearVelocity = dir.normalized * finalSpeed;

        // optional: align initial rotation to travel direction
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        // throw one-shot sound (2D)
        if (throwClip != null)
        {
            AudioSource.PlayClipAtPoint(throwClip, transform.position, audioVolume);
        }

        // spin loop: create a local AudioSource so it follows the shuriken
        if (spinClip != null)
        {
            if (spinSource == null)
            {
                spinSource = gameObject.AddComponent<AudioSource>();
                spinSource.clip = spinClip;
                spinSource.loop = true;
                spinSource.playOnAwake = false;
                spinSource.spatialBlend = 1f; // 3D
                spinSource.volume = audioVolume;
            }
            spinSource.Play();
        }
    }

    // Stop movement and disable further processing. Called on game over.
    public void StopMotion()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        // stop spin audio if present
        if (spinSource != null && spinSource.isPlaying)
        {
            spinSource.Stop();
        }

        // prevent Update/OnTrigger from processing further
        enabled = false;
    }

    private void OnDestroy()
    {
        if (spinSource != null)
        {
            if (spinSource.isPlaying) spinSource.Stop();
            Destroy(spinSource);
            spinSource = null;
        }
    }

    private void Update()
    {
        // simple visual rotation every frame
        if (Mathf.Abs(rotationSpeed) > 0f)
        {
            transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
        }

        if (used) return;
        if (player == null) return;

        // check distance to player
        float dist = Vector2.Distance(transform.position, player.position);
        if (dist <= hitRange)
        {
            used = true;

            var pc = player.GetComponent<PlayerController>();
            if (pc != null)
            {
                EnemyController attacker = owner != null ? owner.GetComponent<EnemyController>() : null;

                bool wasParried;
                bool handled = pc.OnIncomingAttack(damage, attacker, out wasParried);

                if (!handled)
                {
                    // hit player: play hit and optionally player hurt handled in PlayerController
                    if (hitClip != null)
                        AudioSource.PlayClipAtPoint(hitClip, transform.position, audioVolume);

                    pc.TakeDamage(damage);
                }
                else if (wasParried)
                {
                    Debug.Log("Shuriken parried!");

                    // Award score for successful parry
                    UIManager.Instance?.AddScore(1);
                    Debug.Log("[Shuriken] Called UIManager.AddScore(1)");

                    // play parry-related sounds are handled by PlayerController (parry sound, block sound)
                    // stop spin audio just in case
                    if (spinSource != null && spinSource.isPlaying) spinSource.Stop();

                    Destroy(gameObject);
                    return;
                }
            }

            // always destroy once it reaches player
            // stop spin audio to avoid stray loops
            if (spinSource != null && spinSource.isPlaying) spinSource.Stop();
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (used) return;

        // still destroy if it hits environment
        if (other != null && !other.CompareTag("Player"))
        {
            if (owner != null && other.gameObject == owner) return;

            // environment hit: play hit clip
            if (hitClip != null)
                AudioSource.PlayClipAtPoint(hitClip, transform.position, audioVolume);

            if (spinSource != null && spinSource.isPlaying) spinSource.Stop();
            Destroy(gameObject);
        }
    }
}
