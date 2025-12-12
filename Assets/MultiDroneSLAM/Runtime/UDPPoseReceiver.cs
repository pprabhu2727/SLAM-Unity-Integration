using UnityEngine;
using System.Collections.Concurrent; // Needed for the thread-safe queue
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class UDPPoseReceiver : MonoBehaviour, IPoseProvider
{
    // IPoseProvider implementation
    public event System.Action<PoseData> OnPoseReceived;
    
    public int DroneId => droneId;

    [Header("Drone Identity")]
    [Tooltip("The ID of the drone this receiver should listen for and provide data for.")]
    [SerializeField] private int droneId = 0;

    [Header("Network Settings")]
    [Tooltip("The port to listen on for incoming UDP packets.")]
    [SerializeField] private int listenPort = 5005;

    // --- Threading and Networking Members ---
    private UdpClient _udpClient;
    private Thread _receiveThread;
    private volatile bool _isRunning = false;

    // --- Main Thread Dispatching ---
    // We can't call Unity API from another thread. So, the network thread will add
    // received data to this thread-safe queue, and the main Unity thread will
    // process it in its Update() loop.
    private readonly ConcurrentQueue<PoseData> _poseQueue = new ConcurrentQueue<PoseData>();

    // Called when the script is enabled
    void OnEnable()
    {
        StartProvider();
    }

    // Called when the script is disabled or destroyed
    void OnDisable()
    {
        StopProvider();
    }

    public void StartProvider()
    {
        if (_isRunning) return; // Already running

        Debug.Log($"Starting UDP receiver on port {listenPort}...");
        try
        {
            _udpClient = new UdpClient(listenPort);
            _isRunning = true;
            _receiveThread = new Thread(ListenForData);
            _receiveThread.IsBackground = true; 
            _receiveThread.Start();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to start UDP receiver: {e.Message}");
        }
    }

    public void StopProvider()
    {
        if (!_isRunning) return; // Already stopped

        _isRunning = false;

        // Safely shut down the thread
        if (_receiveThread != null && _receiveThread.IsAlive)
        {
            _receiveThread.Join(500); // Wait up to 500ms for the thread to finish
        }

        // Close the UDP client
        if (_udpClient != null)
        {
            _udpClient.Close();
            _udpClient = null;
        }

        Debug.Log("UDP receiver stopped.");
    }

    private void ListenForData()
    {
        IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);

        while (_isRunning)
        {
            try
            {
                // This is a "blocking" call - the thread will wait here until a packet is received.
                byte[] data = _udpClient.Receive(ref anyIP);

                // Convert the byte array to a string.
                string json = Encoding.UTF8.GetString(data);

                // Deserialize the JSON string into our PoseData struct.
                PoseData receivedPose = JsonUtility.FromJson<PoseData>(json);

                // Add the received pose to the queue for the main thread to process.
                _poseQueue.Enqueue(receivedPose);
            }
            catch (SocketException)
            {
                if (_isRunning) Debug.LogError("SocketException occurred while receiving UDP data.");
            }
            catch (System.Exception e)
            {
                if (_isRunning) Debug.LogError($"Error receiving UDP data: {e.Message}");
            }
        }
    }

    // Update is called once per frame on the main Unity thread.
    void Update()
    {
        // Dequeue and process any poses that the listening thread has received.
        while (_poseQueue.TryDequeue(out PoseData pose))
        {
            // Now that we are on the main thread, it is safe to invoke the event.
            OnPoseReceived?.Invoke(pose);
        }
    }
}