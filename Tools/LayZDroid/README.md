# LayZDroid

**Built for LayZ. No launcher ads. No bundled app store.**

A focused Windows alternative to a general-purpose BlueStacks setup. Create fresh Android instances, choose their resources and open KaW from one small native interface.

## What this preview improves

- A launcher focused on KaW and LayZ, without bundled promotions or an app-store shell.
- Conservative defaults: one CPU core, 480×854 portrait display, 30 Hz and 1024 MB requested guest RAM (experimental).
- Cameras, audio, GPS and unused sensors disabled in the instance configuration.
- Separate account storage with a shared Android base image.
- Staggered startup and checks for both free physical memory and Windows commit capacity.
- Clear instance controls, actual VM memory display and a test-report export.

**RAM/CPU savings against BlueStacks have not been measured yet.** This is a launcher/configuration preview using an upstream AOSP Android 14 image, not a finished custom stripped Android build. One instance on a 4 GB PC is the target; two remains a stretch target for testing.

## Run it

1. Download `LayZDroid.exe` from the preview release and run it on 64-bit Windows 10/11. No separate .NET installation is needed.
2. Choose **Set up runtime**, read the upstream terms and accept them if appropriate. Runtime packages download directly from Google (about 1.2 GB). Allow at least 6 GB free disk space. **Choose data folder** can select another drive; close and reopen afterward.
3. Enable CPU virtualization and Windows Hypervisor Platform if the prerequisite check asks; a Windows restart may be needed.
4. **Add instance**, then **Start selected**. Import your KaW base APK and all required split APKs together. APK files and signed-in accounts are not included.
5. Choose **Open KaW** and sign in. **Open LayZ** launches your installed LayZ EXE with the correct ADB settings; close an already-running LayZ first.
6. Try your workload and use **Export test report**. Include whether KaW is responsive, the number of instances and any failure messages when reporting results.

Closing the manager leaves ready emulator instances running. Use **Stop selected** to stop an instance. Do not use two managers or automatic recovery controllers to start/stop the same VM. Full shared lifecycle/recovery integration is not implemented in this preview.

## Preview boundaries

- No arbitrary ten-instance cap in LayZDroid. The pinned engine's declared console-port range provides 16 port pairs; occupied ports reduce that capacity. Actual simultaneous capacity depends on your hardware. The installed LayZ 1.5.2 bot still has its separate ten-worker limit; removing that is a subsequent integration change.
- 768/1024 MB settings are experimental. RAM is a guest allocation, not total host memory use. The engine's `-lowram` option is used; changed effective RAM is treated as a failure. Low-RAM smoothness remains unverified.
- No ARM translation layer, Google Play Store or Play Services. The local KaW split APKs examined require API 25 and have no native ABI dependency; different game versions must pass their own import checks.
- Port 5038 is reserved for this runtime's ADB. A foreign server is reported and preserved, not killed. BlueStacks' separate 5037 server is left alone.
- Startup/crash handling and atomic settings are included. Updates, unattended recovery and a source-built slim image remain future work.
- The EXE is currently unsigned. No security features are disabled automatically.

## Build and check

From this repository's source tree:

```powershell
dotnet run --project Checks/LayZDroid/Checks.csproj -c Release
dotnet run --project Checks/LayZDroidUi/Checks.csproj -c Release
dotnet publish Tools/LayZDroid/LayZDroid.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

Development UI checks render an unshown window and use temporary instance files; they do not start emulator instances. Optional live setup checks are separate.

## Runtime provenance

First-run setup acquires Android Emulator 37.2.12, Platform Tools 37.0.1 and AOSP API 34 x86_64 image revision 4 from Google's official Android repository. Package sizes and upstream checksums are pinned in RuntimeSetup.cs. Upstream license terms are displayed before download, and downloaded packages retain their notices. Runtime binaries and game APKs are not redistributed in the LayZDroid EXE/repository.

LayZDroid's own source is licensed under MIT. Third-party .NET notices are retained with the release. Upstream Android components have their own terms. A future source-built runtime needs its own source/license artifacts before redistribution.
