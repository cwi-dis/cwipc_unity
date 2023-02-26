# Changelog

## [0.11.0] - 2023-02-26

Added cwipc_playback prefab and PointCloudPlayback_script to allow more control over prerecorded playback.
Rendering point clouds on Mac should be improved.

## [0.10.0] - 2022-12-28

Upped minimum Unity version to 2021.3. Turns out we are dependent on something too modern for 2019.3

## [0.9.11] - 2022-12-22

FrameInfo renamed to FrameMetadata, contains prerecorded reader filenames, accessible from various places.

## [0.9.10] - 2022-12-21

Allow switching PrerecordedPointCloudReader source by setting new value in dirName attribute

## [0.9.9] - 2022-12-13

Fixed prerecorded reader, allow setting default pointsize (for ply files).

## [0.9.8] - 2022-12-12

Changed Pointcloud material values of pointfactorsize and cutoff values to prevent pointcloud to disappear in the distance

## [0.9.7] - 2022-12-10

Added methods to allow access to individual points in a cloud, and creating a cloud from a list of points. Plus example code.

## [0.9.6] - 2022-12-08

Added support for mirroring Z (in stead of X) in renderer.
Added support to adjust pointSizeFactor in renderer.

## [0.9.5] - 2022-12-08

Added support for prerecorded pointcloud playback.

## [0.9.4] - 2022-11-30

Added support for OpenXR.

## [0.9.3] - 2022-11-24

Added kinect skeleton support

## [0.9.0] - 2022-11-23

First public release