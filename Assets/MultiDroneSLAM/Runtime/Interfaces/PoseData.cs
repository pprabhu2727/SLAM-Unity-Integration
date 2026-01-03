using UnityEngine;

/// Structure representing the state of a drone at a given moment. Just contains metadata.
public struct PoseData
{
    // The unique ID
    public int DroneId;

    // Pose Timestamp
    public double Timestamp;

    // 3D position (x, y, z)
    public Vector3 Position;

    // 3D orientation
    public Quaternion Rotation;

    // Confidence in its current values
    // (0 = Lost, 1 = Low Confidence, 2 = High Confidence)
    public int TrackingConfidence;
}