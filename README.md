# DevAcademy

A desktop-first, offline-first **learning IDE** for programming: lessons with theory that explains what happens inside the
language runtime, a code editor, real execution of your code in a sandboxed child process, automatic grading with hidden
tests, spaced reviews, and an optional AI tutor with your own API key. Native app in C# / .NET 10 / Avalonia — no Electron,
no WebView UI, no server. The UI and curriculum are in Russian.

## Honest status

Early-stage. It builds and runs; the content is small.

| Area | State |
|---|---|
| App shell (catalog, roadmap, lesson workspace, editor, dashboard, settings) | implemented |
| Code execution: python, javascript, typescript, go, java, kotlin, cpp, csharp, rust, php | implemented, **verified only on macOS arm64** |
| Isolation: process limits; OS sandbox (macOS `sandbox-exec` verified, Linux bubblewrap untested, Windows none); Docker for python/javascript | see [RUNTIME_ARCHITECTURE.md](RUNTIME_ARCHITECTURE.md) |
| Grading (stdout comparison or test harness), hints, quizzes, reviews, progress in SQLite | implemented |
| AI tutor: OpenAI, Anthropic, Gemini, OpenRouter, OpenAI-compatible | implemented, tested with fake HTTP only; no live API verified |
| Curriculum | Python Fundamentals (10 lessons) and short intro courses for JavaScript, TypeScript, Node.js, C#, PHP (2 lessons each); `content/catalog.yaml` is the source of truth. Everything else is **Planned** and shown as such |
| notebooks, Python environment manager (in progress), ML runtime manager, auto-update | not implemented |

Roadmap with per-item status: [ROADMAP.md](ROADMAP.md). Platform matrix: [PLATFORM_SUPPORT.md](PLATFORM_SUPPORT.md).

## Install

Download from the GitHub Releases page (builds are produced by CI on version tags; verify against `SHA256SUMS`, and against
`SHA256SUMS.asc` when the release has it, see [docs/release-signing.md](docs/release-signing.md)):

* **macOS**: `DevAcademy-<v>-osx-arm64.dmg` (Apple silicon) or `-osx-x64.dmg`; drag DevAcademy to Applications. A release
  signed with a Developer ID and notarized opens normally; an ad-hoc signed one (the workflow summary says which) is
  quarantined by Gatekeeper: right-click → Open, or `xattr -dr com.apple.quarantine /Applications/DevAcademy.app`.
* **Windows**: `DevAcademy-<v>-win-x64-setup.exe` (per-user, no admin rights) or the `.zip`. Signed releases carry an
  Authenticode signature from SignPath Foundation; SmartScreen may still warn until the signature builds reputation.
* **Linux**: `.AppImage` (`chmod +x`, run) or `devacademy_<v>_amd64.deb` (`sudo apt install ./devacademy_*.deb`).

Packaging status per platform (only macOS arm64 is run-verified): [PLATFORM_SUPPORT.md](PLATFORM_SUPPORT.md). Build installers yourself: `make package`.

## Code signing policy

Free code signing for Windows is provided by [SignPath.io](https://about.signpath.io), certificate by
[SignPath Foundation](https://signpath.org). macOS builds are signed with the project's Apple Developer ID when the
release workflow has it; Linux checksums are signed with the project's GPG key. Details: [docs/release-signing.md](docs/release-signing.md).

* Committers and reviewers: [members of this repository](https://github.com/Husqvarnalox/devacademy/graphs/contributors)
* Approvers (approve each signing request): [@Husqvarnalox](https://github.com/Husqvarnalox)

Only binaries built by the release workflow from this repository's source are signed.

**Privacy.** DevAcademy does not transfer any information to other networked systems unless the user asks for it: an
AI provider receives requests only after the user configures it ([AI_PROVIDERS.md](AI_PROVIDERS.md)), and language
packages are downloaded only when the user installs a package set. There is no telemetry.

## Quick start

Prerequisites: [.NET SDK 10](https://dotnet.microsoft.com/download), `make`, git. To run exercises in a language you also need
that language's toolchain installed (DevAcademy detects it, never installs it): e.g. Python 3 for the Python course.

```
git clone <repo-url> devacademy && cd devacademy
make setup      # restore packages, list detected language runtimes
make dev        # run the desktop app
make test       # run all tests          (make test-fast skips the slow runner suite)
```

Other targets: `build`, `test-runners`, `lint`, `format`, `validate-content`, `runtimes`, `publish`, `package`, `checksums`, `notices`, `clean` (`make help`).
Content is read from the `content/` directory at runtime; edit a lesson and reload without rebuilding.

## Documentation

* [ARCHITECTURE.md](ARCHITECTURE.md) — layers, domain model, content/evaluation/path engines, planned modules
* [RUNTIME_ARCHITECTURE.md](RUNTIME_ARCHITECTURE.md) — execution engine, limits, isolation per OS, per-language commands
* [SECURITY_MODEL.md](SECURITY_MODEL.md), [SECURITY.md](SECURITY.md) — threat model, vulnerability reporting
* [AI_PROVIDERS.md](AI_PROVIDERS.md) — providers, endpoints, keys, what is sent
* [PLATFORM_SUPPORT.md](PLATFORM_SUPPORT.md) — OS/arch/GPU matrix with verification status
* [CURRICULUM.md](CURRICULUM.md), [docs/curriculum](docs/curriculum) — curriculum model and planned learning trajectories (Russian)
* [docs/content-authoring](docs/content-authoring/README.md) — how to write courses and lessons
* [CONTRIBUTING.md](CONTRIBUTING.md), [docs/adr](docs/adr), [docs/requirements](docs/requirements/ecosystem-expansions.md)

## License

MIT — see [LICENSE](LICENSE).
