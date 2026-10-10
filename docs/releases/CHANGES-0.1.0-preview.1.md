# LayZDroid 0.1.0 Preview 1

- Standalone Windows EXE with fresh Android 14 instances and separate account storage.
- Experimental 768/1024 MB guest settings, configurable cores/display/graphics and staggered startup.
- No launcher ads or bundled app store; conservative camera/audio/sensor settings.
- KaW APK/split import, Open KaW and Open LayZ controls.
- Host memory admission, verified process ownership, setup checksums/cleanup and test-report export.

Run LayZDroid.exe, select Set up runtime, then Add instance. Runtime downloads directly from Google after displayed terms are accepted (about 1.2 GB; allow 6 GB free disk). Download the separate [KaW game files](https://github.com/TheAcAc/LayZDroid/releases/download/v0.1.0-preview.1/KaW-362-APKs.zip), right-click the ZIP and choose Extract All. In LayZDroid, start an instance, click Import APK, select all three extracted APKs together, then click Open KaW and sign in. KaW is third-party software and is not covered by LayZDroid's MIT licence.

This is a launcher/configuration preview, not the finished custom stripped Android image. Low-spec performance, KaW authenticated workflows and comparisons with BlueStacks still need testing. Windows virtualization/WHPX is required. Existing LayZ 1.5.2 retains its ten-worker limit; full recovery integration is still to come.
