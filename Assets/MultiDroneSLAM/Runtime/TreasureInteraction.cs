using UnityEngine;

public class TreasureInteraction : MonoBehaviour
{
    // This Unity function is called automatically when another collider enters
    // the trigger collider attached to this GameObject.
    private void OnTriggerEnter(Collider other)
    {
        // Check if the object that hit us is actually a drone.
        IDroneController drone = other.GetComponent<IDroneController>();

        if (drone != null)
        {
            Debug.Log($"<color=yellow>TREASURE COLLECTED! Drone with ID {drone.DroneId} has passed through!</color>");
        }
    }
}