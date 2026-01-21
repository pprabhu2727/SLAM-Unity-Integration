using UnityEngine;
using System.Collections.Generic;
using System.Text;

/*
 * This is the main class of this project that takes in pose data and does stuff in Unity with it.
 * 
 * It does the following: takes in pose, aligns multiple drones, drift correction and relocalization, pose quality tracking, anchor health and switching,
 *      velocity estimation, predictive collision detection and soft prevention, exposes the state for metrics used by the HUD. 
 */
public class SLAMSystemManager : MonoBehaviour
{
    [System.Serializable]
    public class DronePair
    {
        public string name;
        public GameObject providerObject; //where pose data comes from
        public GameObject controllerObject; //where pose data goes

        [Tooltip("Collision safety radius in meters (SLAM space).")]
        public float safetyRadius = 0.5f;

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

    [Header("Collision Stability")]
    [SerializeField] private float collisionHoldSeconds = 0.25f;
    private float _collisionHoldUntil = -1f;


    private Dictionary<int, PoseData> _lastReceivedPoses = new Dictionary<int, PoseData>(); //Raw SLAM poses
    private Dictionary<int, Pose> _trueRelativeOffsets = new Dictionary<int, Pose>();

    private Dictionary<int, Vector3> _lastWorldPositions = new Dictionary<int, Vector3>(); //Corrected world positions

    private PoseQualityMonitor _quality;

    private Dictionary<int, Transform> _truthByDroneId = new Dictionary<int, Transform>();

    private Dictionary<int, IMotionLimiter> _motionLimiters = new();
    private Dictionary<int, float> _frameSpeedLimits = new();
    private Dictionary<int, float> _confidenceSpeedScale = new();
 

    //Relocalization Fields
    [Header("Relocalization")]
    [SerializeField] private bool enableRelocalization = true;

    //[Tooltip("Press this key to re-anchor the drifting SLAM world back onto Anchor Truth. (Not using anymore)")]
    //[SerializeField] private KeyCode relocalizeKey = KeyCode.R;

    [Tooltip("How long (seconds) to blend the relocalization correction (avoid snapping).")]
    [SerializeField] private float relocalizeBlendSeconds = 0.75f;

    [Tooltip("If enabled, auto-relocalize when drift magnitude exceeds this (meters). 0 disables auto.")]
    [SerializeField] private float autoRelocalizeThresholdMeters = 0.0f;

    [SerializeField] private float autoRelocalizeCooldownSeconds = 2.0f;

    // World correction state. Its the transform that maps SLAM space into Unity space
    private Pose _worldCorrection = Pose.identity;
    private Pose _worldCorrectionStart = Pose.identity;
    private Pose _worldCorrectionTarget = Pose.identity;
    private float _worldCorrectionBlendT0 = -1f;
    private bool _isBlendingWorldCorrection = false;
    private float _lastAutoRelocalizeTime = -999f;

    [Header("Pose Freshness Handling")]
    [Tooltip("If no pose received for this long, the drone is considered stale.")]
    [SerializeField] private float stalePoseSeconds = 0.25f;

    [Tooltip("If true, stale drones stop updating pose (freeze).")]
    [SerializeField] private bool freezeOnStalePose = true;

    private Dictionary<int, bool> _isDroneStale = new Dictionary<int, bool>();

    [Header("Anchor Management")]
    [SerializeField] private int minTrackingConfidenceForAnchor = 1;
    [SerializeField] private float anchorRecheckSeconds = 0.5f;
    private float _lastAnchorCheckTime = -999f;
    private Dictionary<int, int> _lastTrackingConfidence = new Dictionary<int, int>();

    [Header("Anchor Failure Thresholds")]
    [SerializeField] private float anchorFailureSeconds = 10.0f;
    private float _anchorUnhealthyStartTime = -1f;
    [SerializeField] private float anchorSwitchCooldownSeconds = 5.0f;
    private float _lastAnchorSwitchTime = -999f;

    [Header("Startup Guard")]
    [SerializeField] private float startupGraceSeconds = 2.0f;
    private float _startupTime;

    [Header("Predictive Collision")]
    [SerializeField] private float predictionHorizonSeconds = 2.0f;
    [SerializeField] private float minRelativeSpeed = 0.05f;

    [Header("Collision Prevention")]
    [SerializeField] private bool enableCollisionPrevention = true;

    [Tooltip("Distance at which slowing begins (in meters).")]
    [SerializeField] private float slowDownDistance = 2.5f;

    [Tooltip("Distance at which motion toward another reaches max slowdown.")]
    [SerializeField] private float hardStopDistance = 1.5f;

    [Tooltip("Maximum speed allowed when fully slowed.")]
    [SerializeField] private float minAllowedSpeed = 0f;

