/// <summary>
/// A contract for any class that can provide drone pose data.
/// This allows us to abstract the source of the data (e.g., synthetic, UDP, ROS).
/// </summary>
public interface IPoseProvider
{
    // An event that fires whenever a new pose data packet is available.
    // Other scripts can "subscribe" to this to get updates.
    event System.Action<PoseData> OnPoseReceived;

    // A method to initialize and start the provider (e.g., open a network port).
    void StartProvider();

    // A method to clean up and stop the provider (e.g., close the network port).
    void StopProvider();
}