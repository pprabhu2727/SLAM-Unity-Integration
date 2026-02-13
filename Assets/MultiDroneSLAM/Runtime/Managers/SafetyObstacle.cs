using UnityEngine;

//This class is for applying to objects to trigger the drones safety mechanisms (ex. attach this script to walls)
public class SafetyObstacle : MonoBehaviour
{
    [Tooltip("Safety radius of this obstacle in meters")]
    public float safetyRadius = 0.75f;

    [Tooltip("If moving obstacle, additional work needs to be done to handle that")]
    public Vector3 velocity = Vector3.zero;

    public Vector3 Position => transform.position;

    public Vector3 Velocity => velocity;
}
