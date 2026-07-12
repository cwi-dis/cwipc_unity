# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repo is

This repo produces `nl.cwi.dis.cwipc`, a Unity package that is the C#/Unity bridge to the native
[`cwipc`](https://github.com/cwi-dis/cwipc) point-cloud library (C/C++, P/Invoke). It handles capture from
depth cameras (RealSense, Azure Kinect), reading/writing point clouds to disk, a synthetic point cloud
generator, compression (the MPEG Anchor codec, "cwi1") and TCP-based network transmission/reception,
and Unity-side rendering.

It depends on native `cwipc` binaries being installed on the machine (desktop) or bundled as `.so` files
(Android). The higher-level social-XR package [`VR2Gather`](https://github.com/cwi-dis/VR2Gather) depends
on this package for its point-cloud pipelines and renderers.

## Repo layout

- `nl.cwi.dis.cwipc/` — the actual Unity package. This is what gets published/consumed via UPM. Everything
  under here (`Runtime/`, `Editor/`, `Samples~/`, `Documentation~/`) is the deliverable.
- `cwipc_unity_develop/` — a throwaway Unity project (6000.3.x / Unity 6) used only to edit and test the
  package. It imports `nl.cwi.dis.cwipc` via a **symlink** in `cwipc_unity_develop/Packages/`, so edits made
  in the Unity Editor land directly in `nl.cwi.dis.cwipc/` and are what you commit. Nothing meaningful lives
  permanently in this project outside of that symlink and test scenes/samples.
  - Windows: symlinks must be enabled (Developer Mode) and `git config core.symlinks true` set, or the
    package import silently breaks.
- No CI, no automated test suite in this repo. "Testing" a change means opening `cwipc_unity_develop` in the
  Unity Editor and running one of the sample scenes.
- Some prerecorded-playback samples expect a sibling checkout of the (private) `cwipc-test` repo — see
  `readme.md` for details.

## Architecture

### Native bridge

`Runtime/Plugins/cwipc_util_pinvoke.cs` is the low-level P/Invoke layer (`class Cwipc.cwipc`) wrapping the
native `cwipc_util` dynamic library. It is effectively generated/mirrors the native C API 1:1 — check the
native `cwipc` repo's headers when in doubt about semantics, don't infer them from the C# alone. On
macOS the DLL name resolves to `__Internal` (statically linked); elsewhere it's `cwipc_util` loaded
dynamically. `CwipcConfig` (`Runtime/Scripts/CwipcConfig.cs`) is a serializable settings singleton
(log level, codec, queue sizes, native-load-path override) set via the `CwipcSettings` MonoBehaviour;
most runtime-tunable behavior flows through this rather than scattered fields.

### Pipeline model

A "pipeline" (`PointCloudPipelineSimple`, `PointCloudPipelineTiled`, and the `*SelfPipeline*` variants that
add transmission) wires together, per `SourceType` (Synthetic / Camera / Prerecorded / Networked / TCP /
WebRTC):

1. **Source** — `AbstractPointCloudSource` subclasses (`SyntheticPointCloudReader`, camera readers,
   `PrerecordedPointCloudReader`, `NetworkPointCloudReader`) produce `cwipc.pointcloud` frames on Unity's
   main thread (`Update()`), with optional voxelization/filtering.
2. **Encode/decode** — `AbstractPointCloudEncoder`/`AbstractPointCloudDecoder` (concrete: `AsyncPCEncoder`/
   `AsyncPCDecoder` for the cwi1 codec, `AsyncPCNullEncoder`/`AsyncPCNullDecoder` for uncompressed) run on
   background threads.
3. **Transmit/receive** — `AbstractPointCloudSink` + `AsyncWriter`/`AsyncReader` implementations
   (`AsyncTCPWriter`/`AsyncTCPReader`) move frames over the network.
4. **Prepare/render** — `AsyncPointCloudPreparer` stages a frame for the GPU; `PointCloudRenderer` +
   `PointCloud.shader` draw it via a `ComputeBuffer`.

Tiling (`PointCloudPipelineTiled`, `PointCloudTileDescription`, `StreamSupport.cs`) splits a capture into
multiple camera-direction tiles, each independently encodable at multiple quality levels, so a receiver can
select only visible tiles/qualities — this is the mechanism for bandwidth/GPU scaling in multi-camera setups.
`StreamSupport` has the pure data-conversion helpers between the tile/quality/stream description structs
used when building up outgoing vs. incoming stream sets.

### Threading

Everything off the Unity main thread that isn't a plain reader (encoders, decoders, network I/O) derives
from `AsyncWorker` (`Runtime/Scripts/Support/AsyncWorker.cs`): a self-managed `System.Threading.Thread`
polling an overridable `AsyncUpdate()` on a fixed interval, started/stopped via `Start()`/`StopAndWait()`.
`AsyncReader`/`AsyncWriter`/`AsyncFilter` (`Support/`) specialize this for the read/write/transform roles.
Workers are decoupled via `QueueThreadSafe` (`Support/QueueThreadSafe.cs`), not direct references — when
wiring a new pipeline stage, connect it through a queue rather than calling into another worker directly.

### Prefabs vs. scripts

The package ships prefabs (`Runtime/Prefabs/`) as the intended integration point: `cwipc_display` (render
only), `cwipc_avatar_self_simple`/`cwipc_avatar_other_simple` (simple untiled two-user session),
`cwipc_avatar_self`/`cwipc_avatar_other` (tiled). Samples show how a session controller drives these. If you
change a pipeline script's public fields, check whether the prefabs' serialized values need updating too.

## Working conventions

- **Prefer Inspector/prefab wiring over code wiring** for MonoBehaviour connections — this repo leans
  heavily on serialized `[SerializeField]` fields and prefabs rather than runtime `Awake()`/`Start()` wiring;
  match that style in new pipeline code.
- Release process (see `readme.md`): bump `nl.cwi.dis.cwipc/package.json` version and add a
  `nl.cwi.dis.cwipc/CHANGELOG.md` entry. Android native plugin updates are manual: download the release
  tarball from `cwipc`'s GitHub releases, unpack, copy `.so` files into
  `Runtime/Plugins/android-arm64`, and re-verify plugin meta-info in the Unity Editor.
- `CopyCwipcDLLs.cs` (Editor-only Windows post-build step) is a known-incomplete stopgap (logs a warning on
  every build) — don't treat its behavior as authoritative for other platforms; Linux/macOS builds require
  the user to have installed `cwipc` via apt/brew.

## For CWI-DIS group members

Within the CWI DIS group, "VR2Gather" is also used as an umbrella name for the whole family of related repos
(this one, `cwipc`, `VR2Gather`, transport repos, experience apps, etc.), not just the `VR2Gather` Unity
package. As of 2026-07, the convention is to check out all of these repos as siblings under a directory
customarily named `VRTogether/`. That directory is itself a git repo,
[`cwi-dis/VR2Gather-group`](https://github.com/cwi-dis/VR2Gather-group), which is **private** and holds a
group-wide `readme.md` and `CLAUDE.md` (repo map, team ownership, cross-repo dependencies) at its root —
this file inherits some of that context when working inside such a checkout.

This convention is very new and there is no public overview page yet, so don't assume a random clone of
`cwipc_unity` sits inside such a checkout, and don't reference `VR2Gather-group` paths as if they generally
exist — it's only relevant/accessible to CWI-DIS members who have that private repo checked out.
