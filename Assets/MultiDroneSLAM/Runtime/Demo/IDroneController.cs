using UnityEngine;

/*
 * Interface for the drone controller
 */
public interface IDroneController
{
    // Unique ID
    int DroneId { get; }

    // Push new pose data to the drone
    void UpdatePose(PoseData pose);

    Transform GetTransform();
}