# CWI Pointcloud Unity package

This repository contains a Unity package `nl.cwi.dis.cwipc` that allows
capture and display of point clouds and various operations such as
compression for transmission, reading and writing to disk, etc. 

Intel Realsense and Azure Kinect RGBD cameras are supported, but you can
test your software without access to such hardware by using our
synthetic point cloud source (which generates an approximately human
sized point cloud).

Samples are provided to use live point clouds as your representation in
shared games or other activities: you can be **yourself** in stead of an
avatar!

These samples work best when used with a VR headset, obviously, but can
also be used with a normal screen.

This package is part of the <https://github.com/cwi-dis/cwipc> cwipc
pointcloud suite.

## Installation

- You must first install the `cwipc` libraries, utilities and
  dependencies onto your computer. Follow the instructions on
  <https://github.com/cwi-dis/cwipc>
  - When developing for the Oculus Quest you do _not_ have to install the
    cwipc libraries for the Quest: those are already included in this 
	package. But you probably want to install the libraries for your
	desktop platform, so you can test your app from within the Unity Editor.
- Create a new Unity project. You could clone <https://github.com/cwi-dis/cwipc_unity_sample>
  to get started.
- If you want to use the VR samples it is better to first enable the XR
  Loader, OpenXR and the OpenXR device profile for the device you are
  going to use. You should also install the _XR Interaction Toolkit_.
- In the Unity Package Manager, do _Add Package by URL_ and use the URL
  `git+https://github.com/cwi-dis/cwipc_unity?path=/nl.cwi.dis.cwipc`


## Samples
You should probably install the Simple Samples and (if you want to use
point clouds in VR) the VR Samples:

- Documentation for [Simple Samples](Samples~/Simple point cloud samples/readme.md)
- Documentation for [VR Samples](Samples~/VR point cloud extensions/readme.md)

## Render pipeline compatibility

`cwipc_unity` and its samples work unchanged under both Unity's Built-in Render Pipeline (BIRP)
and the Universal Render Pipeline (URP) - no separate package version or sample set needed. This
works because the shipped shaders (`PointCloudTextured.shader`, and `SimpleColor.shader` used by
placeholder/backdrop geometry in the samples) each contain a subshader for both pipelines; Unity
automatically picks the right one based on your project's active render pipeline.

If you have an existing BIRP project and want to make it (or parts of it) URP-compatible too,
`cwipc/SimpleColor` (`Runtime/Shaders/SimpleColor.shader`) may be a useful starting point for
simple, unlit, flat-or-textured materials (e.g. placeholder geometry) - it is not a general
replacement for `Standard`, since it has no lighting response and none of `Standard`'s PBR
features (normal maps, metallic/smoothness, emission, etc.).

> *Note*: if your project has no URP packages installed at all (not just inactive, but genuinely
> absent), the Unity Editor will log a shader compile error for `PointCloudTextured`'s URP
> subshaders (`Couldn't open include file '.../Core.hlsl'`), because Unity's shader importer
> compiles every subshader regardless of which render pipeline is active. If you hit this, add
> `com.unity.render-pipelines.universal` to your project (via Package Manager) even if you don't
> intend to use URP - that resolves the include and clears the error.

## Documentation

See [documentation.](Documentation~/nl.cwi.dis.cwipc.md)