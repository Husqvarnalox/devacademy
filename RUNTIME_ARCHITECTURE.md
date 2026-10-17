# DevAcademy — Runtime Architecture

Describes `src/DevAcademy.Execution` **as implemented**, then the Runtime Manager parts that are
**Planned**. The engine knows nothing about lessons: it writes files, compiles, runs under limits and
reports what it really applied. Evaluation (Io / Harness) lives in `DevAcademy.Application`.

Verification status: every runtime and sandbox below has been exercised **only on macOS arm64**.
See [PLATFORM_SUPPORT.md](PLATFORM_SUPPORT.md).

## 1. Components

| Piece | File | Role |
|---|---|---|
| `ICodeRunner` / `CodeRunner` | `CodeRunner.cs` | `PrepareAsync` (write files into a fresh temp dir, run compile steps once) → `IPreparedProgram.RunAsync` (once per test case) → dispose deletes the dir. `RunOnceAsync` = prepare + run + dispose. `DetectRuntimesAsync` lists runtime statuses. |
| `ILanguageRuntime` | `Languages/Runtimes.cs` | Per-language adapter: `DetectAsync` (version probe — the only place anything executes) and `CreatePlan` → `BuildPlan` (compile steps + run command). |
| `IProcessSandbox` | `Sandboxes/*` | Runs one `ProcessSpec` under `ResourceLimits` and `IsolationLevel`. |
| `ProcessRunner` | `Sandboxes/ProcessRunner.cs` | Shared process driver: stdin, capped output, wall timeout, memory polling, tree kill. |
| `ToolLocator` | `Languages/ToolLocator.cs` | PATH lookup + short version probes. GUI apps on macOS start with a minimal PATH, so it also searches `/opt/homebrew/bin`, `/usr/local/bin`, `~/.cargo/bin`, `~/.dotnet`, `~/.sdkman/candidates/kotlin/current/bin`. The augmented PATH is what child processes receive. |
| `TypeScriptTypes` / `TypeScriptTypesProvisioner` | `Languages/TypeScriptTypes.cs` | Optional `@types/node` in the runner cache so `tsc` can type-check. Installs from the npm registry **only when called on explicit user action**; the runner never does. |
| `PackageManagerDetector` | `Languages/PackageManagerDetector.cs` | Detection only: `npm`, `pnpm`, `yarn` versions. Never installs packages or runs project scripts. |
| `ExecutionOptions` | `ExecutionOptions.cs` | `MaxConcurrentRuns` (default 2), `CacheDirectory`, `AllowIsolationFallback` (default true), `DockerImages`. |

Flow of one graded run: `ExecutionRequest(languageId, files, entryFile, requested isolation)` →
unknown language / path containing `..` or escaping the workdir are rejected → work dir
`<tmp>/devacademy-run-<guid>` (mode 0700 on Unix; world-accessible only for containers, because they run as uid 65534) →
sandbox selection → runtime detection → compile steps (each under `ResourceLimits.DefaultCompile`,
optionally with a larger `MinWallTimeout`) → run → result. The temp dir is always deleted.

## 2. Limits

| Limit | Default (run) | Default (compile) | Enforced by |
|---|---|---|---|
| Wall time | 5 s | 90 s (Kotlin 120 s via `MinWallTimeout`) | `ProcessRunner` timeout → tree kill → `Timeout` |
| Memory | 256 MiB | 2048 MiB | RSS of the **whole process tree** polled every 100 ms (`ProcessTreeSampler`); over limit → kill → `MemoryLimitExceeded`. Polling can overshoot between samples; it is not a hard cap. Containers use `docker --memory` (OOM exit 137 is mapped to `MemoryLimitExceeded`). |
| Output | 64 KiB | 256 KiB | Capped reader; exceeding kills the process → `OutputLimitExceeded` |
| CPU time | wall timeout + 1 s | same | `ulimit -t` (Unix only) |
| File size | 256 MiB per file | same | `ulimit -f` (Unix only) |
| Core dumps | 0 | 0 | `ulimit -c 0` (Unix only) |
| Network | denied | denied | `ResourceLimits.AllowNetwork` (default false); enforced only by OsSandbox / Container |
| Concurrent processes | `MaxConcurrentRuns` = 2, global across compile steps and runs | | semaphore in `CodeRunner` |