    [Header("Control Barrier Safety")]
    [SerializeField] private float barrierStartDistance = 2f;
    [SerializeField] private float barrierHardDistance = 1f;
    [SerializeField] private float barrierAlpha = 3.0f;
    private Dictionary<int, MotionRejection> _motionRejections = new();
    private float _debugBarrierH;
    private float _debugBarrierDhdt;
    private bool _debugBarrierActive;





    [Header("SLAM Confidence Scaling")]
    [SerializeField] private bool enableConfidenceScaling = true;
    [SerializeField] private float goodConfidenceScale = 1.0f;
    [SerializeField] private float degradedConfidenceScale = 0.5f;
    [SerializeField] private float poorConfidenceScale = 0.25f;

    private struct CollisionDebugState
    {
        public bool active;
        public float ttc;
        public float closingSpeed;
        public float appliedScale;
    }
    private CollisionDebugState _collisionDebug;

    private class PoseSample
    {
        public Vector3 position;
        public double timestamp;
    }

    //Used for speed computation and predictive collision
    private Dictionary<int, PoseSample> _previousPose = new Dictionary<int, PoseSample>();
    private Dictionary<int, Vector3> _estimatedVelocity = new Dictionary<int, Vector3>();




    // Throttles periodic drift diagnostics
    private int _lastDriftLogFrame = -999999;

    //Initialization
    void Awake()
    {
        _startupTime = Time.time;
        InitializeSystem();

        _quality = GetComponent<PoseQualityMonitor>();
        if (_quality == null)
        {
            Debug.LogWarning("PoseQualityMonitor not found on manager. Quality stats disabled.");
        }

    }

    void Start()
    {
        // Calculate the true offsets once at the beginning of the simulation.
        CalculateTrueOffsets();

        // Register truth transforms by drone ID
        _truthByDroneId.Clear();

        //Register anchor truth object
        if (anchorTruthTransform != null)
        {
            _truthByDroneId[anchorDroneId] = anchorTruthTransform;
        }

        //Register client truth objects using drone ID
        foreach (var t in clientTruthTransforms)
        {
            var provider = t.GetComponent<SyntheticPoseProvider>();
            if (provider != null)
            {
                _truthByDroneId[provider.droneId] = t;
            }
        }

        RegisterMotionLimitersFromTruth();


    }

