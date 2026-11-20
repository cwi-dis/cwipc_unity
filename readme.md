# CWI Pointcloud Unity package

This repository, <https://github.com/cwi-dis/cwipc_unity>, contains a Unity package `nl.cwi.dis.cwipc` that allows capture and display of
point clouds and various operations such as compression for transmission, reading and writing to disk, etc.

It is part of the <https://github.com/cwi-dis/cwipc> cwipc pointcloud suite, and it depends on the native libraries from `cwipc` installed on your machine.

Checking out this repository is only useful if you want to **modify** the `cwipc_unity` package.
See the [README for nl.cwi.dis.cwipc](nl.cwi.dis.cwipc/README.md) for more details on **using** the cwipc package in your own Unity projects.
But TL;DR: You could clone <https://github.com/cwi-dis/cwipc_unity_sample> to get started.

If you want to **develop** on the package (i.e. modify it) you should check out this whole repository, and open one of the two development projects:

- `cwipc_unity_develop_urp`: uses the Universal Render Pipeline (URP). This is the normal case.
- `cwipc_unity_develop_birp`: uses the Built-in Render Pipeline (BIRP), without the URP package installed. This is for backward compatibility, and it checks that the package works in a project without URP.

Both projects include the `nl.cwi.dis.cwipc` package (and its samples) using symbolic links, so any changes you make in either of them land in the package and can be committed to git. Their own contents are otherwise the same: the XR Interaction Toolkit Starter Assets, the VR sample scenes in the build list, and the Unity Pipeline package (`com.unity.pipeline`) so the Editor can be inspected and driven from the command line (`unity command ...`).

Some of the samples in the development projects (specifically the prerecorded point cloud playback samples) use data from <https://github.com/cwi-dis/cwipc-test>, which should be cloned at the same directory level where `cwipc_unity` has been cloned. Or you can fix the pathnames or use different content.

> *Note*: At the moment `cwipc-test` is a private repository, due to excessive downloads of the content by AI agents and bots and such, and _we_ have to pay for those downloads. Please complain to github.
>
>  In the mean time contact us (for example through an issue) and we will add you as a collaborator on the repo.

### Note for Windows users

Before you check out this repository you should ensure that symbolic links are enabled on your Windows machine (they are not enabled by default) and that `git` knows this.

You do this by first enabling symlinks in _Settings, Privacy & Security_, _Developer Mode_ and then telling git about them, with

```
git config --global core.symlinks true
git config --local core.symlinks true
```

(the `--local` command may not be needed in all cases, don't worry if it fails).

### Tests and CI

The package has EditMode tests in `nl.cwi.dis.cwipc/Tests/Editor`. They show up in the development projects only (which list the package under `testables`): open `Window > General > Test Runner`, tab `EditMode`, and `Run All`. They need the native `cwipc` libraries installed.

CI (`.github/workflows/ci.yml`) runs the same tests against the released `cwipc`: in `cwipc_unity_develop_urp` on Linux and Windows (in game-ci's Docker containers, with `cwipc` in a custom image) and on macOS arm64 and Intel, and in `cwipc_unity_develop_birp` on macOS arm64 with a graphics device, so the shaders are really compiled there. The macOS jobs run on `master` and when the workflow is started by hand. A static check (`.github/scripts/check_urp_subshaders.py`) also verifies that every URP SubShader can be skipped in a project without URP.

### Switching between the VR samples in a build

The VR sample scenes each contain a small scene switcher (`cwipc_scene_switcher` prefab): point at one of its buttons with a controller (or click it with the mouse without a headset) to load another VR sample scene.

### Note for creating a new release

- Update `nl.cwi.dis.cwipc/CHANGELOG.md`
- Update `nl.cwi.dis.cwipc/package.json`

#### Android native plugins

The Android native plugins for `cwipc` are included in this package (unlike those for desktop). They need to be updated for a new release:

- Download `cwipc-android-arm64-package-vX.Y.Z.tar.gz` from the current release of <https://github.com/cwi-dis/cwipc/releases>
- Unpack the tarball.
- Copy the `.so` files from the `lib` folder into `./nl.cwi.dis.cwipc/Runtime/Plugins/android-arm64`
- Open the `cwipc_unity_develop_urp` project in the Unity Editor, navigate to `Plugins/android-arm64` and ensure that all the meta-information about the native plugins is correct.
- `git commit`