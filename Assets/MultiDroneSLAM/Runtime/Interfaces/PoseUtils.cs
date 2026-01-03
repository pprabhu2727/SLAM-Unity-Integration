using UnityEngine;

/*
 * Defines some math helpers for other files
 */
public static class PoseUtils
{
    //Interpolates between two poses
    public static Pose Lerp(Pose a, Pose b, float t)
    {
        //Uses Linear and Spherical interpolation for position and rotation
        return new Pose
        {
            position = Vector3.Lerp(a.position, b.position, t),
            rotation = Quaternion.Slerp(a.rotation, b.rotation, t),
        };
    }

    //Used for debugging
    public static string ToStringF3(Pose p)
    {
        return $"Pos={p.position.ToString("F3")} Rot={p.rotation.eulerAngles.ToString("F1")}";
    }
}