    //Main runtime control loop
    private void Update()
    {
        if (!enableRelocalization) return;

        _frameSpeedLimits.Clear();
        _confidenceSpeedScale.Clear();
        _motionRejections.Clear();
        _debugBarrierActive = false;


        //Used to add a slight buffer to prevent collision status in the HUD from flickering
        bool collisionStillHeld = Time.time < _collisionHoldUntil; 
        if (!collisionStillHeld)
        {
            _collisionDebug.active = false;
        }

        //Set to full speed default
        foreach (var id in _motionLimiters.Keys)
        {
            _frameSpeedLimits[id] = 1f;
        }


        //// Manual relocalize
        //if (Input.GetKeyDown(relocalizeKey))
        //{
        //    TryStartRelocalize("MANUAL");
        //}


        // Blend world correction over time to prevent objects from snapping instantly to a position
        if (_isBlendingWorldCorrection)
        {
            float t = Mathf.Clamp01((Time.time - _worldCorrectionBlendT0) / Mathf.Max(0.0001f, relocalizeBlendSeconds));
            _worldCorrection = PoseUtils.Lerp(_worldCorrectionStart, _worldCorrectionTarget, t);

            if (t >= 1f)
            {
                _isBlendingWorldCorrection = false;
                Debug.Log($"[Relocalize] Blend complete. WorldCorrection={PoseUtils.ToStringF3(_worldCorrection)}");
            }
        }

        // Auto relocalize. Checks drift magnitude and cooldown and will start Relocalizing if it drifts outside the threshold 
        if (autoRelocalizeThresholdMeters > 0.0f && !_isBlendingWorldCorrection)
        {
            if (Time.time - _lastAutoRelocalizeTime > autoRelocalizeCooldownSeconds)
            {
                float drift = EstimateAnchorDriftMeters();
                if (drift >= autoRelocalizeThresholdMeters)
                {
                    TryStartRelocalize($"AUTO drift={drift:F3}m");
                    _lastAutoRelocalizeTime = Time.time;
                }
            }
        }

        // Periodic drift logging (every ~120 frames)
        if (Time.frameCount - _lastDriftLogFrame > 120)
        {
            _lastDriftLogFrame = Time.frameCount;
            float drift = EstimateAnchorDriftMeters();
            if (drift >= 0f)
            {
                Debug.Log($"[Drift] Anchor drift magnitude approx. = {drift:F3}m | auto={(autoRelocalizeThresholdMeters > 0 ? "ON" : "OFF")} thr={autoRelocalizeThresholdMeters:F2} " +
          $"| worldCorrection={(!_isBlendingWorldCorrection ? "stable" : "blending")}");
            }
        }

        // Additional Debug logging for Quality (includes packet rate and jitter)
        if (Time.frameCount % 120 == 0 && _quality != null)
        {
            for (int i = 0; i < dronePairs.Count; i++)
            {
                int id = dronePairs[i].provider != null ? dronePairs[i].provider.DroneId : -1;
                if (id < 0) continue;

                if (_quality.TryGetStats(id, out float pps, out float sinceLast, out float emaDt, out float emaJit))
                {
                    Debug.Log($"[Quality] Drone {id}: pps approx. ={pps:F1} sinceLast={sinceLast:F2}s emaDt={emaDt:F3}s emaJitter={emaJit:F3}s");
                }
            }
        }

        //Staleness. Default behavior is when poses become stale the drone should freeze
        if (_quality != null)
        {
            foreach (var pair in dronePairs)
            {
                if (pair.controller == null) continue;

                int id = pair.controller.DroneId;

                if (_quality.TryGetStats(id, out _, out float sinceLast, out _, out _))
                {
                    bool stale = sinceLast > stalePoseSeconds;

                    if (!_isDroneStale.TryGetValue(id, out bool wasStale) || wasStale != stale)
                    {
                        _isDroneStale[id] = stale;

                        if (stale)
                        {
                            Debug.LogWarning($"[Stale] Drone {id} pose stale ({sinceLast:F2}s). Freezing updates.");
                        }
                        else
                        {
                            Debug.Log($"[Stale] Drone {id} pose resumed ({sinceLast:F2}s).");
                        }
                    }
                }
            }
        }

        //Anchor Health
        if (Time.time - _lastAnchorCheckTime > anchorRecheckSeconds)
        {
            _lastAnchorCheckTime = Time.time;

            //Don't try anchor switching on startup
            if (Time.time - _startupTime < startupGraceSeconds)
                return;

            if (IsAnchorFailed() &&
                Time.time - _lastAnchorSwitchTime > anchorSwitchCooldownSeconds)
            {
                int candidate = FindBestAnchorCandidate();
                if (candidate >= 0)
                {
                    SwitchAnchor(candidate);
                    _lastAnchorSwitchTime = Time.time;
                }
                else
                {
                    Debug.LogError("[AnchorSwitch] No healthy anchor candidates available.");
                }
            }
        }


        //Collision related methods. One checks for immediate collision while the other does predictions
        if (enableCollisionPrevention)
        {
            CheckForCollisions();
            CheckForPredictedCollisions();
        }

        //Applies motion limits to the drones based on various factors such as if pose confidence is low or a collision is predicted
        foreach (var id in _motionLimiters.Keys)
        {
            var limiter = _motionLimiters[id];

            float collisionScale = _collisionDebug.active ? (_frameSpeedLimits.TryGetValue(id, out float c) ? c : 1f): 1f;
            float confidenceScale = GetConfidenceSpeedScale(id);
            float finalScale = collisionScale * confidenceScale;

            limiter.SetSpeedScale(finalScale);

            if (_lastTrackingConfidence.TryGetValue(id, out int conf))
            {
                limiter.SetAxisMask(GetAxisMaskForConfidence(conf));
            }

            if (_motionRejections.TryGetValue(id, out var rejection))
            {
                limiter.SetMotionRejection(rejection);
            }
            else
            {
                limiter.SetMotionRejection(new MotionRejection { active = false });
            }

            Debug.Log(
                $"[SpeedApply] Drone {id} collision={collisionScale:F2} " +
                $"confidence={confidenceScale:F2} final={finalScale:F2}"
            );
        }




    }

    // Acquires the scene objects (provider, controller, debugger)
    private void InitializeSystem()
    {
        foreach (var pair in dronePairs)
        {
            pair.provider = pair.providerObject?.GetComponent<IPoseProvider>();
            pair.controller = pair.controllerObject?.GetComponent<IDroneController>();
            pair.debugger = pair.controllerObject?.GetComponent<DebugPoseViewer>();

            //Wires SLAM data into the system
            if (pair.provider != null)
            {
                pair.provider.OnPoseReceived += HandlePoseReceived;
            }

            if (pair.debugger == null)
            {
                Debug.LogWarning($"No DebugPoseViewer found on {pair.controllerObject.name}. Positional logs will not be shown for this drone.");
            }

            //Debug.Log($"[Limiter] Registered motion limiters: {_motionLimiters.Count}");
            foreach (var kvp in _motionLimiters)
                Debug.Log($"[Limiter] id={kvp.Key} limiter={kvp.Value}");

        }


    }

    //Unsubscribes to pose events
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

    /* 
     * -----------------------------------------------------------------------------
     * SIMULATION-ONLY CODE
     * This section is not requried for real slam integration. 
     * -----------------------------------------------------------------------------
     */

