using UnityEngine;
/*
 * Debugging tool
 */
public class DebugPoseViewer : MonoBehaviour
{
    public void LogInfo(string message)
    {
        Debug.Log($"[Frame {Time.frameCount}] ({this.gameObject.name}): {message}");
    }
}