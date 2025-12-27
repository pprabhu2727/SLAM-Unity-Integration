
# RealSense Integration Guide
Once SLAM is up and running on the physical realsense cameras. This ReadMe will help give you some high level guidance on integrating it with the existing project.

The current pipe line is the following:
 
[Pose Source (implements IPoseProvider) + UDPPoseBroadcast] → UDPPoseReceiver → SLAMSystemManager 

Currently the pose source is "faked" with SyntheticPoseProvider, but this will be replaced by the pose source from the physical RealSense

## Recommended Approach
External SLAM → RealSensePoseReceiver → ExternalSLAMPoseProvider → SLAMSystemManager

## Expected Pose Format

- Position: meters (Unity world units)
- Rotation: quaternion
- Timestamp: double (seconds)
- TrackingConfidence:
  - 2 = good
  - 1 = degraded
  - 0 = lost

## Coordinate Frames
Unity and RealSense  use right-handed coordinate systems:

- X: right
- Y: up
- Z: forward

## Timing Assumptions

- Timestamps should be increasing
- SLAMSystemManager tolerates jitter and packet loss

## Future Steps

1. Disable SyntheticPoseProvider
2. Attach ExternalSLAMPoseProvider
3. Attach RealSensePoseReceiver
4. Forward pose data via InjectExternalPose()

No changes to SLAMSystemManager required.
