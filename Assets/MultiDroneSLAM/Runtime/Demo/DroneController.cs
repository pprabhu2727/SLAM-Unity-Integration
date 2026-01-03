using UnityEngine;

/*
 * This file lets the drone move within the game. However it is not controlled directly as GroundTruthController handles that direct movement.
 * The drones move based on the given pose data. 
 * (GroundTruthController lets you move the drones, the poses are generated and SLAM output is mimicked based on that movement, then this file makes the Unity game movements)
 */
public class DroneController : MonoBehaviour, IDroneController
{
    [Header("Drone Identity")]
    [Tooltip("The unique ID for this drone. Must match the ID from its Pose Provider.")]
    [SerializeField] private int droneId = 0;

    [Header("Smoothing Settings")]
    [Tooltip("How smoothly the drone moves to its target position. Smaller values are slower and smoother, larger values are faster and snappier.")]
    [Range(0.01f, 1f)]
    public float smoothingFactor = 0.1f;

    private Vector3 _targetPosition;
    private Quaternion _targetRotation;
    private bool _hasReceivedData = false;

    //Trying #region label out, can be removed
    #region IDroneController Implementation 

    public int DroneId => droneId;

    //Called by SLAMSystemManager
    public void UpdatePose(PoseData pose)
    {
        _targetPosition = pose.Position;
        _targetRotation = pose.Rotation;

        //Prevents the first-packet snap that could occur on startup
        if (!_hasReceivedData)
        {
            transform.position = _targetPosition;
            transform.rotation = _targetRotation;
            _hasReceivedData = true;
        }
    }

    public Transform GetTransform()
    {
        return this.transform;
    }

    #endregion

    //Used for visual smoothing
    private void Update()
    {
        if (!_hasReceivedData)
        {
            return;
        }

        // Using linear interpolation
        transform.position = Vector3.Lerp(transform.position, _targetPosition, smoothingFactor);

        transform.rotation = Quaternion.Slerp(transform.rotation, _targetRotation, smoothingFactor);
    }
}