Environment given to child processes: `PATH` (augmented), `HOME` and `TMPDIR` inside the workdir,
`LANG=C.UTF-8`, `LC_ALL=C.UTF-8`, plus the adapter's own variables. Nothing else is inherited, so API keys
in the parent environment never reach learner code. On Windows `TEMP`, `TMP`, `SystemRoot`, `ComSpec`,
`PATHEXT`, `windir` are passed through. (The `docker` CLI itself keeps the full environment so it can find
its daemon; learner code inside the container gets the minimal one.)

Process-tree cleanup: on Unix the command is wrapped as
`sh -c 'ulimit …; exec perl -e "setpgrp(0,0); exec @ARGV" cmd…'` so the root becomes a process-group leader
and the group can be killed. **If `perl` is absent, the wrapper falls back to a plain `exec` (no process
group), and cleanup relies on `Process.Kill(entireProcessTree)` plus killing grandchildren seen in the last
memory sample.** Double-forked daemons can still escape on macOS/Windows. Windows: no `ulimit`, no process
group; only tree kill + polling.

## 3. Isolation levels and what each guarantees

`IsolationLevel` is a request; `ExecutionResult.AppliedIsolation` is the truth. `SelectSandboxAsync` walks
downward from the requested level (Container → OsSandbox → ProcessLimits) while `AllowIsolationFallback` is
true, recording each skipped level in the result message. With fallback disabled, an unavailable level yields
an `InternalError` result instead of a weaker run (`Failed(..)` carries the collected notes).

| Level | Mechanism | Network | File writes | File reads | Status |
|---|---|---|---|---|---|
| `ProcessLimits` | child process + limits above | **not blocked** | not restricted (runs as the user) | not restricted | all OS; **not a security boundary** |
| `OsSandbox` macOS | `/usr/bin/sandbox-exec` SBPL profile: `(allow default)(deny network*)(deny file-write*)` then allow writes to workdir, runner cache, `/private/tmp/.dotnet`, `~/Library/Application Support/dotnet`, `/dev/null`,`/dev/tty`,`/dev/dtracehelper` | denied | workdir + caches only | **allowed**, except `~/.ssh`, `~/.aws`, `~/.gnupg`, `~/Library/Keychains` | verified by tests on macOS arm64. Availability is probed with a real `sandbox-exec` call (nested sandboxes are refused by the kernel). `sandbox-exec` is deprecated by Apple but functional. |
| `OsSandbox` Linux | `bwrap --ro-bind / / --dev /dev --proc /proc --tmpfs /tmp --bind workdir --bind cache --unshare-all --die-with-parent --new-session` | denied (`--unshare-all`) | workdir + cache | read-only view of the whole filesystem | **argument construction unit-tested only; never executed on Linux.** Needs `bwrap` and unprivileged user namespaces. |
| `OsSandbox` Windows | — | — | — | — | **not implemented**; falls back to ProcessLimits |
| `Container` | `docker run --rm -i --pull never --network none --memory N --memory-swap N --pids-limit 64 --cpus 1 --read-only --tmpfs /tmp:rw,size=64m --security-opt no-new-privileges --cap-drop ALL --user 65534:65534 -v workdir:workdir` | denied | workdir (bind mount) + 64 MiB tmpfs | image + workdir | languages **python** (`python:3.13-slim`) and **javascript** (`node:22-slim`) only; images must already exist locally (`--pull never`, error tells you to `docker pull`). Kill via `docker kill <name>`. In container mode the host does not need the language installed (the image provides it). Not a strong boundary against kernel exploits; not for multi-tenant use. Covered by argument-construction tests; end-to-end container runs depend on Docker being present and are not part of the verified-on-macOS claim above unless Docker was available. |

Honest summary: on macOS the `OsSandbox` level genuinely blocks network and writes outside the workdir; on
Linux it is designed but unverified; on Windows only resource limits exist and the UI shows the applied
level. Reads are not confined anywhere except Container.

## 4. Per-language commands

Compile steps run once per `PrepareAsync`; the run command runs once per test case. `DefaultEntryFile` is the
default when the exercise gives none.

