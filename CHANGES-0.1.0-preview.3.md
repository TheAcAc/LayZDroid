# LayZDroid 0.1.0 Preview 3

- Check Android's active internet connection during instance startup.
- Retry virtual Wi-Fi once when it is not ready, including rejoining AndroidWifi.
- Leave healthy connections unchanged; keep Android running if internet remains unavailable.
- Preserve hardware settings, display controls and instance deletion.

Existing settings and instances are kept. The [KaW game download](https://github.com/TheAcAc/LayZDroid/releases/download/v0.1.0-preview.1/KaW-362-APKs.zip) is unchanged.

Verified locally: a fresh boot detected missing connectivity, reconnected Wi-Fi, reached Android's validated internet state and launched KaW. Low-spec hardware and signed-in battle automation still need separate testing.
