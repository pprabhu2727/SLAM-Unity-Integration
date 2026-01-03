using UnityEngine;

/* 
 * -----------------------------------------------------------------------------
 * SIMULATION-ONLY CODE
 * This section is not requried for real slam integration. 
 * 
 * Essentially this file tries simulating everything a RealSense would do but without too much complexity or image processing. 
 * It converts the ground truth motion into a noisy, drifting, and unreliable SLAM output to try to be as realistic to actual output we would get from a RealSense
 * -----------------------------------------------------------------------------
 */
public class SyntheticPoseProvider : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private SyntheticProviderConfig config; // Noise parameters
    [SerializeField] private Transform groundTruthTransform; // Actual motion and movement
    [SerializeField] private SharedDriftManager sharedDrift; // Global drift shared between all drones

    [Tooltip("The unique ID for this simulated drone.")]
    [SerializeField] public int droneId = 0;

    [Header("Output")]
    [SerializeField] private UDPPoseBroadcaster broadcaster;

    [Header("RealSense Compatibility")]
    [SerializeField] private float outputHz = 60f;

    private float _nextAllowedSendTime = 0f;

    private SLAMConfidenceOverride _confidenceOverride; //Used for purposely worsening the SLAM confidence


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

        // Simulation of SLAM performance drop if the setting is enabled
        float effectiveHz = outputHz;
        if (_confidenceOverride != null && _confidenceOverride.enabledOverride)
        {
            effectiveHz = Mathf.Min(outputHz, _confidenceOverride.degradedOutputHz);
        }

        if (Time.time < _nextAllowedSendTime)
            return;

        _nextAllowedSendTime = Time.time + (1f / Mathf.Max(1f, effectiveHz));


        //_accumulatedDrift += config.driftPerSecond * Time.deltaTime;

        //Adding noise to position and rotation
        Vector3 positionNoise = Random.insideUnitSphere * config.positionNoiseIntensity;
        Quaternion rotationNoise = Quaternion.Slerp(Quaternion.identity, Random.rotationUniform, config.rotationNoiseIntensity * Time.deltaTime);

        //Vector3 noisyPosition = groundTruthTransform.position + _accumulatedDrift + positionNoise;

        //Adding drift
        Vector3 globalDrift = sharedDrift != null ? sharedDrift.CurrentDrift : Vector3.zero;
        Vector3 noisyPosition = groundTruthTransform.position + globalDrift + positionNoise;

        Quaternion noisyRotation = groundTruthTransform.rotation * rotationNoise;

        //If performance is effected, SLAM can't claim good confidence
        int confidence = 2;
        if (_confidenceOverride != null && _confidenceOverride.enabledOverride)
        {
            confidence = Mathf.Min(confidence, _confidenceOverride.maxConfidenceWhileDegraded);
        }

        //Simulates packet loss by returning early
        if (_confidenceOverride != null && _confidenceOverride.enabledOverride)
        {
            if (Random.value < _confidenceOverride.dropProbability)
            {        
                return;
            }
        }

        //Emit pose
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