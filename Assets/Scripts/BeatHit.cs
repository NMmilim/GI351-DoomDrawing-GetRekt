using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple BeatManager using AudioSettings.dspTime and BPM.
/// Other systems can subscribe to OnBeat or schedule actions at specific DSP times.
/// Drop this on a GameObject, assign a music AudioSource and call Play().
/// </summary>
public class BeatHit : MonoBehaviour
{
    public static BeatHit Instance { get; private set; }

    [Header("Audio")]
    public AudioSource musicSource; // assign your music AudioSource in inspector
    public float bpm = 120f;
    public double startDelay = 0.1; // seconds to wait before scheduled play
    public bool playOnStart = false; // auto play when the scene starts
    public bool debugBeats = false; // log beats to console for debugging

    // (dspTime, beatIndex)
    public event Action<double, int> OnBeat;

    double secondsPerBeat = 0.5;
    double nextBeatDsp = 0.0;
    int beatIndex = 0;

    bool isPlaying = false;

    class ScheduledItem { public double time; public Action action; }
    readonly List<ScheduledItem> scheduled = new List<ScheduledItem>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);
    }

    /// <summary>
    /// Start music playback and begin beat events.
    /// </summary>
    public void Play()
    {
        if (musicSource == null)
        {
            Debug.LogWarning("BeatHit: musicSource not assigned");
            return;
        }

        secondsPerBeat = 60.0 / Math.Max(0.0001f, bpm);
        double dspStart = AudioSettings.dspTime + startDelay;
        musicSource.PlayScheduled(dspStart);
        nextBeatDsp = dspStart + secondsPerBeat;
        beatIndex = 0;
        isPlaying = true;
    }

    void Start()
    {
        if (playOnStart)
            Play();
    }

    /// <summary>
    /// Stop playback and clear scheduled actions.
    /// </summary>
    public void Stop()
    {
        if (musicSource != null) musicSource.Stop();
        isPlaying = false;
        scheduled.Clear();
    }

    void Update()
    {
        double dsp = AudioSettings.dspTime;

        if (isPlaying)
        {
            // fire beat events (may catch up multiple beats if Update lagged)
            while (dsp >= nextBeatDsp)
            {
                try { OnBeat?.Invoke(nextBeatDsp, beatIndex); } catch (Exception ex) { Debug.LogException(ex); }
                if (debugBeats) Debug.Log($"Beat {beatIndex} @ {nextBeatDsp}");
                beatIndex++;
                nextBeatDsp += secondsPerBeat;
            }
        }

        // run scheduled actions whose time has passed
        if (scheduled.Count > 0)
        {
            for (int i = scheduled.Count - 1; i >= 0; --i)
            {
                var it = scheduled[i];
                if (dsp >= it.time)
                {
                    try { it.action?.Invoke(); } catch (Exception ex) { Debug.LogException(ex); }
                    scheduled.RemoveAt(i);
                }
            }
        }
    }

    /// <summary>
    /// Schedule an action to run at a specific DSP time.
    /// </summary>
    public void ScheduleAction(double dspTime, Action action)
    {
        if (action == null) return;
        scheduled.Add(new ScheduledItem { time = dspTime, action = action });
    }

    /// <summary>
    /// Convenience: schedule action after N beats from the next beat.
    /// </summary>
    public void ScheduleAfterBeats(int beatsAhead, Action action)
    {
        if (beatsAhead < 0) beatsAhead = 0;
        double target = nextBeatDsp + beatsAhead * secondsPerBeat;
        ScheduleAction(target, action);
    }
}
