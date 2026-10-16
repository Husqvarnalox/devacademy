# Contributing to DevAcademy

DevAcademy is a native C# / .NET 10 / Avalonia learning IDE. Read [ARCHITECTURE.md](ARCHITECTURE.md) (module boundaries),
[RUNTIME_ARCHITECTURE.md](RUNTIME_ARCHITECTURE.md) (execution engine) and [SECURITY_MODEL.md](SECURITY_MODEL.md) first.

## Development setup

Prerequisites: .NET SDK 10 (`dotnet --version` ≥ 10), `make`, git. Optional, only for exercises/tests of that language:
Python 3, Node.js ≥ 22.6, Go, JDK, Kotlin, clang++/g++, Rust, PHP; Docker for container isolation tests. The only platform
verified end to end is macOS arm64 ([PLATFORM_SUPPORT.md](PLATFORM_SUPPORT.md)).

```
make setup             restore packages and list detected language runtimes
make dev               run the desktop app (Debug)
make build             build the solution
make test              all tests;  make test-fast = all except the slow runner suite
make test-runners      code execution engine tests (real toolchains; missing ones are skipped)
make lint / format     dotnet format whitespace (verify / apply) — CI fails on a lint diff
make validate-content  validate content and run reference solutions
make runtimes          print detected runtimes
make publish           self-contained desktop build for RID=<rid> into artifacts/
make clean
```

## Project layout

`src/DevAcademy.{Domain,Content,Execution,AI,Application,Infrastructure,Desktop,Cli}`, one xUnit v3 project per src project
under `tests/`, curriculum data under `content/`, design docs at the root and under `docs/`. Dependency direction is
enforced by project references: Domain depends on nothing; Execution knows nothing about lessons; only Desktop references
Avalonia. Do not add a reference that breaks the arrows in ARCHITECTURE §2.

## Coding rules

* `TreatWarningsAsErrors` and nullable are on; keep the build warning-free. Package versions live in `Directory.Packages.props`.
* No WebView in the main UI; no Electron/Tauri/Qt for DevAcademy itself.
* User code runs only through `ICodeRunner`, in a child process, never in the app process. Do not add code paths that run
  learner code with a weaker isolation without reporting `AppliedIsolation`.
* API keys only in `ISecretStore`; never log, persist or export them; never pass them to child processes.
* Nothing installs software or touches the network on its own: package installs and downloads happen only on explicit user action.
* Do not claim support you have not verified. Unverified = say so in code comments and docs; unimplemented = **Planned**.
* Tests: add or update tests with every behaviour change; AI code is tested with fake HTTP handlers, never live APIs; tests needing
  a toolchain must skip cleanly when it is absent.
* Run `make lint test-fast validate-content` before opening a PR. CI additionally runs the runner suite on 5 RIDs.

## Authoring content

Full format: [docs/content-authoring/README.md](docs/content-authoring/README.md). Short version:

1. Check the target track exists in `content/catalog.yaml` (planned tracks become `available` only with a real course).
2. Create `content/courses/<course-id>/course.yaml` and `lessons/<NN-slug>/{lesson.yaml,theory.md,exercise/...}`.
3. Write the reference solution first, then tests (>=1 public, >=1 hidden), then the starter that must fail them.
4. `make validate-content` until clean — it runs solutions, starters and runnable theory samples.
5. Version-specific claims (framework APIs, CLI flags) must have an https source or be phrased "verify against current docs".

Curriculum design docs (Russian) are under [docs/curriculum](docs/curriculum) and [CURRICULUM.md](CURRICULUM.md); update them when
a course moves from Planned to implemented.

## Documentation

English for engineering docs, Russian for curriculum docs. Document reality, mark the rest **Planned**, keep
[ROADMAP.md](ROADMAP.md) status markers current, and link rather than duplicate.

## Reporting security issues

See [SECURITY.md](SECURITY.md). License: MIT ([LICENSE](LICENSE)); contributions are accepted under the same license.
