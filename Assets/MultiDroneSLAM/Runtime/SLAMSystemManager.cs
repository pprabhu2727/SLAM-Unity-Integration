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

    private Dictionary<int, PoseData> _lastReceivedPoses = new Dictionary<int, PoseData>();
    private Dictionary<int, Pose> _trueRelativeOffsets = new Dictionary<int, Pose>();

    private Dictionary<int, Vector3> _lastWorldPositions = new Dictionary<int, Vector3>();

    private PoseQualityMonitor _quality;

    private Dictionary<int, Transform> _truthByDroneId = new Dictionary<int, Transform>();

    //Relocalization Fields
    [Header("Relocalization (Phase 6)")]
    [SerializeField] private bool enableRelocalization = true;

    //[Tooltip("Press this key to re-anchor the drifting SLAM world back onto Anchor Truth. (Depreciated)")]
    //[SerializeField] private KeyCode relocalizeKey = KeyCode.R;

    [Tooltip("How long (seconds) to blend the relocalization correction (avoid snapping).")]
    [SerializeField] private float relocalizeBlendSeconds = 0.75f;

    [Tooltip("If enabled, auto-relocalize when drift magnitude exceeds this (meters). 0 disables auto.")]
    [SerializeField] private float autoRelocalizeThresholdMeters = 0.0f;

    [SerializeField] private float autoRelocalizeCooldownSeconds = 2.0f;

    // World correction state
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

    [Header("Anchor Management (Phase 8)")]
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

    [Header("Predictive Collision (Phase 12)")]
    [SerializeField] private float predictionHorizonSeconds = 2.0f;
    [SerializeField] private float minRelativeSpeed = 0.05f;

    private class PoseSample
    {
        public Vector3 position;
        public double timestamp;
    }

    private Dictionary<int, PoseSample> _previousPose = new Dictionary<int, PoseSample>();
    private Dictionary<int, Vector3> _estimatedVelocity = new Dictionary<int, Vector3>();




    // optional log throttling
    private int _lastDriftLogFrame = -999999;

    void Awake()
    {
        _startupTime = Time.time;
        InitializeSystem();

        _quality = GetComponent<PoseQualityMonitor>();
        if (_quality == null)
        {
            Debug.LogWarning("[Phase7] PoseQualityMonitor not found on manager. Quality stats disabled.");
        }

    }

    void Start()
    {
        // Calculate the true offsets once at the beginning of the simulation.
        CalculateTrueOffsets();

        // Register truth transforms by drone ID
        _truthByDroneId.Clear();

        if (anchorTruthTransform != null)
        {
            _truthByDroneId[anchorDroneId] = anchorTruthTransform;
        }

        foreach (var t in clientTruthTransforms)
        {
            var provider = t.GetComponent<SyntheticPoseProvider>();
            if (provider != null)
            {
                _truthByDroneId[provider.droneId] = t;
            }
        }

    }

    private void Update()
    {
        if (!enableRelocalization) return;

        //// Manual relocalize
        //if (Input.GetKeyDown(relocalizeKey))
        //{
        //    TryStartRelocalize("MANUAL");
        //}

        // Blend world correction over time
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

        // Auto relocalize
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

        // Additional Debug logging for Quality
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

        //Staleness
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

        CheckForCollisions();
        CheckForPredictedCollisions();

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
        _quality?.NotePacket(rawPose.DroneId);
        _lastReceivedPoses[rawPose.DroneId] = rawPose;

        // Ignore incoming pose updates while stale
        if (freezeOnStalePose && _isDroneStale.TryGetValue(rawPose.DroneId, out bool isStale) && isStale)
        {
                return;
        }

        _lastTrackingConfidence[rawPose.DroneId] = rawPose.TrackingConfidence;

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

    private float EstimateAnchorDriftMeters()
    {
        if (anchorTruthTransform == null) return -1f;
        if (!_lastReceivedPoses.ContainsKey(anchorDroneId)) return -1f;

        Vector3 anchorTruthPos = anchorTruthTransform.position;

        Pose anchorSlamPose = new Pose
        {
            position = _lastReceivedPoses[anchorDroneId].Position,
            rotation = _lastReceivedPoses[anchorDroneId].Rotation
        };

        Pose anchorWorldPose = _worldCorrection * anchorSlamPose;
        return Vector3.Distance(anchorTruthPos, anchorWorldPose.position);
    }

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

        Pose anchorTruthPose = new Pose { position = anchorTruthTransform.position, rotation = anchorTruthTransform.rotation };

        Pose anchorSlamPose = new Pose
        {
            position = _lastReceivedPoses[anchorDroneId].Position,
            rotation = _lastReceivedPoses[anchorDroneId].Rotation
        };

        // WorldCorrection * AnchorSlamPose == AnchorTruthPose
        // => WorldCorrection = AnchorTruthPose * inverse(AnchorSlamPose)
        Pose target = anchorTruthPose * anchorSlamPose.Inverse();

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
        TryStartRelocalize("MANUAL (Inspector ContextMenu)");
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

    private void SwitchAnchor(int newAnchorId)
    {
        if (newAnchorId == anchorDroneId) return;

        Debug.LogWarning($"[AnchorSwitch] Switching anchor from {anchorDroneId} to {newAnchorId}");

        // We must re-anchor the world so visual positions do not jump.
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

        // Solve: newWorldCorrection * newAnchorSlamPose = oldAnchorWorldPose
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
                Vector3 vRel = vB - vA;

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
                }
            }
        }
    }

    // --- ---


}