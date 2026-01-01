using UnityEngine;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class UDPPoseReceiver : MonoBehaviour, IPoseProvider
{
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
    private readonly ConcurrentQueue<PoseData> _poseQueue = new ConcurrentQueue<PoseData>();

    void OnEnable()
    {
        StartProvider();
    }

    void OnDisable()
    {
        StopProvider();
    }

    public void StartProvider()
    {
        if (_isRunning) return; 

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
        if (!_isRunning) return; 

        _isRunning = false;

        if (_receiveThread != null && _receiveThread.IsAlive)
        {
            _receiveThread.Join(500); 
        }

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
                // Thread will wait here until a packet comes in
                byte[] data = _udpClient.Receive(ref anyIP);

                // Convert byte to string
                string json = Encoding.UTF8.GetString(data);

                // Convert into PoseData struct
                PoseData receivedPose = JsonUtility.FromJson<PoseData>(json);

                // Add pose to the queue for the main thread
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

    void Update()
    {
        // Invoke any poses that the listening thread received
        while (_poseQueue.TryDequeue(out PoseData pose))
        {
            OnPoseReceived?.Invoke(pose);
        }
    }
}