    //Figures out where each drone is relative to the anchor. In a real system we couldnt know the true offsets but we can in this project for testing and accuracy purposes
    private void CalculateTrueOffsets()
    {
        if (anchorTruthTransform == null)
        {
            Debug.LogError("Anchor Truth Transform is not assigned in the manager!");
            return;
        }

        //Absolute truth
        Pose anchorTruthPose = new Pose { position = anchorTruthTransform.position, rotation = anchorTruthTransform.rotation };

        //For each clients truth transform, map its truth object to drone ID, then compute and store the relative pose
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

    //Entry point for incoming SLAM pose data
    private void HandlePoseReceived(PoseData rawPose)
    {
        _quality?.NotePacket(rawPose.DroneId); //Packet timing info recorded for PoseQualityMonitor
        _lastReceivedPoses[rawPose.DroneId] = rawPose; 

        // Ignore incoming pose updates while stale
        if (freezeOnStalePose && _isDroneStale.TryGetValue(rawPose.DroneId, out bool isStale) && isStale)
        {
                return;
        }

        _lastTrackingConfidence[rawPose.DroneId] = rawPose.TrackingConfidence;

        //Finds the correct drone based on the incoming pose
        var targetController = FindControllerForId(rawPose.DroneId);
        var targetDebugger = FindDebuggerForId(rawPose.DroneId);
        if (targetController == null) return;

        // --- ANCHOR DRONE LOGIC ---
        if (rawPose.DroneId == anchorDroneId)
        {
            Pose anchorSlamPose = new Pose { position = rawPose.Position, rotation = rawPose.Rotation };
            Pose anchorWorldPose = _worldCorrection * anchorSlamPose;

            PoseData anchorWorldPoseData = new PoseData
            {
                DroneId = rawPose.DroneId,
                Timestamp = rawPose.Timestamp,
                Position = anchorWorldPose.position,
                Rotation = anchorWorldPose.rotation,
                TrackingConfidence = rawPose.TrackingConfidence
            };

            targetController.UpdatePose(anchorWorldPoseData);
            _lastWorldPositions[rawPose.DroneId] = anchorWorldPoseData.Position;
            UpdateVelocityEstimate(rawPose.DroneId, anchorWorldPoseData.Position, rawPose.Timestamp);
            targetDebugger?.LogInfo($"ANCHOR WORLD: {anchorWorldPoseData.Position.ToString("F3")}");
            return;

        }

        // --- CLIENT DRONE LOGIC ---
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

            // Re-apply into anchor SLAM frame (shared drifting SLAM world)
            Pose alignedInSlamWorld = anchorSlamPose * relativeClientPose;
            Pose correctedWorldPose = _worldCorrection * alignedInSlamWorld;


            PoseData correctedPoseData = new PoseData
            {
                DroneId = rawPose.DroneId,
                Timestamp = rawPose.Timestamp,
                Position = correctedWorldPose.position,
                Rotation = correctedWorldPose.rotation,
                TrackingConfidence = rawPose.TrackingConfidence
            };
            _lastWorldPositions[rawPose.DroneId] = correctedWorldPose.position;
            UpdateVelocityEstimate(rawPose.DroneId, correctedWorldPose.position, rawPose.Timestamp);

            targetController.UpdatePose(correctedPoseData);

            Pose anchorWorldPose = _worldCorrection * anchorSlamPose;
            targetDebugger?.LogInfo(
                $"LIVE CLIENT ALIGN: Rel={relativeClientPose.position.ToString("F3")} " +
                $"SlamWorld={alignedInSlamWorld.position.ToString("F3")} " +
                $"AnchorWorld={anchorWorldPose.position.ToString("F3")} " +
                $"FinalWorld={correctedWorldPose.position.ToString("F3")}"
            );
        }


    }

    //Maps drone ID to a Unity motion controller
    private IDroneController FindControllerForId(int id)
    {
        foreach (var pair in dronePairs)
        {
            if (pair.controller != null && pair.controller.DroneId == id) return pair.controller;
        }
        return null;
    }

    //Maps drone ID to a Unity debugger
    private DebugPoseViewer FindDebuggerForId(int id)
    {
        foreach (var pair in dronePairs)
        {
            if (pair.controller != null && pair.controller.DroneId == id) return pair.debugger;
        }
        return null;
    }

    /* 
     * -----------------------------------------------------------------------------
     * CONTAINS SIMULATION-ONLY CODE
     * Parts of this section is not requried for real slam integration. 
     * -----------------------------------------------------------------------------
     */
    //Gets the distance between anchor SLAM pose and anchor truth.
    //In this simulation if the drone drifts far enough from the truth it triggers relocalization.
    //But in a real system this method needs to be replaced with a different trigger based on how confident the system is that the map and pose are still valid. 
    private float EstimateAnchorDriftMeters()
    {
        if (anchorTruthTransform == null) return -1f;
        if (!_lastReceivedPoses.ContainsKey(anchorDroneId)) return -1f;

        Vector3 anchorTruthPos = anchorTruthTransform.position; //Get truth position

        Pose anchorSlamPose = new Pose //Get SLAM pose
        {
            position = _lastReceivedPoses[anchorDroneId].Position,
            rotation = _lastReceivedPoses[anchorDroneId].Rotation
        };

        Pose anchorWorldPose = _worldCorrection * anchorSlamPose; //Apply world correction
        return Vector3.Distance(anchorTruthPos, anchorWorldPose.position); //Then compute distance
    }

