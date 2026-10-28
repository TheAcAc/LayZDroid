# LayZDroid 1.0.6

Includes the previously local 1.0.3–1.0.5 fixes and the new instance manager layout.

- Full-width instance list, with settings underneath in three aligned groups, retaining the existing purple theme.
- Clearer table headers and selected-instance context. Short windows scroll the middle section while footer tools and status stay available.
- Restore saved-instance recovery through `--start-instance <saved ID> --data <existing folder>`. Existing Android configuration and account folders are verified and reused, never recreated by recovery.
- Preserve recovery runtime-status merging and emulator log draining after the start acknowledgement.
- Fix process verification when WMI omits an executable path or returns an exited process. Native limited-rights inspection verifies identity and keeps ownership checks intact.
- Also correlate WMI creation time with native process identity, preventing stale arguments from being attached to a reused process ID even when the executable path is unchanged.
- Retain the local 1.0.5 RAM fix: additional host headroom is bounded to 1–2 GB, with emulator overhead, commit checks and pending-start reservations preserved. Rejections show required and available memory.
- Include `LayZDroid-Recovery.exe`, an identical copy for existing recovery integrations. Use `LayZDroid.exe` for the instance manager.

## Validation and scope

Policy/storage, UI rendering/contrast, process identity and saved-instance recovery checks pass. The release workflow runs all four suites before publishing the Windows x64 executable and SHA-256 checksums.

Local fixes were recovered from their original source snapshots and patches, then compared with the installed 1.0.5 assembly: all 17 non-UI types matched decompiled code after normalizing licence-text line endings, before the additional creation-time safeguard was applied. See [local fix provenance](../LOCAL-FIX-INVENTORY.md).

Existing game accounts, instance identities, configured resources and data locations are retained. This release does not install a Google Play image or fix KaW's account-specific billing popup. Live emulator workflows and Windows display-scaling configurations were not requalified for this release; low-spec performance remains experimental.
