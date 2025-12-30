using UnityEngine;

public static class PoseUtils
{
    public static Pose Lerp(Pose a, Pose b, float t)
    {
        return new Pose
        {
            position = Vector3.Lerp(a.position, b.position, t),
            rotation = Quaternion.Slerp(a.rotation, b.rotation, t),
        };
    }

    public static string ToStringF3(Pose p)
    {
        return $"Pos={p.position.ToString("F3")} Rot={p.rotation.eulerAngles.ToString("F1")}";
    }
}