| Language id | Entry | Detection | Compile | Run |
|---|---|---|---|---|
| `python` | `main.py` | first working of `python3`/`python` (`--version`, rejects the Windows Store shim) | — | `python -E -s -B -X utf8 <entry>` (isolated from env and user site, no bytecode); env `PYTHONDONTWRITEBYTECODE=1`, `PYTHONIOENCODING=utf-8`. Containerizable. |
| `javascript` | `main.js` | `node --version` | — | `node <entry>`. Containerizable. |
| `typescript` | `main.ts` | Node ≥ **22.6** required (else `Unsupported`); `tsc --version` | if `tsc` found **and** `@types/node` provisioned in the runner cache: `tsc --noEmit --strict --target es2022 --module nodenext --types node --typeRoots <cache>/typescript-types/node_modules/@types <all .ts files>` | `node [--experimental-strip-types if Node < 23.6] --disable-warning=ExperimentalWarning <entry>`. Without `tsc`/types: runs by type stripping and sets `RuntimeStatus.Warning` ("type-checking unavailable …") which is surfaced in the result. Types are erased, so a stripped run proves nothing about type correctness. |
| `go` | `main.go` | `go version` | `go build -o prog <non-test .go files>`; env `GOFLAGS=-mod=mod`, `GOCACHE`/`GOPATH` under the runner cache | `./prog` |
| `java` | `Main.java` | `javac -version` and `java -version` (a JDK) | `javac -encoding UTF-8 -d out <all .java>` | `java -XX:+UseSerialGC -Xss16m -cp out <MainClass>` |
| `kotlin` | `Main.kt` | `kotlinc -version` + working `java` | `kotlinc <all .kt> [-cp <coroutines jar>] -include-runtime -d prog.jar` (timeout ≥ 120 s) | `java -XX:+UseSerialGC -Xss16m -jar prog.jar`; with the coroutines jar: `java -XX:+UseSerialGC -Xss16m -cp prog.jar<sep><coroutines jar> <package.>FileKt` (`-jar` ignores `-cp`; the main class is derived from the entry file and its `package` line), and the jar's directory is added to `ReadOnlyPaths` of the run step. |
| `cpp` | `main.cpp` | first working of `clang++`/`g++` (rejects the Xcode shim without Command Line Tools) | `-std=c++20 -pthread -O2 -Wall -Wextra -o prog <.cpp/.cc/.cxx>` (debug plan: `-std=c++20 -pthread -g -O0 -Wall -Wextra`) | `./prog` |
| `csharp` | `Program.cs` | `dotnet --version`, SDK major ≥ **10** (file-based apps) | `dotnet build <entry> -o out --nologo -v q --disable-build-servers -p:UseSharedCompilation=false`; env `DOTNET_CLI_HOME`, `NUGET_PACKAGES` under the runner cache, telemetry off | `dotnet out/<name>.dll`. **Only the entry file is compiled** (file-based app). |
| `rust` | `main.rs` | `rustc --version` (also searches `~/.cargo/bin`) | `rustc --edition 2021 -O -o prog <entry>`; `RUSTUP_HOME`/`CARGO_HOME` pointed at the real dirs (read-only use) | `./prog`. Single `rustc` invocation; `mod x;` resolves next to the entry. No Cargo projects. |
| `php` | `main.php` | `php --version` | — | `php <entry>` |

`RuntimeStatus` carries `Availability` (Available / Missing / Unsupported), `Version`, `ExecutablePath`,
`Notes`, `InstallHint`, `Warning`. A missing runtime yields `ExecutionStatus.RuntimeMissing` with the hint.
`devacademy runtimes` (CLI) prints the detected set.

Kotlin: `DetectAsync` looks for `kotlinx-coroutines-core-jvm.jar` beside the symlink-resolved `kotlinc`
(`lib/`, `../lib/`, `../libexec/lib/`: zip layout and Homebrew) and reports it in `Notes`; `CreatePlan` re-resolves it
(no extra field on `RuntimeStatus`). Without the jar the plan is the plain `-jar` one and `import kotlinx.coroutines.*`
fails to compile. C++ builds with `-pthread` so `std::thread` links on Linux. The standard stays `-std=c++20`: `-std=c++23`
was tried and rejected because libc++ declares `std::print` in C++23, which ADL finds for calls such as
`print("label", someStdVector)` and silently prefers over a user's own `print(const std::string&, const std::vector<T>&)`
(the cpp-first-steps value-semantics exercise prints wrong output). `std::expected` therefore stays unavailable;
the language-service `compile_flags.txt` matches (`-std=c++20 -pthread`).

