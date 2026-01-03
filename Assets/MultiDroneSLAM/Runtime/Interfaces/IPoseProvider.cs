public interface IPoseProvider
{
    int DroneId { get; }

    // Fires whenever a new pose data packet is available
    event System.Action<PoseData> OnPoseReceived;

    // Initialize
    void StartProvider();

    // Clean up
    void StopProvider();
}