using UnityEngine;

/// <summary>
/// A contract for any class that represents and controls a drone in the Unity scene.
/// </summary>
public interface IDroneController
{
    // The unique ID of the drone this controller manages.
    int DroneId { get; }

    // The primary method for the system to push new pose data to the drone.
    void UpdatePose(PoseData pose);

    // A helper method to get the drone's Transform component.
    Transform GetTransform();
}