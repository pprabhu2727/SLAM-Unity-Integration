using UnityEngine;
using System.Text;

public class SLAMDebugHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SLAMSystemManager slamManager;
    [SerializeField] private PoseQualityMonitor qualityMonitor;

    [Header("HUD Settings")]
    [SerializeField] private bool showHUD = true;
    [SerializeField] private int fontSize = 14;
    [SerializeField] private float refreshHz = 10f;

    private float _nextRefreshTime = 0f;
    private string _cachedText = "";

    private GUIStyle _style;

    private void Awake()
    {
        _style = new GUIStyle
        {
            fontSize = fontSize,
            normal = { textColor = Color.red }
        };
    }

    private void Update()
    {
        if (!showHUD) return;

        if (Time.time >= _nextRefreshTime)
        {
            _nextRefreshTime = Time.time + (1f / Mathf.Max(1f, refreshHz));
            _cachedText = BuildHUDText();
        }
    }

    private void OnGUI()
    {
        if (!showHUD) return;
        if (_style == null) return;

        GUILayout.BeginArea(new Rect(10, 10, 500, Screen.height));
        GUILayout.Label(_cachedText, _style);
        GUILayout.EndArea();
    }

    private string BuildHUDText()
    {
        var sb = new StringBuilder(512);

        sb.AppendLine("=== Multi-Drone SLAM Debug HUD ===");

        if (slamManager == null)
        {
            sb.AppendLine("SLAM Manager: NOT SET");
            return sb.ToString();
        }

        float drift = slamManager.Debug_GetAnchorDriftMeters();
        bool relocalizing = slamManager.Debug_IsRelocalizing();

        sb.AppendLine($"Anchor Drift: {drift:F3} m");
        sb.AppendLine($"Relocalizing: {(relocalizing ? "YES" : "NO")}");
        sb.AppendLine($"Current Anchor: {slamManager.Debug_GetAnchorId()}");
        sb.AppendLine("");

        if (qualityMonitor == null)
        {
            sb.AppendLine("PoseQualityMonitor: NOT SET");
            return sb.ToString();
        }

        foreach (var info in slamManager.Debug_GetDroneIds())
        {
            int id = info;

            float speed = 0f;
            bool hasSpeed = slamManager.Debug_TryGetDroneSpeed(id, out speed);

            int confidence = -1;
            bool hasConfidence = slamManager.Debug_TryGetTrackingConfidence(id, out confidence);


            if (qualityMonitor.TryGetStats(id, out float pps, out float sinceLast, out float emaDt, out float emaJitter))
            {
                bool stale = sinceLast > slamManager.Debug_GetStaleThreshold();
                string confStr = hasConfidence ? ConfidenceToString(confidence) : "--";

                sb.AppendLine(
                    $"Drone {id} | pps {pps,6:F1} | since {sinceLast,5:F2}s | " +
                    $"dt {emaDt,5:F3}s | jitter {emaJitter,5:F3}s | " +
                    $"{(stale ? "STALE" : "OK")}"
                );
                sb.AppendLine(
                    $"speed {(hasSpeed ? speed.ToString("F2") : "--"),5} m/s | " +
                    $"conf {(hasConfidence ? ConfidenceToString(confidence) : "--"),-7}"
                    
                );
            }
            else
            {
                sb.AppendLine($"Drone {id} | No data");
            }
        }

        return sb.ToString();
    }

    private static string ConfidenceToString(int conf)
    {
        return conf switch
        {
            >= 2 => "GOOD",
            1 => "LIMITED",
            0 => "LOST",
            _ => "--"
        };
    }

}
