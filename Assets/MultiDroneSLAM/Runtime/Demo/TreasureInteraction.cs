using UnityEngine;

/*
 * A basic file that prints a console message whenever a drone collides with the treasure object
 * Not an important file but useful for testing that collision detection is functional
 */
public class TreasureInteraction : MonoBehaviour
{

    private void OnTriggerEnter(Collider other)
    {
        // Check if the object that hit us is actually a drone
        IDroneController drone = other.GetComponent<IDroneController>();

        if (drone != null)
        {
            Debug.Log($"<color=yellow>TREASURE COLLECTED! Drone with ID {drone.DroneId} has passed through!</color>");
        }
    }
}