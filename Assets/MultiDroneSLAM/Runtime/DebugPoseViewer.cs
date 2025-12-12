using UnityEngine;

public class DebugPoseViewer : MonoBehaviour
{
    // A public method so any other script can tell this component to log a message.
    // We will print the frame count to see WHEN the log happens.
    public void LogInfo(string message)
    {
        // We will filter by the drone's name to make the console easier to read.
        Debug.Log($"[Frame {Time.frameCount}] ({this.gameObject.name}): {message}");
    }
}