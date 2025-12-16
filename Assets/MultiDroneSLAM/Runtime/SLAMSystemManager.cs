using UnityEngine;
using System.Collections.Generic;
using System.Text;

public class SLAMSystemManager : MonoBehaviour
{
    [System.Serializable]
    public class DronePair
    {
        public string name;
        public GameObject providerObject;
        public GameObject controllerObject;

        [HideInInspector] public IPoseProvider provider;
        [HideInInspector] public IDroneController controller;
        [HideInInspector] public DebugPoseViewer debugger;
    }

    [Header("Drone Configuration")]
    [SerializeField] private List<DronePair> dronePairs = new List<DronePair>();

    [Header("Alignment Settings")]
    [SerializeField] private int anchorDroneId = 0;

    [Header("Ground Truth (For Simulation Only)")]
    public Transform anchorTruthTransform;
    public List<Transform> clientTruthTransforms;

    private Dictionary<int, PoseData> _lastReceivedPoses = new Dictionary<int, PoseData>();
    private Dictionary<int, Pose> _trueRelativeOffsets = new Dictionary<int, Pose>();

    void Awake()
    {
        InitializeSystem();
    }

    void Start()
    {
        // Calculate the true offsets once at the beginning of the simulation.
        CalculateTrueOffsets();
    }

    private void InitializeSystem()
    {
        foreach (var pair in dronePairs)
        {
            pair.provider = pair.providerObject?.GetComponent<IPoseProvider>();
            pair.controller = pair.controllerObject?.GetComponent<IDroneController>();
            pair.debugger = pair.controllerObject?.GetComponent<DebugPoseViewer>();

            if (pair.provider != null)
            {
                pair.provider.OnPoseReceived += HandlePoseReceived;
            }

            if (pair.debugger == null)
            {
                Debug.LogWarning($"No DebugPoseViewer found on {pair.controllerObject.name}. Positional logs will not be shown for this drone.");
            }
        }
    }

    private void ShutdownSystem()
    {
        foreach (var pair in dronePairs)
        {
            if (pair.provider != null)
            {
                pair.provider.OnPoseReceived -= HandlePoseReceived;
            }
        }
    }

    private void CalculateTrueOffsets()
    {
        if (anchorTruthTransform == null)
        {
            Debug.LogError("Anchor Truth Transform is not assigned in the manager!");
            return;
        }

        Pose anchorTruthPose = new Pose { position = anchorTruthTransform.position, rotation = anchorTruthTransform.rotation };

        foreach (var clientTransform in clientTruthTransforms)
        {
            var provider = clientTransform.GetComponent<SyntheticPoseProvider>();
            if (provider != null)
            {
                int clientId = provider.droneId;
                Pose clientTruthPose = new Pose { position = clientTransform.position, rotation = clientTransform.rotation };

                // The offset is the pose of the client relative to the anchor in the real world.
                Pose relativeOffset = anchorTruthPose.Inverse() * clientTruthPose;
                _trueRelativeOffsets[clientId] = relativeOffset;

                Debug.Log($"Calculated TRUE relative offset for Drone {clientId}: Pos={relativeOffset.position.ToString("F3")}");
            }
        }
    }

    private void HandlePoseReceived(PoseData rawPose)
    {
        _lastReceivedPoses[rawPose.DroneId] = rawPose;
        var targetController = FindControllerForId(rawPose.DroneId);
        var targetDebugger = FindDebuggerForId(rawPose.DroneId);
        if (targetController == null) return;

        // --- ANCHOR DRONE LOGIC ---
        if (rawPose.DroneId == anchorDroneId)
        {
            targetController.UpdatePose(rawPose);
            targetDebugger?.LogInfo($"Received ANCHOR pose. Pos: {rawPose.Position.ToString("F3")}");
            return;
        }

        // --- LIVE CLIENT ALIGNMENT LOGIC ---
        if (_lastReceivedPoses.ContainsKey(anchorDroneId))
        {
            Pose anchorSlamPose = new Pose
            {
                position = _lastReceivedPoses[anchorDroneId].Position,
                rotation = _lastReceivedPoses[anchorDroneId].Rotation
            };

            Pose clientSlamPose = new Pose
            {
                position = rawPose.Position,
                rotation = rawPose.Rotation
            };

            // Compute client motion relative to anchor SLAM frame
            Pose relativeClientPose = anchorSlamPose.Inverse() * clientSlamPose;

            // Re-apply into anchor world frame (shared drift)
            Pose correctedPose = anchorSlamPose * relativeClientPose;

            PoseData correctedPoseData = new PoseData
            {
                DroneId = rawPose.DroneId,
                Timestamp = rawPose.Timestamp,
                Position = correctedPose.position,
                Rotation = correctedPose.rotation,
                TrackingConfidence = rawPose.TrackingConfidence
            };

            targetController.UpdatePose(correctedPoseData);

            targetDebugger?.LogInfo(
                $"LIVE CLIENT ALIGN: Rel={relativeClientPose.position.ToString("F3")} World={correctedPose.position.ToString("F3")}"
            );
        }


    }

    private IDroneController FindControllerForId(int id)
    {
        foreach (var pair in dronePairs)
        {
            if (pair.controller != null && pair.controller.DroneId == id) return pair.controller;
        }
        return null;
    }

    private DebugPoseViewer FindDebuggerForId(int id)
    {
        foreach (var pair in dronePairs)
        {
            if (pair.controller != null && pair.controller.DroneId == id) return pair.debugger;
        }
        return null;
    }
}