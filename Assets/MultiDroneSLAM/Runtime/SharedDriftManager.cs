using UnityEngine;

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
