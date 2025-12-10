using UnityEngine;

// This class implements our IDroneController interface. Its job is to apply
// incoming pose data to the Transform of the GameObject it's attached to.
public class DroneController : MonoBehaviour, IDroneController
{
    [Header("Drone Identity")]
    [Tooltip("The unique ID for this drone. Must match the ID from its Pose Provider.")]
    [SerializeField] private int droneId = 0;

    [Header("Smoothing Settings")]
    [Tooltip("How smoothly the drone moves to its target position. Smaller values are slower and smoother, larger values are faster and snappier.")]
    [Range(0.01f, 1f)]
    public float smoothingFactor = 0.1f;

    // These private variables will store the latest pose data we've received.
    // We store it so we can smoothly interpolate towards it in the Update() loop.
    private Vector3 _targetPosition;
    private Quaternion _targetRotation;
    private bool _hasReceivedData = false;

    #region IDroneController Implementation

    // This is the public property from our interface.
    public int DroneId => droneId; // This is a shorthand way of writing { get { return droneId; } }

    /// <summary>
    /// This is the main method from our interface. The SLAMSystemManager will call this
    /// every time a new pose packet for this drone arrives.
    /// </summary>
    public void UpdatePose(PoseData pose)
    {
        // We simply store the incoming data. The actual movement happens in Update().
        _targetPosition = pose.Position;
        _targetRotation = pose.Rotation;

        // If this is the very first packet, we should snap directly to the position
        // instead of smoothing from the origin (0,0,0).
        if (!_hasReceivedData)
        {
            transform.position = _targetPosition;
            transform.rotation = _targetRotation;
            _hasReceivedData = true;
        }
    }

    // This is the other method from our interface.
    public Transform GetTransform()
    {
        return this.transform;
    }

    #endregion

    /// <summary>
    /// Update is called once per frame. We use it for the visual smoothing.
    /// </summary>
    private void Update()
    {
        // If we haven't received any data yet, there's nothing to do.
        if (!_hasReceivedData)
        {
            return;
        }

        // --- The Smoothing Logic ---
        // Lerp (Linear Interpolation) moves a value part of the way towards a target.
        // By doing this every frame, we create a smooth, eased movement.
        // The smoothingFactor determines "how much" of the remaining distance we cover each frame.

        // Smoothly move the drone's actual position towards the target position.
        transform.position = Vector3.Lerp(transform.position, _targetPosition, smoothingFactor);

        // Smoothly rotate the drone's actual rotation towards the target rotation.
        transform.rotation = Quaternion.Slerp(transform.rotation, _targetRotation, smoothingFactor);
    }
}