Course packages come in per-course **sets** (`tools/content-packages/`, shipped as `content-packages/` beside the executable; manifests only).
Node sets are installed by `NodeEnvironmentService` into `<data dir>/node-content/<set>/` via `npm ci --ignore-scripts --no-audit --no-fund` run in that
set's directory only, after an explicit confirmation showing the command and that the network is needed; a run selects the exercise's set (smallest set
covering its packages) and mounts only that `node_modules` read-only. Python sets are pinned requirements files installed into the active managed
environment through `PythonEnvironmentService.PlanSetInstallAsync` (the same plan + consent flow). CLI `--node-env <dir>` takes the directory holding the set
subdirectories (`make node-env`). The runner itself never installs anything.

Go sets (`tools/content-packages/go/<set>/{go.mod,go.sum}`, language `go` in `index.json`) are installed by `GoEnvironmentService` into
`<data dir>/go-content/<set>/` with `go mod download` (`GOMODCACHE=<root>/<set>/pkg/mod`, `GOFLAGS=-mod=mod`, `GOTOOLCHAIN=local`) run in that set's directory only,
after the same confirmation (modules with versions, the exact command, that the network is needed). A run whose exercise declares `requires.goModules` (and optionally
`requires.go: ">=1.26.5"`) gets `ExecutionRequest.Go = GoRunSelection(modcache path)`: `GoRuntime` then builds offline (`GOPROXY=off`, `GOSUMDB=off`, `GOFLAGS=-mod=mod`,
`GOTOOLCHAIN=local`, `CGO_ENABLED=0`), the module cache is a read-only path of the OS sandbox (the build cache stays in the work directory), and such a run never uses a
container. `go.mod` and `go.sum` of the exercise are part of its files and must carry the same pins as the set. CLI `--go-env <dir>` takes the directory holding the set
subdirectories (`make go-env`, default `.go-content`). CI has no `make go-env` step yet (no verified `setup-go` configuration), so `make validate-content` in CI skips
Go-module exercises and theory samples with a warning.

Not covered by an adapter: any package installation by the runner, virtualenvs, multi-file Cargo/Gradle/Maven/npm projects.
A learner's `import numpy` works only if the system interpreter already has it (the sandbox does not provide
packages; the `-s` flag disables user site-packages).

## 5. Result model

`ExecutionStatus`: Success, CompileError, RuntimeError, Timeout, MemoryLimitExceeded, OutputLimitExceeded,
RuntimeMissing, Cancelled, InternalError. `ExecutionResult` also carries stdout/stderr, compile output,
duration, peak memory (when sampled), `AppliedIsolation` and a `Message` (isolation fallback notes,
TypeScript warning).

## 6. Planned: Runtime Manager

None of this exists; it is the design the next phases build on. See also ARCHITECTURE.md §14–§17.

* **Runtime Manager** generalises detection: each runtime/tool becomes a *capability provider* with
  `Detect → Describe → Provision (explicit user action only)`. `DetectRuntimesAsync` remains the read-only
  part and keeps working unchanged.
* **Python Environment Manager** (in progress): interpreter discovery, venv/uv environments under the app
  data dir, explicit dependency installs, package listing, validation, running code/tests inside the env.
  A new `ILanguageRuntime` variant takes an environment's interpreter path instead of a PATH lookup; `-s`
  stays on, environment isolation moves from "system interpreter" to "this venv". Installs need the network,
  so they run outside the sandbox profile that denies it, as a distinct, user-confirmed step.
* **Node project workspaces**: `npm`/`pnpm`/`yarn` `install`, `run dev|test|build` as workspace commands
  with long-lived process support (stream output, stop). Prerequisite: PTY or at least line-streaming
  process sessions. Install is the only network-enabled step.
* **ML Runtime Manager**: hardware probe + in-environment backend probe → capability set
  (`cpu`, `cuda`, `rocm`, `mps`); exercises declare required capabilities; CPU-first.
* **Jupyter kernels** for notebooks: ipykernel launched from a managed environment.
* **Windows OS sandbox** (AppContainer / Job Objects with restricted token) — undecided; until then
  Windows grades with ProcessLimits and says so.
