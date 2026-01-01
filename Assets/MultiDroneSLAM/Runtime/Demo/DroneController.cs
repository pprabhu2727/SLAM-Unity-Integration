using UnityEngine;

/*
 * Allows us to move our drone in the game
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

    #region IDroneController Implementation

    public int DroneId => droneId;

    public void UpdatePose(PoseData pose)
    {
        _targetPosition = pose.Position;
        _targetRotation = pose.Rotation;

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