# DevAcademy — Security Model & Threat Model

Scope: the local desktop application. A future multi-tenant web version needs a separate
model (§6). Report vulnerabilities as described in [SECURITY.md](SECURITY.md).

## 1. Assets

| Asset | Where |
|---|---|
| AI provider API keys | OS secret store only (Keychain / Windows Credential Manager / Secret Service) |
| Learner source code and projects | plain files in user-chosen directories; lesson drafts in SQLite |
| Progress, history | SQLite in the app data dir |
| The user's machine (files, network, other processes) | — |

## 2. Trust boundaries & threats

| # | Threat | Boundary | Mitigation (current) | Residual risk |
|---|---|---|---|---|
| T1 | Learner/lesson code damages the machine (rm -rf, fork bomb, filling disk) | app ↔ child process | Code never runs in the app process. Fresh temp workdir, wall timeout, CPU rlimit, file-size rlimit, no core dumps, output cap, memory polling with kill, process-tree kill, global concurrency limit, minimal env. | **ProcessLimits is not a security boundary.** Code runs as the user. Fork bombs and double-forked daemons are only partially contained on macOS/Windows. |
| T2 | Code exfiltrates data over the network | process ↔ network | `OsSandbox`: macOS `sandbox-exec` profile denies network and writes outside workdir/caches. Linux: bubblewrap (`--unshare-all`) when installed — **implemented but unverified on real Linux**. `Container`: Docker `--network none`, read-only rootfs, non-root, pids/memory/cpu limits, no capabilities. | Reads of user files are still allowed under `sandbox-exec` (allow-default profile). Windows has no OS sandbox yet (falls back to ProcessLimits; the UI shows the applied level). Docker is not a strong boundary against kernel exploits. |
| T3 | Malicious content package (lesson code samples, harnesses, reference solutions) | content ↔ app | Content is data, loaded with a strict YAML model; code from content runs only through the execution engine under the same limits as learner code. Only the bundled `content/` is loaded today. | Installing third-party packages (future) needs signatures/checksums and a review UI. |
| T4 | API key leakage | app ↔ disk/logs/children | Keys only in the OS secret store; never in SQLite, JSON, exports, logs, crash dumps; child processes get a clean environment (no inherited env vars); keys sent only to the provider's own base URL. If no secret store is available, AI keys cannot be saved (no silent plaintext fallback). | A compromised user account can read the keychain entry. |
| T5 | AI output causes harmful actions (prompt injection via code/comments, "run this command") | AI ↔ app | AI responses are text only. No automatic command execution or file writes; proposed edits require diff review and explicit acceptance (planned workflow). User controls what context is sent (code on/off). | Users can still copy-paste harmful suggestions. |
| T6 | Unexpected spend on paid models | app ↔ provider | No automatic fallback to another model/provider; per-request metadata log; approximate cost shown only when the provider publishes prices, labelled as an estimate. | Provider-side billing is authoritative. |
| T7 | Cheating on graded exercises | learner ↔ evaluator | Exercise passes only when all public and hidden tests pass; harness results are bound to a per-run random nonce. | Offline: hidden tests are readable on disk and the nonce is in the child env. Integrity matters only to the learner; not a security property. |
| T8 | Corrupted local DB / crash during write | app ↔ disk | SQLite WAL + transactional migrations; atomic file writes (temp + rename) for exports; backup/restore. | — |
| T9 | Project terminal runs arbitrary commands | user ↔ own shell | The Projects terminal is the user's own shell (their account, environment, no sandbox) and the UI states this next to Run/Test. Run/Test/Install only *type visible text* into the terminal after the user's click; template `postCreate` commands are shown, never auto-run; nothing typed by an AI is executed (AI output is text only, T5). Deleting uses the OS trash; permanent delete needs an explicit warning. Project file contents are never stored in SQLite. | Whatever the user (or code they run) does in the shell is unrestricted. Windows ConPTY and trash paths are unverified. |

## 3. Isolation levels (what we claim)

* `ProcessLimits` — resource limits and cleanup. **Not isolation.**
* `OsSandbox` — macOS: no network, no writes outside the workdir and runner caches (verified by
  automated tests on macOS arm64). Linux bubblewrap: designed, argument-tested, not yet verified
  on hardware. Windows: not implemented.
* `Container` — Docker with the restrictions above, for Python and JavaScript. Suitable for local
  use. **Not suitable for public multi-tenant execution.**

The applied level is reported with every run result and displayed in the UI.

Python environments: a selected managed env is mounted **read-only** during runs (macOS profile deny rule,
bwrap `--ro-bind`; verified by a test on macOS), so learner code cannot plant `.pth` files or modify packages.
Container isolation is downgraded to OsSandbox for such runs. Package installs happen only on explicit user
confirmation, outside graded runs, with network access for that single tool process.

## 4. Secure defaults

Graded runs request `OsSandbox`; network is denied by default; secrets are never inherited by
child processes; AI is off until the user configures a provider; telemetry: none.

### Package sets and installation

Nothing is installed implicitly: opening a lesson, running or grading code, and opening Settings never download packages. A package set
(`content-packages/`, pinned manifests shipped with the app) is installed only after a confirmation dialog that lists the packages with versions, the exact
command (`npm ci --ignore-scripts --no-audit --no-fund` in the set's own directory, `go mod download` in the set's own directory with a private `GOMODCACHE`, or `pip/uv install -r <pinned file>` into the active managed Python
environment) and states that the network is needed. Lifecycle scripts are disabled, nothing is global, versions come from lock/pinned files, and set ids are
validated as path-safe. Removal of a set deletes only its directory and is confirmed. At grading time `node_modules` is read-only inside the sandbox. The same holds for the Go module cache: `go build` runs with `GOPROXY=off`, `GOSUMDB=off` and
`GOTOOLCHAIN=local` (no downloads and no toolchain switching), the cache is a read-only sandbox path, and `go.sum` hashes from the set (`go mod download` verifies
them against the module proxy and checksum database at install time) pin the contents. A Go module is arbitrary code that runs at build or run time inside the same
sandbox as learner code (no network, writes only to the work directory); installing the cache itself runs no module code.

NuGet sets follow the same rules: `dotnet restore restore.csproj --packages <set>/packages --source https://api.nuget.org/v3/index.json` runs in the set's own directory only (private
`DOTNET_CLI_HOME` and HTTP cache, no user or machine `NuGet.Config`), versions are exact pins in the shipped `restore.csproj`, and nothing is downloaded without the confirmation dialog. At grading time the
packages folder is a read-only sandbox path (`NUGET_PACKAGES`), restore is offline (`-p:RestoreSources=<packages>`) and the run never uses a container. The `.nupkg` payload is arbitrary code
(including MSBuild `build/*.props|targets` that execute during `dotnet build`); it runs inside the same sandbox as learner code (no network, writes only to the work directory), and installing performs no
package scripts (PackageReference restore has none). Listening sockets are denied, so ASP.NET Core exercises run under `TestServer` in memory.

## 5. Out of scope (today)

Protection against a malicious local administrator; kernel/hypervisor escapes; supply-chain
verification of the user's own toolchains (python, node, go …) — the app only detects them and
never runs installers without explicit confirmation.

## 6. Future public web version

Requires dedicated runner hosts with gVisor or Firecracker micro-VMs, per-job ephemeral VMs, no
shared filesystem, egress firewall, per-tenant quotas, and a separate BYOK trust model (keys
proxied server-side or kept client-side). Not implemented and not claimed.
