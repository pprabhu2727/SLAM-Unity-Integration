using UnityEngine;

/// <summary>
/// A lightweight data structure representing the state of a drone at a specific moment in time.
/// This is the core data packet sent from a pose provider (real or simulated).
/// </summary>
public struct PoseData
{
    // The unique identifier for the drone that this data belongs to.
    public int DroneId;

    // The timestamp when the pose was captured, crucial for sequencing and latency checks.
    // Using a double for high precision.
    public double Timestamp;

    // The 3D position (x, y, z) as reported by the SLAM system.
    public Vector3 Position;

    // The 3D orientation as a quaternion, which is efficient and avoids gimbal lock.
    public Quaternion Rotation;

    // A value indicating the SLAM system's confidence in its current tracking.
    // (e.g., 0 = Lost, 1 = Low Confidence, 2 = High Confidence)
    public int TrackingConfidence;
}