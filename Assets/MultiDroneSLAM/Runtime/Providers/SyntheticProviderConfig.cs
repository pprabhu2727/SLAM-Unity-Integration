using UnityEngine;

/* 
 * File used to  create instances of SytheticPoseProviders so each drone can have different noise settings
 * Not really needed but can be helpful. Not needed if actually integrating SLAM cameras. 
 */
[CreateAssetMenu(fileName = "NewSyntheticProviderConfig", menuName = "MultiDroneSLAM/Synthetic Provider Config")]
public class SyntheticProviderConfig : ScriptableObject
{
    //[Header("Drift Settings (Depreciated)")]
    //[Tooltip("The amount of positional drift to accumulate per second. Simulates IMU integration error.")]
    //public Vector3 driftPerSecond = new Vector3(0.01f, 0.005f, 0.01f);

    [Header("Noise Settings")]
    [Tooltip("The maximum random offset to apply to the position each frame. Simulates visual tracking jitter.")]
    [Range(0f, 0.1f)]
    public float positionNoiseIntensity = 0.005f;

    [Tooltip("The maximum random rotational offset to apply each frame. Simulates rotational jitter.")]
    [Range(0f, 1f)]
    public float rotationNoiseIntensity = 0.1f;
}



