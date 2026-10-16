# DevAcademy — Platform Support

**Only macOS arm64 has been verified end to end** (build, tests, running the app, runtimes, sandbox, Keychain).
Everything else is "builds in CI" at best, and CI results are not yet recorded in this file; do not read ✔-like
claims into the other columns.

Legend: **V** verified by hand/tests on real hardware · **B** built/tested by CI only (workflow exists, results not
reviewed for this document) · **C** compiled/argument-tested, never executed · **—** not implemented · **?** unknown.

## 1. Runtime identifiers

CI (`.github/workflows/ci.yml`): `test` job builds, checks formatting, runs all tests and `validate-content --run-solutions`
on linux-x64, linux-arm64, win-x64, osx-arm64, osx-x64. `publish` job (push to main only) produces a self-contained
single-file build with `content/` copied next to it for linux-x64, linux-arm64, win-x64, win-arm64, osx-arm64, osx-x64
and uploads it as a workflow artifact. No installers, no code signing, no notarization, no auto-update.

| Feature | osx-arm64 | osx-x64 | win-x64 | win-arm64 | linux-x64 | linux-arm64 |
|---|---|---|---|---|---|---|
| Builds / unit tests | V | B | B | publish only | B | B |
| Desktop app launches | V | ? | ? | ? | ? | ? |
| Self-contained publish | B | B | B | B | B | B |
| SQLite persistence | V | B | B | ? | B | B |
| Secret store | V (Keychain) | C | C (Credential Manager) | C | C (libsecret) | C |
| Language runtimes detected & run (python, node, go, java, kotlin, cpp, csharp, rust, php) | V | ? | ? | ? | ? | ? |
| `ProcessLimits` isolation | V | ? | partial: no `ulimit`, no process group | ? | ? | ? |
| `OsSandbox` | V (`sandbox-exec`) | ? (same mechanism, untested) | — | — | C (bubblewrap) | C |
| `Container` (Docker; python, javascript) | argument-tested; end-to-end only where Docker is installed | ? | ? | ? | ? | ? |
| Code editor (AvaloniaEdit + TextMate) | V | ? | ? | ? | ? | ? |

Notes: Windows ARM64 is built but not tested in CI. macOS `sandbox-exec` is deprecated by Apple but works on the verified
version (Darwin 25). Linux bubblewrap needs `bwrap` and unprivileged user namespaces (often disabled in containers and
some distributions); when unavailable the runner falls back to `ProcessLimits` and says so.

## 2. Toolchains the app does not ship

DevAcademy detects, never installs, language toolchains. Needed for exercises: Python 3, Node.js (≥ 22.6 for TypeScript),
Go, a JDK, Kotlin compiler (+JDK), clang++/g++, .NET SDK 10+, Rust (`rustc`), PHP. Missing ones produce a clear
`RuntimeMissing` result with an install hint. `tsc` + `@types/node` are optional for TypeScript type-checking.

## 3. ML acceleration matrix (Planned — ML Runtime Manager does not exist)

What the frameworks' own documentation supports in general terms. **Nothing here has been verified with DevAcademy**,
and a detected GPU never implies framework support: the manager will confirm a backend only by probing inside the chosen
Python environment. Verify exact versions against the framework's current install matrix at implementation time.

| Accelerator | macOS arm64 | macOS x64 | Windows x64 | Windows arm64 | Linux x64 | Linux arm64 |
|---|---|---|---|---|---|---|
| CPU | expected to work | expected | expected | wheel availability varies | expected | wheel availability varies |
| NVIDIA CUDA | impossible (no CUDA on macOS) | impossible | framework dependent; needs driver + CUDA-enabled build | not supported by most builds | framework dependent; needs driver + CUDA-enabled build | limited (e.g. Jetson/Grace builds) |
| AMD ROCm | impossible | impossible | not generally supported by frameworks (support differs from Linux) | no | framework dependent; limited GPU list | no |
| Apple MPS (Metal) | PyTorch: some ops; not every framework | no (Intel Macs have limited/no support) | no | no | no | no |
| DirectML | no | no | possible via separate packages | ? | no | no |

Status of every cell: **not verified**. The UI will state *why* acceleration is unavailable (OS, arch, framework build,
Python version, driver) and CPU-first exercises must pass everywhere.

## 4. Python/package availability caveat

Binary wheels for some ML packages lag new Python versions and are missing for some OS/arch pairs (notably Windows arm64 and
Linux arm64 for GPU builds). The Python Environment Manager (in progress) records Python version, OS and arch per environment
so such mismatches can be explained instead of surfacing as pip errors.
