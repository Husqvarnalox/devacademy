# ADR 0001 — C# / .NET 10 + Avalonia UI for the desktop application

Status: Accepted · Date: 2026-10-16

## Context

DevAcademy is desktop-first and offline-first. Requirements that drive the choice:

* Native UI on Windows x64/ARM64, macOS x64/ARM64, Linux x64/ARM64. Electron and Tauri are
  excluded; a WebView shell around a web UI is excluded.
* The educational core (content, learning path, evaluation, progress, AI providers) must be
  reusable by a future web version without a rewrite.
* The app spawns compilers and interpreters, manages child processes and limits, talks to OS
  secret stores, and needs a capable code editor.
* One maintainable language for UI and core; open-source friendly licensing.

## Decision

* **C# on .NET 10 (LTS, supported until Nov 2028)** for all layers.
* **Avalonia UI 12** for the presentation layer, MVVM with **CommunityToolkit.Mvvm**.
* **AvaloniaEdit + TextMate grammars** for the editor; **Markdig** rendered to native Avalonia
  controls for lesson theory (no WebView).
* **SQLite** (Microsoft.Data.Sqlite) for local state.
* A strict layer split (see ARCHITECTURE.md §2): only `DevAcademy.Desktop` references Avalonia.

## Alternatives considered

| Option | Why not |
|---|---|
| .NET MAUI | No official Linux desktop support. |
| Qt (C++) / Qt for Python | Strong native toolkit, but C++ for the whole product slows iteration and complicates the async HTTP/AI/process code; PySide makes packaging and child-process control harder; licensing (LGPL/commercial) adds friction. |
| Flutter | Good rendering, but Dart ecosystem for process control, PTY, secret stores and editor components is thinner; second language for a future .NET/ASP.NET backend. |
| Uno Platform | Viable; Avalonia has a larger desktop-focused community and a mature editor control (AvaloniaEdit). |
| Electron / Tauri / WebView | Excluded by requirements. |

## Consequences

* Positive: one language end-to-end; the core can be hosted later by ASP.NET Core; first-class
  async, `System.Diagnostics.Process`, `HttpClient` + SSE streaming; self-contained single-file
  publishes per RID (`win-x64`, `win-arm64`, `osx-x64`, `osx-arm64`, `linux-x64`, `linux-arm64`).
* Negative: Avalonia renders its own controls (Skia), so look-and-feel is "consistent", not
  OS-stock; accessibility support must be verified per platform. There is no mature
  cross-platform terminal-emulator control for Avalonia — the PTY terminal (ConPTY / forkpty) and
  a VT renderer are a dedicated work item (ROADMAP Phase 3b), not hidden behind a text box.
* C++ is reserved for isolated native helpers if a measured need appears (none so far).
