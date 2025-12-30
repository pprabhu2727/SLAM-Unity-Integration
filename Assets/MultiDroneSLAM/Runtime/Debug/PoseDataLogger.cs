using UnityEngine;

public class PoseDataLogger : MonoBehaviour
{
    private IPoseProvider _poseProvider;

    // OnEnable is called when the component is activated.
    private void OnEnable()
    {
        // Find the pose provider on the same GameObject.
        _poseProvider = GetComponent<IPoseProvider>();
        if (_poseProvider != null)
        {
            // Subscribe our LogPose method to the provider's event.
            _poseProvider.OnPoseReceived += LogPose;
        }
        else
        {
            Debug.LogError("PoseDataLogger could not find an IPoseProvider on this GameObject.");
        }
    }

    // OnDisable is called when the component is deactivated.
    private void OnDisable()
    {
        if (_poseProvider != null)
        {
            _poseProvider.OnPoseReceived -= LogPose;
        }
    }

    // This method will be called every time the provider sends new data.
    private void LogPose(PoseData pose)
    {
        Debug.Log($"Received Pose! ID: {pose.DroneId}, Pos: {pose.Position.ToString("F3")}");
    }
}