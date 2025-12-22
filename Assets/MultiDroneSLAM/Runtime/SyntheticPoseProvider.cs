using UnityEngine;

public class SyntheticPoseProvider : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private SyntheticProviderConfig config;
    [SerializeField] private Transform groundTruthTransform;
    [SerializeField] private SharedDriftManager sharedDrift;

    [Tooltip("The unique ID for this simulated drone.")]
    [SerializeField] public int droneId = 0;

    [Header("Output")]
    [SerializeField] private UDPPoseBroadcaster broadcaster;

    [Header("RealSense Compatibility")]
    [SerializeField] private float outputHz = 60f;

    private float _nextSendTime = 0f;


    //private Vector3 _accumulatedDrift;

    //private void Start()
    //{
    //    _accumulatedDrift = Vector3.zero;
    //}

    private void Update()
    {
        
        if (config == null || groundTruthTransform == null || broadcaster == null)
        {
            return;
        }

        if (Time.time < _nextSendTime)
            return;

        _nextSendTime = Time.time + (1f / outputHz);

        //_accumulatedDrift += config.driftPerSecond * Time.deltaTime;

        Vector3 positionNoise = Random.insideUnitSphere * config.positionNoiseIntensity;
        Quaternion rotationNoise = Quaternion.Slerp(Quaternion.identity, Random.rotationUniform, config.rotationNoiseIntensity * Time.deltaTime);

        //Vector3 noisyPosition = groundTruthTransform.position + _accumulatedDrift + positionNoise;
        Vector3 globalDrift = sharedDrift != null ? sharedDrift.CurrentDrift : Vector3.zero;
        Vector3 noisyPosition = groundTruthTransform.position + globalDrift + positionNoise;

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

        if (Time.frameCount % 300 == 0)
        {
            Debug.Log($"[SyntheticPoseProvider {droneId}] GT={groundTruthTransform.position.ToString("F2")} Drift={globalDrift.ToString("F2")}");
        }
    }


}