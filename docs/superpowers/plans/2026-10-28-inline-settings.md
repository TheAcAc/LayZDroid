# LayZDroid Inline Settings Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan in the current session. Steps use checkbox syntax for tracking.

**Goal:** Apply the approved full-width instance list with inline settings below it, preserving the purple LayZ identity.

**Architecture:** Change only the Windows Forms presentation in MainForm. Retain the existing controls and action handlers. Package a separate candidate from GitHub commit 681580f; do not replace the installed 1.0.5 application with this 1.0.2-based build.

**Tech Stack:** C#, .NET 8 Windows Forms.

**Spec:** User-approved Inline settings mockup and outputs/LayZDroid-layout-spec.md in the parent chat workspace.

## Global Constraints

- Preserve existing colours, instance identities, resource limits, storage and runtime behavior.
- Full-width instance table; settings underneath in three aligned groups.
- Keep operation cancellation and diagnostic/folder actions available.
- No new runtime dependencies.

## Review Focus

- Minimum supported size must retain settings and actions without clipping.
- Long names and data paths must not push other controls offscreen.
- Disabled controls retain readable contrast.
- Selection continues loading the corresponding settings.
- Candidate packaging must explicitly identify the GitHub baseline mismatch.

### Task 1: Refine the manager presentation

**Files:** Modify Tools/LayZDroid/MainForm.cs. No action-handler signatures change.

- [x] Change body to one column with an expanding list and fixed-height inline settings.
- [x] Arrange settings in three columns; retain numeric, graphics and name controls, save/import/delete handlers and standard display.
- [x] Improve input and header contrast, control alignment and selected-instance context.
- [x] Run existing UI checks and inspect rendered form images at normal and minimum widths.

### Task 2: Verify and package the candidate

**Files:** Candidate executable, source patch, rendered screenshots, test logs and README in the chat outputs folder.

- [x] Run dotnet run --project Checks/LayZDroid/Checks.csproj -c Release.
- [x] Run dotnet run --project Checks/LayZDroidUi/Checks.csproj -c Release with the render output path; expect all checks to pass.
- [x] Publish a Windows x64 self-contained single-file build to a separate candidate folder.
- [x] Review the diff to confirm action handlers are unchanged; document the source baseline and unverified live workflows.

## Execution notes

This is a reversible presentation change. Use existing checks and visual inspection rather than adding tests that mirror the layout implementation.
The native worktree tool rejected the external sourceRoot, so an isolated detached Git worktree was created inside the chat's work folder from the fetched GitHub ref.

