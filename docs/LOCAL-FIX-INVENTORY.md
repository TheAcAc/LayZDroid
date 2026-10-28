# Local fixes included in 1.0.6

The installed 1.0.5 executable was built from a dirty source tree identified by informational version `1.0.5+95311a1b8532dbff0209627296e4a3f74e767e25`; that revision was unavailable in the checked GitHub repositories. Its SHA-256 is `09219dc88c4d6b6bed0d9c7db13e6ecc507b3b81019d47a5c33dc0289272e93a`.

The missing source was recovered from original file snapshots and patch records in the earlier startup-repair session, rather than replacing it with an older published runtime.

| Area | Included source and validation |
| --- | --- |
| Saved-instance recovery | Program.cs, RecoveryCommand.cs, Runner.Start(useExistingConfiguration), Store.PrepareRecoveryConfiguration; existing configuration, exact ID, cancellation and status-merge checks |
| Process verification | ProcessIdentity/ProcessInventory, Runner.Inventory/Owner; exited processes, missing WMI paths, native limited-rights inspection and unverifiable identity checks |
| RAM admission | Policy.CanStart; 6,297 MB available with a 2,816 MB guest, exact 5,568 MB boundary, pending starts and insufficient commit regressions |
| New manager layout | MainForm; full-width list and inline settings, original minimum size and short-screen scrolling |
| Additional release safeguard | WMI/native creation-time correlation; same-executable PID reuse and timezone-offset regressions |

Before adding the new creation-time safeguard, a build of the reconstructed source was decompiled with the same tool as the installed executable. All 17 non-UI types matched after normalizing line endings inside the displayed licence text: ApkInfo, ApkMetadata, DataFolders, HostMemory, Instance, NetworkStartup, Policy, ProcessIdentity, ProcessInventory, Program, RecoveryCommand, Runner, RuntimeSetup, RuntimeTerms, Store, TcpOwnership and ThemeButton. MainForm differs intentionally for the approved visual changes. This is a code-comparison result, not a claim that rebuilt executable bytes or all live behavior are identical.

The historical LayZ-side Publish-Recovery and Verify-Publish scripts belong to the separate LayZ distribution. This LayZDroid-only release preserves their required helper behavior by publishing the same self-contained binary under both LayZDroid.exe and LayZDroid-Recovery.exe, with checksums. No LayZ bot source or account configuration is included.
