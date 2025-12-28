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
            richText = true,
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

        GUILayout.BeginArea(new Rect(10, 10, 520, Screen.height));
        GUILayout.Label(_cachedText, _style);
        GUILayout.EndArea();
    }

    private string Section(string title)
    {
        return $"<b>=== {title} ===</b>\n";
    }


    private string BuildHUDText()
    {
        var sb = new StringBuilder(512);

        sb.AppendLine(Section("SLAM SYSTEM STATUS"));

        if (slamManager == null)
        {
            sb.AppendLine("SLAM Manager: NOT SET");
            return sb.ToString();
        }

        float drift = slamManager.Debug_GetAnchorDriftMeters();
        bool relocalizing = slamManager.Debug_IsRelocalizing();

        string driftColor = drift < 0.10f ? "green" : drift < 0.30f ? "yellow" : "red";

        sb.AppendLine(Colorize(
            $"Anchor Drift     : {drift:F3} m",
            driftColor
        ));

        sb.AppendLine(Colorize(
            $"Relocalizing     : {(relocalizing ? "YES" : "NO")}",
            relocalizing ? "yellow" : "green"
        ));

        sb.AppendLine($"Current Anchor   : {slamManager.Debug_GetAnchorId()}");
        sb.AppendLine();

        if (qualityMonitor == null)
        {
            sb.AppendLine("PoseQualityMonitor: NOT SET");
            return sb.ToString();
        }

        sb.AppendLine(Section("SLAM QUALITY (PER DRONE)"));
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
                string healthColor = ColorForHealth(stale, emaJitter, sinceLast);
                string confColor = hasConfidence ? ColorForConfidence(confidence) : "white";

                sb.AppendLine(Colorize(
                    $"Drone {id} | pps {pps,6:F1} | since {sinceLast,5:F2}s | " +
                    $"dt {emaDt,5:F3}s | jitter {emaJitter,5:F3}s | " +
                    $"{(stale ? "STALE" : "OK")}",
                    healthColor
                ));

                sb.AppendLine(
                    $"speed {(hasSpeed ? speed.ToString("F2") : "--"),5} m/s | " +
                    Colorize(
                        $"conf {(hasConfidence ? ConfidenceToString(confidence) : "--"),-7}",
                        confColor
                    )
                );

            }
            else
            {
                sb.AppendLine($"Drone {id} | No data");
            }

            sb.AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine(Section("MOTION & COLLISION"));
        bool collisionActive = slamManager.Debug_IsCollisionActive();


        float ttc = slamManager.Debug_GetCollisionTTC();

        string riskColor = ttc < 0.5f ? "red" : ttc < 1.5f ? "yellow" : "orange";
    
        if (!collisionActive)
        {
            sb.AppendLine(Colorize("No predicted collisions", "green"));
        }
        else
        {
            sb.AppendLine(Colorize($"Predicted collision in {ttc:F2} s", riskColor));
        }

        sb.AppendLine(Colorize(
            "Soft avoidance active",
            "yellow"
        ));
   



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

    private static string Colorize(string text, string color)
    {
        return $"<color={color}>{text}</color>";
    }

    private static string ColorForHealth(bool stale, float jitter, float sinceLast)
    {
        if (stale || sinceLast > 1.0f)
            return "red";

        if (jitter > 0.02f || sinceLast > 0.25f)
            return "yellow";

        return "green";
    }

    private static string ColorForConfidence(int conf)
    {
        return conf switch
        {
            >= 2 => "green",
            1 => "yellow",
            0 => "red",
            _ => "white"
        };
    }


}
