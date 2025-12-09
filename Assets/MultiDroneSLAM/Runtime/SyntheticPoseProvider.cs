using UnityEngine;

// This class implements our IPoseProvider interface, fulfilling the contract we defined.
public class SyntheticPoseProvider : MonoBehaviour, IPoseProvider
{
    // This event is the core of the IPoseProvider interface. Other scripts will subscribe to it.
    public event System.Action<PoseData> OnPoseReceived;

    [Header("Configuration")]
    [Tooltip("The configuration asset that defines drift and noise behavior.")]
    [SerializeField] private SyntheticProviderConfig config;

    [Tooltip("The transform representing the perfect 'ground truth' position of the drone.")]
    [SerializeField] private Transform groundTruthTransform;

    [Tooltip("The unique ID for this simulated drone.")]
    [SerializeField] private int droneId = 0;

    // A private variable to keep track of the accumulated drift over time.
    private Vector3 _accumulatedDrift;

    // This method is from the IPoseProvider interface.
    public void StartProvider()
    {
        // When the provider starts, we reset the drift to zero.
        _accumulatedDrift = Vector3.zero;
        Debug.Log($"SyntheticPoseProvider for Drone ID {droneId} started.");
    }

    // This method is also from the IPoseProvider interface.
    public void StopProvider()
    {
        Debug.Log($"SyntheticPoseProvider for Drone ID {droneId} stopped.");
        // Nothing else to clean up for this specific provider.
    }

    // A standard Unity lifecycle method.
    private void Start()
    {
        // Automatically start the provider when the game begins.
        StartProvider();
    }

    // A standard Unity lifecycle method.
    private void OnDestroy()
    {
        // Ensure we call stop when the object is destroyed.
        StopProvider();
    }

    // The core logic happens here, once every frame.
    private void Update()
    {
        // --- Safety Checks ---
        // If either the config or the transform is not assigned, do nothing to prevent errors.
        if (config == null || groundTruthTransform == null)
        {
            return;
        }

        // --- 1. Simulate Drift ---
        // Drift is a constant error that accumulates over time.
        _accumulatedDrift += config.driftPerSecond * Time.deltaTime;

        // --- 2. Simulate Noise ---
        // Noise is a random, non-accumulating jitter that happens each frame.
        Vector3 positionNoise = Random.insideUnitSphere * config.positionNoiseIntensity;
        Quaternion rotationNoise = Quaternion.Slerp(Quaternion.identity, Random.rotationUniform, config.rotationNoiseIntensity * Time.deltaTime);

        // --- 3. Combine to create the final noisy pose ---
        Vector3 noisyPosition = groundTruthTransform.position + _accumulatedDrift + positionNoise;
        Quaternion noisyRotation = groundTruthTransform.rotation * rotationNoise;

        // --- 4. Package the data into our struct ---
        PoseData pose = new PoseData
        {
            DroneId = this.droneId,
            Timestamp = Time.timeAsDouble, // Use Unity's high-precision time
            Position = noisyPosition,
            Rotation = noisyRotation,
            TrackingConfidence = 2 // We'll just hard-code "High Confidence" for now
        };

        // --- 5. Fire the event ---
        // The '?.' is a null-conditional operator. It safely checks if anyone is listening
        // to the event before trying to call it. This prevents errors.
        OnPoseReceived?.Invoke(pose);
    }
}