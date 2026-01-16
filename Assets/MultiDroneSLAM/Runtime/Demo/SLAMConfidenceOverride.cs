using UnityEngine;
/*
 * This class allows a user to manually degrade the performance of SLAM
 * Used for Demo purposes only. Not needed for functionality.
 */
public class SLAMConfidenceOverride : MonoBehaviour
{
    [Header("Enable")]
    [Tooltip("Enable artificial SLAM degradation")]
    public bool enabledOverride = false;

    [Header("Packet Loss")]
    [Range(0f, 1f)]
    [Tooltip("Probability [0-1] that a pose packet is dropped")]
    public float dropProbability = 0.1f;

    [Header("Rate Throttling")]
    [Tooltip("Max output rate when degraded (Hz)")]
    public float degradedOutputHz = 15f;

    [Header("Confidence Floor")]
    [Range(0, 2)]
    [Tooltip("Maximum confidence allowed while degraded")]
    public int maxConfidenceWhileDegraded = 1;


    public SLAMConfidenceLevel forcedLevel = SLAMConfidenceLevel.Degraded;
    public int GetForcedConfidence()
    {
        return forcedLevel switch
        {
            SLAMConfidenceLevel.Good => 2,
            SLAMConfidenceLevel.Degraded => 1,
            SLAMConfidenceLevel.Poor => 0,
            SLAMConfidenceLevel.Lost => 0,
            _ => 1
        };
    }

}
