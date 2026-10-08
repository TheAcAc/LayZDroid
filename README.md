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

## Quick start

**You need:** 64-bit Windows 10/11, at least 6 GB free disk space, an internet connection. Download LayZDroid and the separate KaW game files below. You will sign in to your own game account.

1. **Download both files.** Get [LayZDroid.exe](https://github.com/TheAcAc/LayZDroid/releases/download/v0.1.0-preview.1/LayZDroid.exe) and the [KaW game files](https://github.com/TheAcAc/LayZDroid/releases/download/v0.1.0-preview.1/KaW-362-APKs.zip). Right-click the KaW ZIP and choose **Extract All**. Then double-click `LayZDroid.exe` to open it. No separate .NET installation is needed.
2. **Install the Android runtime once.** Click **Set up runtime**, read the terms, then accept them to enable **Download runtime**. Wait for setup to finish; it downloads about 1.2 GB.
3. **Create and start an emulator.** Click **Add instance**, select its row in the list, then click **Start selected**. Wait for Android to finish starting. Begin with one instance and the default settings.
4. **Install KaW.** With the running instance selected, click **Import APK**. Open the KaW folder you extracted in step 1 and select **all three APK files together**: `base.apk`, `split_config.en.apk` and `split_config.hdpi.apk`. Hold **Ctrl** while clicking each file, then click **Open** and wait for installation to finish.
5. **Open the game.** Click **Open KaW** and sign in inside the emulator.
6. **Connect LayZ.** Close LayZ if it is already running. Click **Open LayZ** and select your installed LayZ `.exe`; LayZDroid supplies the connection settings automatically.

**If startup asks for virtualization:** enable CPU virtualization in your PC's BIOS/UEFI and **Windows Hypervisor Platform** in Windows Features, then restart Windows and try **Start selected** again.

**Prefer another drive?** Before runtime setup, click **Choose data folder**, choose a folder, then close and reopen LayZDroid. Existing instances stay in their original folder.

**When finished:** click **Stop selected** to shut down an emulator. Closing LayZDroid alone leaves running instances open. To share test results, click **Export test report** and mention how many instances you ran, whether KaW was responsive and any error messages.

Use one manager to control each instance. Shared automatic recovery with LayZ is still being developed.

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

First-run setup acquires Android Emulator 37.2.12, Platform Tools 37.0.1 and AOSP API 34 x86_64 image revision 4 from Google's official Android repository. Package sizes and upstream checksums are pinned in RuntimeSetup.cs. Upstream license terms are displayed before download, and downloaded packages retain their notices. Runtime binaries are not redistributed in the LayZDroid EXE/repository. KaW is a separate release download; its three APK files are unmodified and are not included in the LayZDroid EXE or source tree.

Third-party .NET notices are retained with the release. A future source-built runtime needs its own source/license artifacts before redistribution.

## MIT licence

LayZDroid's own code is [MIT licensed](LICENSE): you can use, modify and share it, including commercially, provided you keep the copyright and licence notice. It is supplied without warranty. Downloaded Android components retain their own licences and terms. KaW belongs to A Thinking Ape and is not covered by LayZDroid's MIT licence; its [game terms](https://www.kingdomsatwar.com/faq/tos.html) apply.
