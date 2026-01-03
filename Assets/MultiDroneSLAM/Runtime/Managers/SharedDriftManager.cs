using UnityEngine;

/* 
 * -----------------------------------------------------------------------------
 * SIMULATION-ONLY CODE
 * This section is not requried for real slam integration. 
 * 
 * Dealing with global drift here and not local drift of the drones. 
 * SLAM is already expected to have aligned two drones within the same coordinate system, but the cameras and shared coordinate system itself can be flawed with drift or bias.
 * This means the entire shared map can drift which is what this file simulates. 
 * Any drones aligned to this shared map share this same global drift that the shared SLAM map has. 
 * -----------------------------------------------------------------------------
 */
public class SharedDriftManager : MonoBehaviour
{
    [Header("Global Drift (World Frame)")]
    public Vector3 driftPerSecond = new Vector3(0.01f, 0.0f, 0.01f);

    public Vector3 CurrentDrift { get; private set; }

    void Start()
    {
        CurrentDrift = Vector3.zero;
        Debug.Log($"[SharedDrift] Initialized. driftPerSecond={driftPerSecond.ToString("F3")}");
    }

    void Update()
    {
        CurrentDrift += driftPerSecond * Time.deltaTime;

        if (Time.frameCount % 300 == 0)
        {
            Debug.Log($"[SharedDrift] Drift={CurrentDrift.ToString("F3")}");
        }
    }
}
