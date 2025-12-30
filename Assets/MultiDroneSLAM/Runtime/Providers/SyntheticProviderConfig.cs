using UnityEngine;

// The [CreateAssetMenu] attribute allows us to easily create instances of this class
// in the Unity Editor's asset creation menu.
[CreateAssetMenu(fileName = "NewSyntheticProviderConfig", menuName = "MultiDroneSLAM/Synthetic Provider Config")]
public class SyntheticProviderConfig : ScriptableObject
{
    [Header("Drift Settings (Depreciated)")]
    [Tooltip("The amount of positional drift to accumulate per second. Simulates IMU integration error.")]
    public Vector3 driftPerSecond = new Vector3(0.01f, 0.005f, 0.01f);

    [Header("Noise Settings")]
    [Tooltip("The maximum random offset to apply to the position each frame. Simulates visual tracking jitter.")]
    [Range(0f, 0.1f)]
    public float positionNoiseIntensity = 0.005f;

    [Tooltip("The maximum random rotational offset to apply each frame. Simulates rotational jitter.")]
    [Range(0f, 1f)]
    public float rotationNoiseIntensity = 0.1f;
}