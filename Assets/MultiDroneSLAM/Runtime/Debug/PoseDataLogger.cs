using UnityEngine;
/*
 * This class is no longer really needed. Other Loggers contain more specific and useful information.
 */
public class PoseDataLogger : MonoBehaviour
{
    private IPoseProvider _poseProvider;

    private void OnEnable()
    {
        _poseProvider = GetComponent<IPoseProvider>();
        if (_poseProvider != null)
        {
            _poseProvider.OnPoseReceived += LogPose;
        }
        else
        {
            Debug.LogError("PoseDataLogger could not find an IPoseProvider on this GameObject.");
        }
    }

    private void OnDisable()
    {
        if (_poseProvider != null)
        {
            _poseProvider.OnPoseReceived -= LogPose;
        }
    }

    private void LogPose(PoseData pose)
    {
        Debug.Log($"Received Pose! ID: {pose.DroneId}, Pos: {pose.Position.ToString("F3")}");
    }
}