    //Realign SLAM world back to truth
    private void TryStartRelocalize(string reason)
    {
        if (anchorTruthTransform == null)
        {
            Debug.LogWarning("[Relocalize] anchorTruthTransform not set, cannot relocalize.");
            return;
        }

        if (!_lastReceivedPoses.ContainsKey(anchorDroneId))
        {
            Debug.LogWarning("[Relocalize] No anchor SLAM pose received yet, cannot relocalize.");
            return;
        }

        //Truth. Where the drone should be
        Pose anchorTruthPose = new Pose 
        { 
            position = anchorTruthTransform.position, 
            rotation = anchorTruthTransform.rotation 
        };

        //SLAM pose. Where the drone thinks it is
        Pose anchorSlamPose = new Pose
        {
            position = _lastReceivedPoses[anchorDroneId].Position,
            rotation = _lastReceivedPoses[anchorDroneId].Rotation
        };

        // WorldCorrection * AnchorSlamPose == AnchorTruthPose
        // => WorldCorrection = AnchorTruthPose * inverse(AnchorSlamPose)

        //Math: need a target such that target * anchorTruthPose = truth
        Pose target = anchorTruthPose * anchorSlamPose.Inverse();

        //Sets up a smooth visual blending instead of instantly jumping objects during a relocalization
        _worldCorrectionStart = _worldCorrection;
        _worldCorrectionTarget = target;
        _worldCorrectionBlendT0 = Time.time;
        _isBlendingWorldCorrection = true;

        float driftBefore = Vector3.Distance(anchorTruthPose.position, anchorSlamPose.position);
        Debug.Log($"[Relocalize] START ({reason}) driftBefore={driftBefore:F3}m " +
                  $"TargetWorldCorrection={PoseUtils.ToStringF3(_worldCorrectionTarget)}");
    }

    [ContextMenu("Relocalize Now (Manual)")]
    public void RelocalizeNow_FromInspector()
    {
        if (!enableRelocalization)
        {
            Debug.LogWarning("[Relocalize] enableRelocalization is false.");
            return;
        }
        TryStartRelocalize("MANUAL (Inspector Context Menu)");
    }

    // Debug / HUD Accessors
    public float Debug_GetAnchorDriftMeters()
    {
        return EstimateAnchorDriftMeters();
    }

    public bool Debug_IsRelocalizing()
    {
        return _isBlendingWorldCorrection;
    }

    public float Debug_GetStaleThreshold()
    {
        return stalePoseSeconds;
    }

    public IEnumerable<int> Debug_GetDroneIds()
    {
        foreach (var pair in dronePairs)
        {
            if (pair.controller != null)
                yield return pair.controller.DroneId;
        }
    }

    // --- Anchor Related Helper Methods ---
    
    //Checks to see if the anchor should be replaced based on  if its been unhealthy for a long enough time
    private bool IsAnchorFailed()
    {
        if (!_lastReceivedPoses.ContainsKey(anchorDroneId))
            return true;

        bool stale = _isDroneStale.TryGetValue(anchorDroneId, out bool s) && s;
        bool lowConfidence = _lastTrackingConfidence.TryGetValue(anchorDroneId, out int conf) &&
                             conf < minTrackingConfidenceForAnchor;

        if (stale || lowConfidence)
        { 
            if (_anchorUnhealthyStartTime < 0f)
                _anchorUnhealthyStartTime = Time.time;

            //Debug Log
            if (Time.frameCount % 60 == 0)
            {
                Debug.LogWarning(
                    $"[AnchorHealth] Anchor unhealthy for {(Time.time - _anchorUnhealthyStartTime):F2}s " +
                    $"(stale={stale}, conf={(lowConfidence ? "LOW" : "OK")})"
                );
            }

            // Has been continuously unhealthy long enough and is considered failed
            if (Time.time - _anchorUnhealthyStartTime >= anchorFailureSeconds)
                return true;

            return false; // not failed yet
        }

        // Anchor is healthy. Reset timer
        _anchorUnhealthyStartTime = -1f;
        return false;
    }

