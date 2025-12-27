public interface IPoseProvider
{
    int DroneId { get; }

    // Fires whenever a new pose data packet is available
    event System.Action<PoseData> OnPoseReceived;

    // Initialize the provider (can do things like, open a network port)
    void StartProvider();

    // Clean up and stop the provider (can do things like, close the network port)
    void StopProvider();
}