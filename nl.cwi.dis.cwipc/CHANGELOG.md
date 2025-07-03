# Changelog

## [7.6.12] - 2025-07-03

- Underlying cwipc has some fixes (in v7.6.12) that were needed here.
- Various fixes needed by VR2Gather LiveTileSelector (#50)
- Minor fixes.

## [7.6.8] - 2025-07-01

- Allow tiled point clouds to be rendered at th same point size (#47)

## [7.6.7] - 2025-05-30

- Fixed off-by-one error for tile numbering (#44)

## [7.6.3] - 2025-02-21

- Fixed failure to run on Android (#42)

## [7.6.2] - 2025-02-17

- Closed some memory leaks, especially with uncompressed TCP transport (#41)

## [7.6.1] - 2025-01-21

- When building a Windows player including this package copy the cwipc DLLs into the player (#39)

## [7.6.0] - 2025-01-19

- New version of native cwipc package

## [7.5.7] - 2024-11-05

- Include Quest native libraries (from git lfs) in zip distribution (#37)

## [7.5.6] - 2024-10-30

- Compatible with Meta Quest (#21)
- Added point cloud streaming prefab and sample scene (#36)

## [7.5.5] - 2024-07-02

- Better exception messages when cwipc is not installed correctly (#35)
- Allow compressed point cloud streams to be used for prerecoded reader (#20)

## [7.5.4] - 2024-05-28

- Fixed PrerecordedPointCloudReader issue (#33)
- TCP protocol constructor signatures added (#32)

## [7.5.3] - 2024-04-23

- Removed WetRTC files that should not have been there and are unused.

## [7.5.1] - 2024-03-28

- First official release.
- Version numbers now match cwipc version numbering scheme.

## [0.21.0] - 2024-02-02

- More self-calibration fixes.
- Added get_centroid() method to cwipc.pointcloud.

## [0.20.0] - 2023-11-04

- Self-calibration fixes

## [0.19.0] - 2023-10-30

- Self-calibration fix for use without HMD 

## [0.18.0] - 2023-10-27

- Self-calibration (aligning HMD viewpoint to captured point cloud) improved 

## [0.17.0] - 2023-08-29

- Not being able to load capturers (for the generic capturer) isn't a fatal error.

## [0.16.0] - 2023-08-08

- Added support for cwipc_capturer (which uses whatever capturer is specified in cameraconfig.json)

## [0.15.0] - 2023-08-06

- Added support for reading pointclouds from a remote camera (using cwipc_forward on the camera machine)

## [0.14.0] - 2023-08-04

- Load native dynamic libraries from /opt/homebrew on Silicon Macs.

## [0.13.0] - 2023-07-27

-Native support for the Oculus Quest added.


## [0.12.0] - 2023-03-29

- ViewAdjust has moved from Samples/VR to Runtime and adjusts self-pointcloud-playback.
- Samples renamed.

## [0.11.0] - 2023-02-26

- Added cwipc\_playback prefab and PointCloudPlayback script to allow more control over prerecorded playback.
- Rendering point clouds on Mac should be improved.

## [0.10.0] - 2022-12-28

- Upped minimum Unity version to 2021.3. Turns out we are dependent on something too modern for 2019.3

## [0.9.11] - 2022-12-22

- FrameInfo renamed to FrameMetadata, contains prerecorded reader filenames, accessible from various places.

## [0.9.10] - 2022-12-21

- Allow switching PrerecordedPointCloudReader source by setting new value in dirName attribute

## [0.9.9] - 2022-12-13

- Fixed prerecorded reader, allow setting default pointsize (for ply files).

## [0.9.8] - 2022-12-12

- Changed Pointcloud material values of pointfactorsize and cutoff values to prevent pointcloud to disappear in the distance

## [0.9.7] - 2022-12-10

- Added methods to allow access to individual points in a cloud, and creating a cloud from a list of points. Plus example code.

## [0.9.6] - 2022-12-08

- Added support for mirroring Z (in stead of X) in renderer.
- Added support to adjust pointSizeFactor in renderer.

## [0.9.5] - 2022-12-08

- Added support for prerecorded pointcloud playback.

## [0.9.4] - 2022-11-30

- Added support for OpenXR.

## [0.9.3] - 2022-11-24

- Added kinect skeleton support

## [0.9.0] - 2022-11-23

- First public release