    //Selects a new anchor that is not stale, has good confidence, and isnt the current anchor
    private int FindBestAnchorCandidate()
    {
        foreach (var pair in dronePairs)
        {
            if (pair.controller == null) continue;

            int id = pair.controller.DroneId;
            if (id == anchorDroneId) continue;

            if (_isDroneStale.TryGetValue(id, out bool stale) && stale)
                continue;

            if (_lastTrackingConfidence.TryGetValue(id, out int conf) &&
                conf >= minTrackingConfidenceForAnchor)
            {
                return id;
            }
        }

        return -1;
    }

    //Switches the anchor by preserving the old world pose and computing the correction so the new anchor ends up at that old pose. 
    private void SwitchAnchor(int newAnchorId)
    {
        if (newAnchorId == anchorDroneId) return;

        Debug.LogWarning($"[AnchorSwitch] Switching anchor from {anchorDroneId} to {newAnchorId}");

        // WorldCorrection_new * NewAnchorSlamPose == OldAnchorWorldPose

        Pose oldAnchorWorldPose = Pose.identity;

        if (_lastReceivedPoses.ContainsKey(anchorDroneId))
        {
            Pose oldAnchorSlam = new Pose
            {
                position = _lastReceivedPoses[anchorDroneId].Position,
                rotation = _lastReceivedPoses[anchorDroneId].Rotation
            };

            oldAnchorWorldPose = _worldCorrection * oldAnchorSlam;
        }

        Pose newAnchorSlamPose = new Pose
        {
            position = _lastReceivedPoses[newAnchorId].Position,
            rotation = _lastReceivedPoses[newAnchorId].Rotation
        };

        Pose newWorldCorrection = oldAnchorWorldPose * newAnchorSlamPose.Inverse();

        _worldCorrection = newWorldCorrection;
        _worldCorrectionStart = newWorldCorrection;
        _worldCorrectionTarget = newWorldCorrection;
        _isBlendingWorldCorrection = false;

        anchorDroneId = newAnchorId;
        _anchorUnhealthyStartTime = -1f;

        // Switch the anchor truth reference
        if (_truthByDroneId.TryGetValue(newAnchorId, out Transform newTruth))
        {
            anchorTruthTransform = newTruth;
            Debug.Log($"[AnchorSwitch] Anchor truth switched to GroundTruth of drone {newAnchorId}");
        }
        else
        {
            Debug.LogWarning($"[AnchorSwitch] No truth transform registered for drone {newAnchorId}");
        }

        Debug.Log($"[AnchorSwitch] New anchor={anchorDroneId} WorldCorrection={PoseUtils.ToStringF3(_worldCorrection)}");
        
        RecalculateOffsetsForNewAnchor();

    }

    /* 
     * -----------------------------------------------------------------------------
     * SIMULATION-ONLY CODE
     * This section is not requried for real slam integration. 
     * -----------------------------------------------------------------------------
     */
    //Recompute the truth offsets whenenver an anchor switches
    private void RecalculateOffsetsForNewAnchor()
    {
        _trueRelativeOffsets.Clear();

        if (anchorTruthTransform == null) return;

        Pose anchorTruthPose = new Pose
        {
            position = anchorTruthTransform.position,
            rotation = anchorTruthTransform.rotation
        };

        foreach (var kvp in _truthByDroneId)
        {
            int id = kvp.Key;
            if (id == anchorDroneId) continue;

            Transform clientTruth = kvp.Value;

            Pose clientTruthPose = new Pose
            {
                position = clientTruth.position,
                rotation = clientTruth.rotation
            };

            Pose offset = anchorTruthPose.Inverse() * clientTruthPose;
            _trueRelativeOffsets[id] = offset;

            Debug.Log($"[AnchorSwitch] Recomputed offset for Drone {id}: {offset.position.ToString("F3")}");
        }
    }


    public int Debug_GetAnchorId()
    {
        return anchorDroneId;
    }

    // --- ---

    // --- Collision Related Methods ---
    private void CheckForCollisions()
    {
        for (int i = 0; i < dronePairs.Count; i++)
        {
            var a = dronePairs[i];
            if (a.controller == null) continue;

            int idA = a.controller.DroneId;
            if (!_lastWorldPositions.ContainsKey(idA)) continue;

            Vector3 posA = _lastWorldPositions[idA];

            for (int j = i + 1; j < dronePairs.Count; j++)
            {
                var b = dronePairs[j];
                if (b.controller == null) continue;

                int idB = b.controller.DroneId;
                if (!_lastWorldPositions.ContainsKey(idB)) continue;

                Vector3 posB = _lastWorldPositions[idB];

                float distance = Vector3.Distance(posA, posB);
                float safeDistance = a.safetyRadius + b.safetyRadius;

                if (distance < safeDistance)
                {
                    Debug.LogWarning(
                        $"[CollisionRisk] Drones {idA} and {idB} TOO CLOSE! " +
                        $"dist={distance:F2}m safe={safeDistance:F2}m"
                    );
                }
            }
        }
    }

