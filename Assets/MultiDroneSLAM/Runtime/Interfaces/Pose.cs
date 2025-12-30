using UnityEngine;

// A simple struct to hold position and rotation for cleaner math.
public struct Pose
{
    public Vector3 position;
    public Quaternion rotation;

    public static Pose identity => new Pose { position = Vector3.zero, rotation = Quaternion.identity };

    public static Pose operator *(Pose a, Pose b)
    {
        return new Pose
        {
            rotation = a.rotation * b.rotation,
            position = a.position + (a.rotation * b.position)
        };
    }

    // A helper method to calculate the inverse of a pose.
    public Pose Inverse()
    {
        var invRotation = Quaternion.Inverse(rotation);
        return new Pose
        {
            rotation = invRotation,
            position = invRotation * -position
        };
    }
}