/*
    t265_multi_udp_pose_sender.cpp

    Multi-camera Intel RealSense T265 UDP pose streamer.

    For each connected T265:
        DroneId = index (0,1,2,...)
        UDP Port = BASE_PORT + index

    Example:
        Camera 0 -> DroneId 0 -> 127.0.0.1:5005
        Camera 1 -> DroneId 1 -> 127.0.0.1:5006

    Unity JSON format:

    {
      "DroneId":0,
      "Timestamp":123.456,
      "Position":{"x":0,"y":0,"z":0},
      "Rotation":{"x":0,"y":0,"z":0,"w":1},
      "TrackingConfidence":2
    }

    Requirements:
      - librealsense2 installed
      - Link: realsense2.lib, Ws2_32.lib
      - realsense2.dll accessible
*/

#include <librealsense2/rs.hpp>

#include <winsock2.h>
#include <ws2tcpip.h>
#pragma comment(lib, "Ws2_32.lib")

#include <iostream>
#include <sstream>
#include <iomanip>
#include <string>
#include <thread>
#include <vector>
#include <chrono>

// ================= CONFIG =================

static const char* DEST_IP = "127.0.0.1";
static const int   BASE_PORT = 5005;     // Camera 0 -> 5005, Camera 1 -> 5006
static const int   SEND_RATE_HZ = 120;

// ==========================================


// ================= JSON FORMAT =================

static std::string make_json(
    int droneId,
    double timestamp,
    float px, float py, float pz,
    float qx, float qy, float qz, float qw,
    int tracker_conf)
{
    std::ostringstream o;
    o.setf(std::ios::fixed);
    o << std::setprecision(6);

    o << "{"
        << "\"DroneId\":" << droneId << ","
        << "\"Timestamp\":" << timestamp << ","

        << "\"Position\":{"
        << "\"x\":" << px << ","
        << "\"y\":" << py << ","
        << "\"z\":" << pz << "},"

        << "\"Rotation\":{"
        << "\"x\":" << qx << ","
        << "\"y\":" << qy << ","
        << "\"z\":" << qz << ","
        << "\"w\":" << qw << "},"

        << "\"TrackingConfidence\":" << tracker_conf
        << "}";

    return o.str();
}


// ================= CAMERA THREAD =================

void stream_camera(std::string serial, int droneId, int port)
{
    SOCKET sock = socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP);
    if (sock == INVALID_SOCKET)
    {
        std::cerr << "Socket creation failed for Drone "
            << droneId << "\n";
        return;
    }

    sockaddr_in dest{};
    dest.sin_family = AF_INET;
    dest.sin_port = htons(port);
    inet_pton(AF_INET, DEST_IP, &dest.sin_addr);

    std::cout << "Drone " << droneId
        << " | Serial: " << serial
        << " | Port: " << port << "\n";

    rs2::pipeline pipe;
    rs2::config cfg;

    cfg.enable_device(serial);
    cfg.enable_stream(RS2_STREAM_POSE);

    try
    {
        pipe.start(cfg);
    }
    catch (const rs2::error& e)
    {
        std::cerr << "Pipeline start failed (Drone "
            << droneId << "): "
            << e.what() << "\n";
        return;
    }

    const auto send_interval =
        std::chrono::milliseconds(1000 / SEND_RATE_HZ);

    int frame_counter = 0;

    while (true)
    {
        try
        {
            rs2::frameset frames = pipe.wait_for_frames();
            rs2::pose_frame pose_frame = frames.get_pose_frame();

            if (!pose_frame)
                continue;

            rs2_pose pose = pose_frame.get_pose_data();

            // ---- Coordinate Conversion (RealSense -> Unity) ----
            float px = pose.translation.x;
            float py = -pose.translation.y;
            float pz = -pose.translation.z;

            float qx = -pose.rotation.x;
            float qy = -pose.rotation.y;
            float qz = pose.rotation.z;
            float qw = pose.rotation.w;

            int confidence =
                static_cast<int>(pose.tracker_confidence);

            double timestamp =
                pose_frame.get_timestamp() * 0.001;

            std::string json =
                make_json(
                    droneId,
                    timestamp,
                    px, py, pz,
                    qx, qy, qz, qw,
                    confidence);

            sendto(sock,
                json.c_str(),
                static_cast<int>(json.size()),
                0,
                (sockaddr*)&dest,
                sizeof(dest));

            frame_counter++;

            if (frame_counter % 120 == 0)
            {
                std::cout << "Drone " << droneId
                    << " | x=" << px
                    << " y=" << py
                    << " z=" << pz
                    << " conf=" << confidence
                    << "\n";
            }

            std::this_thread::sleep_for(send_interval);
        }
        catch (const rs2::error& e)
        {
            std::cerr << "RealSense error (Drone "
                << droneId << "): "
                << e.what() << "\n";
            std::this_thread::sleep_for(
                std::chrono::seconds(1));
        }
    }

    closesocket(sock);
}


// ================= MAIN =================

int main()
{
    WSADATA wsa;
    if (WSAStartup(MAKEWORD(2, 2), &wsa) != 0)
    {
        std::cerr << "WSAStartup failed\n";
        return 1;
    }

    rs2::context ctx;
    auto devices = ctx.query_devices();

    if (devices.size() == 0)
    {
        std::cerr << "No RealSense devices found.\n";
        return 1;
    }

    std::vector<std::thread> threads;
    int droneId = 0;

    std::cout << "Detected devices:\n";

    for (auto dev : devices)
    {
        std::string name =
            dev.get_info(RS2_CAMERA_INFO_NAME);

        std::string serial =
            dev.get_info(RS2_CAMERA_INFO_SERIAL_NUMBER);

        std::cout << "  " << name
            << " | S/N: " << serial << "\n";

        if (name.find("T265") != std::string::npos)
        {
            int port = BASE_PORT + droneId;

            threads.emplace_back(
                stream_camera,
                serial,
                droneId,
                port);

            droneId++;
        }
    }

    if (threads.empty())
    {
        std::cerr << "No T265 devices detected.\n";
        return 1;
    }

    std::cout << "\nStarted "
        << threads.size()
        << " T265 stream(s).\n";

    for (auto& t : threads)
        t.join();

    WSACleanup();
    return 0;
}