    //Uses the difference between poses to estimate the velocity
    private void UpdateVelocityEstimate(int droneId, Vector3 currentPos, double timestamp)
    {
        if (_previousPose.TryGetValue(droneId, out PoseSample prev))
        {
            double dt = timestamp - prev.timestamp;
            if (dt > 1e-4)
            {
                Vector3 velocity = (currentPos - prev.position) / (float)dt;
                _estimatedVelocity[droneId] = velocity;
            }
        }

        _previousPose[droneId] = new PoseSample
        {
            position = currentPos,
            timestamp = timestamp
        };
    }

    //Will compute the relative motion, predict the closest approach, and if its within certain boundaries it figures out a safer slowdown speed the drone can follow
    private void CheckForPredictedCollisions()
    {
        for (int i = 0; i < dronePairs.Count; i++)
        {
            var a = dronePairs[i];
            if (a.controller == null) continue;

            int idA = a.controller.DroneId;
            if (!_lastWorldPositions.ContainsKey(idA) || !_estimatedVelocity.ContainsKey(idA))
                continue;

            Vector3 pA = _lastWorldPositions[idA];
            Vector3 vA = _estimatedVelocity[idA];

            for (int j = i + 1; j < dronePairs.Count; j++)
            {
                var b = dronePairs[j];
                if (b.controller == null) continue;

                int idB = b.controller.DroneId;
                if (!_lastWorldPositions.ContainsKey(idB) || !_estimatedVelocity.ContainsKey(idB))
                    continue;

                Vector3 pB = _lastWorldPositions[idB];
                Vector3 vB = _estimatedVelocity[idB];

                Vector3 pRel = pB - pA;
                float currentDistance = Vector3.Distance(pA, pB);
                Vector3 normalAB = pRel.normalized;

                Vector3 vRel = vB - vA;

                if (currentDistance < barrierStartDistance)
                {
                    float h = (currentDistance * currentDistance) - (barrierHardDistance * barrierHardDistance);

                    float dhdt = 2f * Vector3.Dot(pRel, vRel);

                    bool violating = dhdt < -barrierAlpha * h;

                    if (violating)
                    {
                        _motionRejections[idA] = new MotionRejection
                        {
                            active = true,
                            worldNormal = normalAB
                        };

                        _motionRejections[idB] = new MotionRejection
                        {
                            active = true,
                            worldNormal = -normalAB
                        };

                        _collisionDebug.active = true;
                        _collisionHoldUntil = Time.time + collisionHoldSeconds;

                        _debugBarrierH = h;
                        _debugBarrierDhdt = dhdt;
                        _debugBarrierActive = true;
                    }
                }


                float speedSq = vRel.sqrMagnitude;
                if (speedSq < minRelativeSpeed * minRelativeSpeed)
                    continue;

                float tClosest = -Vector3.Dot(pRel, vRel) / speedSq;

                if (tClosest <= 0f || tClosest > predictionHorizonSeconds)
                    continue;

                

                


                Vector3 pA_future = pA + vA * tClosest;
                Vector3 pB_future = pB + vB * tClosest;

                float futureDistance = Vector3.Distance(pA_future, pB_future);
                float safeDistance = a.safetyRadius + b.safetyRadius;

                if (futureDistance < safeDistance)
                {
                    Debug.LogWarning(
                        $"[PredictedCollision] Drones {idA} & {idB} " +
                        $"TTC={tClosest:F2}s futureDist={futureDistance:F2}m safe={safeDistance:F2}m"
                    );

                    Vector3 toB = (pB - pA).normalized;

                    bool aMovingToward = IsMovingToward(vA, toB);
                    bool bMovingToward = IsMovingToward(vB, -toB);

                    //Motion Rejection code for preventing drone movement in an unsafe direction
                    //bool insideHardZone = futureDistance < rejectDistance;
                    //if (insideHardZone)
                    //{
                    //    Vector3 normalAB = (pB - pA).normalized;

                    //    if (aMovingToward)
                    //    {
                    //        _motionRejections[idA] = new MotionRejection
                    //        {
                    //            active = true,
                    //            worldNormal = normalAB
                    //        };
                    //        _frameSpeedLimits[idA] = 0f;
                    //    }

                    //    if (bMovingToward)
                    //    {
                    //        _motionRejections[idB] = new MotionRejection
                    //        {
                    //            active = true,
                    //            worldNormal = -normalAB
                    //        };
                    //        _frameSpeedLimits[idB] = 0f;
                    //    }
                    //}

                    Vector3 relDir = pRel.normalized;
                    float closingSpeed = Mathf.Max(0f, Vector3.Dot(vRel, relDir));

                    const float aggressiveClosingSpeed = 1.5f;
                    float closing01 = Mathf.Clamp01(closingSpeed / aggressiveClosingSpeed);
                    float closingSpeedScale = Mathf.Lerp(1f, 0.2f, closing01);

                    float distanceScale = ComputeSpeedScale(futureDistance);

                    float scale = Mathf.Clamp01(distanceScale * closingSpeedScale);

                    if (aMovingToward && _motionLimiters.TryGetValue(idA, out var limiterA))
                    {
                        _frameSpeedLimits[idA] = Mathf.Min(_frameSpeedLimits[idA], scale);
                    }

                    if (bMovingToward && _motionLimiters.TryGetValue(idB, out var limiterB))
                    {
                        _frameSpeedLimits[idB] = Mathf.Min(_frameSpeedLimits[idB], scale);
                    }

                    //Saving info for HUD
                    _collisionDebug.active = true;
                    _collisionHoldUntil = Time.time + collisionHoldSeconds;
                    _collisionDebug.ttc = tClosest;
                    _collisionDebug.closingSpeed = closingSpeed;
                    _collisionDebug.appliedScale = scale;


                    Debug.LogWarning(
                        $"[Avoidance] Drones {idA} & {idB} " +
                        $"TTC={tClosest:F2}s dist={futureDistance:F2}m " +
                        $"closing={closingSpeed:F2}m/s scale={scale:F2}"
                    );

                }

            }


        }
    }

