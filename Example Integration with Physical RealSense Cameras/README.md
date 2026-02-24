# RealSense T265 Integration Setup
This guide and the contents of the folder can help you get set up to integrate the RealSense cameras into my Unity project. 

### Folder contents: 
- t265_udp_pose_sender
  - A Visual Studio 2022 project that contains the script for sending the pose to my Unity project.
- xrlabs_sf
  - RealSense files.

## Setup
First, download and save the contents of the folder to a location on your computer (you'll need the filepath later). You will also need Visual Studio 2022 installed, and my Unity project should be installed. 

### Step 1
Open the t265_udp_pose_sender project in Visual Studio 2022. Then open the Visual Studio Solution Explorer.

### Step 2
Right-click on the pose-sender file (the .cpp file) in the Solution Explorer and click on properties.
Navigate: C/C++ → General → Additional Include Directories

And add the following line:
```
C:\Users\quadc\Desktop\xrlabs_sf\librealsense\include
```

**Important**: The above line and all other lines in this guide are just examples. The actual filepath needs to be modified based on where you saved these folders on your machine. 

### Step 3
Right-click on the main project folder 't265_udp_pose_sender' in the Solution Explorer and click on properties.

At the very top of the window, make sure you have the settings:
```
Configuration: All Configurations
Platform: x64
``` 

Next, navigate: Linker → General → Additional Library Directories.
And add the following line:
```
C:\Users\quadc\Desktop\xrlabs_sf\librealsense\build\Release
```

Next, navigate: Linker → Input → Additional Dependencies.
And add the following line:
```
realsense2.lib
Ws2_32.lib
```

### Step 4
At this point, the project dependencies should be properly linked. Plug in the RealSense camera/s. Build the project. Then run the project, and a console should appear displaying the pose data output. 

Then run my Unity project, which contains the Receiver code. (You'll need to first make adjustments to the scene, such as disabling the "ground-truth" objects so that it won't generate the synthetic pose data, which will conflict with the actual pose data.) And moving the RealSense camera will move the drone within my Unity project. 