using UnityEngine;
using System;

/* 
 * Placeholder provider for external SLAM systems.
 * This file can replace SyntheticPoseProvider
 */
public class ExternalSLAMPoseProvider : MonoBehaviour, IPoseProvider
{
    public int DroneId => droneId;

    [SerializeField] private int droneId = 0;

    public event Action<PoseData> OnPoseReceived;

    // Call this method when a pose is received. This format matches how SyntheticPoseProvider did it
    public void InjectExternalPose(
        Vector3 position,
        Quaternion rotation,
        double timestamp,
        int trackingConfidence)
    {
        PoseData pose = new PoseData
        {
            DroneId = droneId,
            Timestamp = timestamp,
            Position = position,
            Rotation = rotation,
            TrackingConfidence = trackingConfidence
        };

        OnPoseReceived?.Invoke(pose);
    }

    public void StartProvider()
    {
        
    }

    public void StopProvider()
    {
        
    }
}