    private bool IsMovingToward(Vector3 velocity, Vector3 toOther)
    {
        return Vector3.Dot(velocity, toOther) > 0f;
    }

    private float ComputeSpeedScale(float distance)
    {
        if (distance <= hardStopDistance)
            return 0f;

        if (distance >= slowDownDistance)
            return 1f;

        float t = Mathf.InverseLerp(hardStopDistance, slowDownDistance, distance);

        float curved = Mathf.Pow(t, 4f);

        return Mathf.Clamp01(curved);
    }

    private void ResetSpeedLimits()
    {
        foreach (var limiter in _motionLimiters.Values)
        {
            limiter.SetSpeedScale(1f);
        }
    }

    //Uses IMotionLimiter component on world truth object and allows SLAM to indirectly control the objections
    private void RegisterMotionLimitersFromTruth()
    {
        _motionLimiters.Clear();

        foreach (var kvp in _truthByDroneId)
        {
            int id = kvp.Key;
            Transform truthT = kvp.Value;

            if (truthT == null) continue;

            var limiter = truthT.GetComponent<IMotionLimiter>();
            if (limiter != null)
            {
                _motionLimiters[id] = limiter;
                Debug.Log($"[Limiter] Registered IMotionLimiter for drone {id} on {truthT.name}");
            }
            else
            {
                Debug.LogWarning($"[Limiter] No IMotionLimiter found for drone {id} on truth object {truthT.name}");
            }
        }

        Debug.Log($"[Limiter] Total registered limiters = {_motionLimiters.Count}");
    }

    public bool Debug_TryGetDroneSpeed(int droneId, out float speed)
    {
        speed = 0f;

        if (_estimatedVelocity.TryGetValue(droneId, out var v))
        {
            speed = v.magnitude;
            return true;
        }

        return false;
    }

    // --- ---

    //SLAM confidence effects the speed
    private float GetConfidenceSpeedScale(int droneId)
    {
        if (!enableConfidenceScaling)
            return 1f;

        if (!_lastTrackingConfidence.TryGetValue(droneId, out int conf))
            return degradedConfidenceScale;

        return conf switch
        {
            >= 2 => goodConfidenceScale,
            1 => degradedConfidenceScale,
            _ => poorConfidenceScale
        };
    }

    public bool Debug_TryGetTrackingConfidence(int droneId, out int confidence)
    {
        return _lastTrackingConfidence.TryGetValue(droneId, out confidence);
    }

    private MotionAxisMask GetAxisMaskForConfidence(int conf)
    {
        return conf switch
        {
            >= 2 => new MotionAxisMask { allowX = true, allowY = true, allowZ = true, allowYaw = true }, // Good
            1 => new MotionAxisMask { allowX = true, allowY = false, allowZ = true, allowYaw = true }, // Degraded (stops vertical movement)
            0 => new MotionAxisMask { allowX = false, allowY = false, allowZ = false, allowYaw = true }, // Poor (drone can only rotate)
            _ => new MotionAxisMask { allowX = false, allowY = false, allowZ = false, allowYaw = false }  // Lost (no movement)
        };
    }


    //Accessors for HUD Collision Info
    public bool Debug_IsCollisionActive()
    {
        return _collisionDebug.active;
    }

    public float Debug_GetCollisionTTC()
    {
        return _collisionDebug.ttc;
    }

    public bool Debug_IsBarrierActive() 
    {
        return _debugBarrierActive;
    } 
    public float Debug_GetBarrierH()
    {
        return _debugBarrierH;
    }    
    public float Debug_GetBarrierDhdt()
    {
        return _debugBarrierDhdt;
    }  





}