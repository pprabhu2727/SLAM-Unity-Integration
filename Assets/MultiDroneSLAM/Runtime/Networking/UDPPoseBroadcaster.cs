using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;

public class UDPPoseBroadcaster : MonoBehaviour
{
    [Header("Network Settings")]
    [Tooltip("The IP address to send data to. '127.0.0.1' is the local machine.")]
    [SerializeField] private string remoteIpAddress = "127.0.0.1";

    [Tooltip("The port to send data to. Must match the receiver's listen port.")]
    [SerializeField] private int remotePort = 5005;

    private UdpClient _udpClient;
    private IPEndPoint _remoteEndPoint;

    void Start()
    {
        _udpClient = new UdpClient();
        _remoteEndPoint = new IPEndPoint(IPAddress.Parse(remoteIpAddress), remotePort);
        Debug.Log($"UDP Broadcaster configured to send to {remoteIpAddress}:{remotePort}");
    }

    void OnDestroy()
    {
        _udpClient?.Close();
    }


    /// Serializes a PoseData struct to JSON and broadcasts it over UDP.
    public void BroadcastPose(PoseData pose)
    {
        if (_udpClient == null) return;

        try
        {
            // Serialize the object to a JSON string.
            string json = JsonUtility.ToJson(pose);

            // Convert the string to a byte array.
            byte[] data = Encoding.UTF8.GetBytes(json);

            // Send the data.
            _udpClient.Send(data, data.Length, _remoteEndPoint);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error broadcasting UDP data: {e.Message}");
        }
    }
}