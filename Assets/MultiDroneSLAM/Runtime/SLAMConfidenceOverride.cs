using UnityEngine;

public class SLAMConfidenceOverride : MonoBehaviour
{
    [Tooltip("Enable artificial confidence degradation")]
    public bool enabledOverride = false;

    [Range(0, 2)]
    public int forcedConfidence = 1;
}
