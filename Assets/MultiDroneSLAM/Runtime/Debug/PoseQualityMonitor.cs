using UnityEngine;
using System.Collections.Generic;

public class PoseQualityMonitor : MonoBehaviour
{
    private class DroneStats
    {
        public int droneId;
        public int packetsInWindow;
        public float windowStartTime;
        public float lastPacketTime;
        public float emaDelta;
        public float emaJitter;
        public bool initialized;
    }

    [Header("Settings")]
    [Tooltip("Seconds per rate window.")]
    [SerializeField] private float rateWindowSeconds = 1.0f;

    [Tooltip("EMA smoothing factor")]
    [Range(0.01f, 0.5f)]
    [SerializeField] private float emaAlpha = 0.10f;

    private readonly Dictionary<int, DroneStats> _stats = new Dictionary<int, DroneStats>();

    public void NotePacket(int droneId)
    {
        if (!_stats.TryGetValue(droneId, out var s))
        {
            s = new DroneStats
            {
                droneId = droneId,
                windowStartTime = Time.time,
                lastPacketTime = Time.time
            };
            _stats[droneId] = s;
        }

        float now = Time.time;
        float dt = now - s.lastPacketTime;

        if (s.initialized)
        {
            // EMA dt
            s.emaDelta = Mathf.Lerp(s.emaDelta, dt, emaAlpha);
            // EMA jitter
            float jitterSample = Mathf.Abs(dt - s.emaDelta);
            s.emaJitter = Mathf.Lerp(s.emaJitter, jitterSample, emaAlpha);
        }
        else
        {
            s.emaDelta = dt;
            s.emaJitter = 0f;
            s.initialized = true;
        }

        s.lastPacketTime = now;
        s.packetsInWindow++;

        // reset rate window
        if (now - s.windowStartTime >= rateWindowSeconds)
        {
            s.packetsInWindow = 0;
            s.windowStartTime = now;
        }
    }

    public bool TryGetStats(int droneId, out float packetsPerSecond, out float secondsSinceLast, out float emaDt, out float emaJitter)
    {
        packetsPerSecond = 0; secondsSinceLast = 0; emaDt = 0; emaJitter = 0;

        if (!_stats.TryGetValue(droneId, out var s)) return false;

        float now = Time.time;
        float windowAge = Mathf.Max(0.0001f, now - s.windowStartTime);
        packetsPerSecond = s.packetsInWindow / windowAge;
        secondsSinceLast = now - s.lastPacketTime;
        emaDt = s.emaDelta;
        emaJitter = s.emaJitter;
        return true;
    }
}
