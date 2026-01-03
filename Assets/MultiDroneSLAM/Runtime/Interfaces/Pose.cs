using UnityEngine;

/* 
 * Struct to hold position and rotation for cleaner math later on
 */
public struct Pose
{
    public Vector3 position;
    public Quaternion rotation;

    //Identity transform. Used for world correction and initialization. 
    public static Pose identity => new Pose { position = Vector3.zero, rotation = Quaternion.identity };

    //Using rigid body transform math. Takes pose b and expresses it world frame of a. 
    public static Pose operator *(Pose a, Pose b)
    {
        return new Pose
        {
            rotation = a.rotation * b.rotation,
            position = a.position + (a.rotation * b.position)
        };
    }

    // Calculate the inverse of a pose
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