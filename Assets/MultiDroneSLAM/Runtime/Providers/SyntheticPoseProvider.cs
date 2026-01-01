using UnityEngine;

/* 
 * -----------------------------------------------------------------------------
 * SIMULATION-ONLY CODE
 * This section is not requried for real slam integration. 
 * -----------------------------------------------------------------------------
 */
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

    private float _nextAllowedSendTime = 0f;

    private SLAMConfidenceOverride _confidenceOverride;


    //private Vector3 _accumulatedDrift;

    //private void Start()
    //{
    //    _accumulatedDrift = Vector3.zero;
    //}

    private void Awake()
    {
        _confidenceOverride = GetComponent<SLAMConfidenceOverride>();
    }

    private void Update()
    {
        
        if (config == null || groundTruthTransform == null || broadcaster == null)
        {
            return;
        }

        float effectiveHz = outputHz;

        if (_confidenceOverride != null && _confidenceOverride.enabledOverride)
        {
            effectiveHz = Mathf.Min(outputHz, _confidenceOverride.degradedOutputHz);
        }

        if (Time.time < _nextAllowedSendTime)
            return;

        _nextAllowedSendTime = Time.time + (1f / Mathf.Max(1f, effectiveHz));


        //_accumulatedDrift += config.driftPerSecond * Time.deltaTime;

        Vector3 positionNoise = Random.insideUnitSphere * config.positionNoiseIntensity;
        Quaternion rotationNoise = Quaternion.Slerp(Quaternion.identity, Random.rotationUniform, config.rotationNoiseIntensity * Time.deltaTime);

        //Vector3 noisyPosition = groundTruthTransform.position + _accumulatedDrift + positionNoise;
        Vector3 globalDrift = sharedDrift != null ? sharedDrift.CurrentDrift : Vector3.zero;
        Vector3 noisyPosition = groundTruthTransform.position + globalDrift + positionNoise;

        Quaternion noisyRotation = groundTruthTransform.rotation * rotationNoise;

        int confidence = 2;

        if (_confidenceOverride != null && _confidenceOverride.enabledOverride)
        {
            confidence = Mathf.Min(confidence, _confidenceOverride.maxConfidenceWhileDegraded);
        }

        if (_confidenceOverride != null && _confidenceOverride.enabledOverride)
        {
            if (Random.value < _confidenceOverride.dropProbability)
            {        
                return;
            }
        }

        PoseData pose = new PoseData
        {
            DroneId = this.droneId,
            Timestamp = Time.timeAsDouble,
            Position = noisyPosition,
            Rotation = noisyRotation,
            TrackingConfidence = confidence
        };

        broadcaster?.BroadcastPose(pose);

        if (_confidenceOverride != null && _confidenceOverride.enabledOverride && Time.frameCount % 120 == 0)
        {
            Debug.Log(
                $"[ConfidenceOverride] Drone {droneId} forcing TrackingConfidence={confidence}"
            );
        }


        if (Time.frameCount % 300 == 0)
        {
            Debug.Log($"[SyntheticPoseProvider {droneId}] GT={groundTruthTransform.position.ToString("F2")} Drift={globalDrift.ToString("F2")}");
        }
    }


}