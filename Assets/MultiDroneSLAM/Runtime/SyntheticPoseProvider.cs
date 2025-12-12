using UnityEngine;

public class SyntheticPoseProvider : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private SyntheticProviderConfig config;
    [SerializeField] private Transform groundTruthTransform;

    [Tooltip("The unique ID for this simulated drone.")]
    [SerializeField] public int droneId = 0;

    [Header("Output")]
    [SerializeField] private UDPPoseBroadcaster broadcaster;

    private Vector3 _accumulatedDrift;

    private void Start()
    {
        _accumulatedDrift = Vector3.zero;
    }

    private void Update()
    {
        if (config == null || groundTruthTransform == null || broadcaster == null)
        {
            return;
        }

        _accumulatedDrift += config.driftPerSecond * Time.deltaTime;

        Vector3 positionNoise = Random.insideUnitSphere * config.positionNoiseIntensity;
        Quaternion rotationNoise = Quaternion.Slerp(Quaternion.identity, Random.rotationUniform, config.rotationNoiseIntensity * Time.deltaTime);

        Vector3 noisyPosition = groundTruthTransform.position + _accumulatedDrift + positionNoise;
        Quaternion noisyRotation = groundTruthTransform.rotation * rotationNoise;

        PoseData pose = new PoseData
        {
            DroneId = this.droneId,
            Timestamp = Time.timeAsDouble,
            Position = noisyPosition,
            Rotation = noisyRotation,
            TrackingConfidence = 2
        };

        broadcaster?.BroadcastPose(pose);
    }
}