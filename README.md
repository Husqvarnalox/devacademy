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
| Terminal (PTY), LSP, debugger, project workspaces (npm/venv), notebooks, Python environment manager (in progress), ML runtime manager, installers | not implemented |

Roadmap with per-item status: [ROADMAP.md](ROADMAP.md). Platform matrix: [PLATFORM_SUPPORT.md](PLATFORM_SUPPORT.md).

## Quick start

Prerequisites: [.NET SDK 10](https://dotnet.microsoft.com/download), `make`, git. To run exercises in a language you also need
that language's toolchain installed (DevAcademy detects it, never installs it): e.g. Python 3 for the Python course.

```
git clone <repo-url> devacademy && cd devacademy
make setup      # restore packages, list detected language runtimes
make dev        # run the desktop app
make test       # run all tests          (make test-fast skips the slow runner suite)
```

Other targets: `build`, `test-runners`, `lint`, `format`, `validate-content`, `runtimes`, `publish`, `clean` (`make help`).
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
