# CWI Pointcloud Unity package

This repository, <https://github.com/cwi-dis/cwipc_unity>, contains a Unity package `nl.cwi.dis.cwipc` that allows capture and display of
point clouds and various operations such as compression for transmission, reading and writing to disk, etc.

It is part of the <https://github.com/cwi-dis/cwipc> cwipc pointcloud suite, and it depends on the native libraries from `cwipc` installed on your machine.

Checking out this repository is only useful if you want to **modify** the `cwipc_unity` package.
See the [README for nl.cwi.dis.cwipc](nl.cwi.dis.cwipc/README.md) for more details on **using** the cwipc package in your own Unity projects.
But TL;DR: You could clone <https://github.com/cwi-dis/cwipc_unity_sample> to get started.

If you want to **develop** on the package (i.e. modify it) you should check out this whole repository, and open the `cwipc_unity_develop` Unity project. That project includes the `nl.cwi.dis.cwipc` package using symbolic links, so any changes you make to it can be committed to git.

Some of the samples in `cwipc_unity_develop` (specifically the prerecorded point cloud playback samples) use data from <https://github.com/cwi-dis/cwipc-test>, which should be cloned at the same directory level where `cwipc_unity` has been cloned. Or you can fix the pathnames or use different content.

### Note for Windows users

Before you check out this repository you should ensure that symbolic links are enabled on your Windows machine (they are not enabled by default) and that `git` knows this.

You do this by first enabling symlinks in _Settings, Privacy & Security_, _Developer Mode_ and then telling git about them, with

```
git config --global core.symlinks true
git config --local core.symlinks true
```

(the `--local` command may not be needed in all cases, don't worry if it fails).

### Note for creating a new release

The Android native plugins for `cwipc` are included in this package (unlike those for desktop). They need to be updated for a new release.

