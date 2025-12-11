using UnityEngine;
using System.Collections.Generic;

public class SLAMSystemManager : MonoBehaviour
{
    // We create a dedicated class to hold the pairing of a provider and a controller.
    // The [System.Serializable] attribute is crucial; it makes this class show up
    // in the Unity Inspector so we can configure it.
    [System.Serializable]
    public class DronePair
    {
        public string name; // Just for organization in the Inspector
        public GameObject providerObject; // The GameObject with the IPoseProvider
        public GameObject controllerObject; // The GameObject with the IDroneController

        // We will store the actual interface references here after we find them.
        [HideInInspector] public IPoseProvider provider;
        [HideInInspector] public IDroneController controller;
    }

    [Header("Drone Configuration")]
    [Tooltip("The list of all drone provider/controller pairs in the scene.")]
    [SerializeField] private List<DronePair> dronePairs = new List<DronePair>();

    // Awake is called before the first frame update, even before Start().
    // It's the ideal place to set up references.
    void Awake()
    {
        InitializeSystem();
    }

    // OnDestroy is called when the object is destroyed.
    void OnDestroy()
    {
        ShutdownSystem();
    }

    private void InitializeSystem()
    {
        if (dronePairs.Count == 0)
        {
            Debug.LogWarning("SLAMSystemManager has no drone pairs configured.");
            return;
        }

        // Loop through each configured pair.
        foreach (var pair in dronePairs)
        {
            // Find the interfaces on the assigned GameObjects.
            pair.provider = pair.providerObject?.GetComponent<IPoseProvider>();
            pair.controller = pair.controllerObject?.GetComponent<IDroneController>();

            // --- Critical Connection Logic ---
            // If both the provider and controller are found...
            if (pair.provider != null && pair.controller != null)
            {
                // ...check if their IDs match. This is a crucial sanity check.
                if (pair.controller.DroneId == pair.provider.DroneId)
                {
                    // Subscribe the controller's UpdatePose method to the provider's event.
                    // Now, whenever the provider fires OnPoseReceived, the controller's
                    // UpdatePose method will automatically be called.
                    pair.provider.OnPoseReceived += pair.controller.UpdatePose;
                    Debug.Log($"Successfully linked Provider and Controller for Drone ID: {pair.controller.DroneId}");
                }
                else
                {
                    Debug.LogError($"ID mismatch for pair '{pair.name}'! " +
                                   $"Provider has ID {pair.provider.DroneId}, " +
                                   $"Controller expects ID {pair.controller.DroneId}.");
                }
            }
            else
            {
                Debug.LogError($"Failed to find Provider or Controller for pair '{pair.name}'. " +
                               "Check if the scripts are attached to the correct GameObjects.");
            }
        }
    }

    private void ShutdownSystem()
    {
        // Loop through all pairs and unsubscribe to prevent memory leaks. This is very important!
        foreach (var pair in dronePairs)
        {
            if (pair.provider != null && pair.controller != null)
            {
                pair.provider.OnPoseReceived -= pair.controller.UpdatePose;
            }
        }
        Debug.Log("SLAMSystemManager shut down and unsubscribed all events.");
    }
}