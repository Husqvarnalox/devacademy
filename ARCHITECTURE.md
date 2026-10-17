# DevAcademy — Architecture

Status: foundation implemented (domain, content engine, execution engine, evaluation, learning
path engine, AI layer, SQLite persistence, Avalonia shell). Sections marked **Planned** are
design decisions only — nothing in them exists in code. This file is the authoritative
description of module boundaries and contracts. Details: [RUNTIME_ARCHITECTURE.md](RUNTIME_ARCHITECTURE.md),
[AI_PROVIDERS.md](AI_PROVIDERS.md), [SECURITY_MODEL.md](SECURITY_MODEL.md),
[PLATFORM_SUPPORT.md](PLATFORM_SUPPORT.md), [CURRICULUM.md](CURRICULUM.md), [ROADMAP.md](ROADMAP.md),
[requirements digest](docs/requirements/ecosystem-expansions.md),
ADR: [docs/adr/0001-csharp-avalonia.md](docs/adr/0001-csharp-avalonia.md).

## 1. Product shape

DevAcademy is a **desktop-first, offline-first native learning IDE** (C# / .NET 10 LTS /
Avalonia 12). There is no server. Content, progress, and projects are local. Cloud is used
only for optional AI providers (BYOK) and, later, content updates and sync.

## 2. Layers and projects

```
src/
  DevAcademy.Domain          pure model + rules, no I/O, no dependencies
  DevAcademy.Content         Content Engine: loads YAML+Markdown packages into Domain, validates them
  DevAcademy.Execution       Code Execution Engine: language adapters, sandboxes, limits. Knows nothing about lessons
  DevAcademy.AI              AI Integration Layer: IModelProvider + OpenAI/Anthropic/Gemini/OpenRouter HTTP clients
  DevAcademy.Application     use cases: catalog, learning path, evaluation, progress, reviews, hints, tutor
  DevAcademy.Infrastructure  SQLite repositories + migrations, OS secret store, app data paths, backup/export
  DevAcademy.LanguageServices LSP client + server host (JSON-RPC over stdio, registry, workspace mirror); no UI types
  DevAcademy.Debugging         DAP client + adapter registry/resolver + debug session (state machine); no UI types
  DevAcademy.Desktop         Avalonia MVVM presentation layer + composition root
  DevAcademy.Cli             headless tool: validate-content [--run-solutions], list, runtimes (used by make/CI)
tests/                       one xUnit v3 project per src project
content/                     curriculum packages (data, not code)
```

Dependency rule (arrows = "may reference"):

```
Desktop ─► Application ─► Domain
   │            ├──────► Execution (contracts)
   │            └──────► AI (contracts)
   └──► Infrastructure ─► Application (implements its repository interfaces), Domain, AI
Content ─► Domain          Cli ─► Content, Application, Execution, Infrastructure
```

* Domain, Application, Content, Execution, AI never reference Avalonia. A future
  ASP.NET Core host replaces only Desktop (and swaps SQLite for PostgreSQL in Infrastructure).
* Application defines repository ports (`IProgressRepository`, `ISubmissionRepository`,
  `ISettingsRepository`, `IContentRepository`, `IClock`); Infrastructure implements them.
* No microservices, no message bus, no server. User code always runs in a **separate child
  process**, never in the app process.

## 3. Domain model (DevAcademy.Domain)

| Entity | Key fields |
|---|---|
| `Track` | id, kind (Language/Framework/Algorithms/ComputerScience/MachineLearning), title, description, languageId?, status (Available/Planned), courseIds |
| `Course` | id, version (semver, curriculum version), trackId, languageId, title, description, level 0–6, ordered lessonIds, `requires` (course ids), optional `framework` metadata |
| `Lesson` | id (globally unique, e.g. `python.fundamentals.lists`), title, goal, difficulty, estimatedMinutes, prerequisites[], objectives[], related[], next[], tags[], theory (Markdown with required sections), facts[], quiz?, exercise?, challenge? |
| `Exercise` | id, languageId, starter files, entry file, evaluation (Io or Harness), test cases (public/hidden), progressive hints (1–4), reference solution files, mastery criteria |
| `TestCase` | id, title, stdin, expected stdout, comparison (Exact / IgnoreTrailingWhitespace / Tokens / Numeric±tol), hidden, failureHint |
| `Quiz` | questions (SingleChoice / MultipleChoice / ShortText / Numeric) with explanation; passThreshold |
| `Fact` | kind (Historical/Internals/Misconception/Performance/Security/RealWorld), text, sources[] (URLs; required for Historical/Internals) |
| `LessonProgress` | lessonId, state (NotStarted/InProgress/Completed), timestamps, quizBestScore, exercisePassed, attempts, failedAttempts, review schedule |
| `Submission` | id, lessonId, exerciseId, languageId, code, verdict, passed/total, duration, timestamp |
| `LearnerProfile` | goal, self-assessed level, depth, enrolled courses (several at once), preferred UI language |

Progress is keyed by lesson id, so switching languages or enrolling in several courses never
loses progress.

## 4. Content Engine

Content is data, loaded at runtime from `content/` (and later from installed packages in the
app data dir). Adding a lesson never requires recompiling the app.

```
content/
  catalog.yaml                 tracks (Available or Planned) → course ids
  courses/<course-id>/
    course.yaml                id, version, title, languageId, level, lessons (ordered)
    lessons/<NN-slug>/
      lesson.yaml              metadata, objectives, prerequisites, facts, quiz, exercise, tests
      theory.md                Markdown; sections marked with <!-- section: simple|precise|examples|mistakes|summary -->
      exercise/                starter + reference solution (+ harness file for Harness evaluation)
  schema/*.schema.json         JSON Schema for authoring tools (the C# validator is authoritative)
```

Validation (`devacademy validate-content`, `make validate-content`, CI):
unique ids; prerequisites/related/next exist; prerequisite graph is acyclic; required theory
sections present; every lesson with an exercise has ≥1 public and ≥1 hidden test; facts of kind
Historical/Internals have a source URL; internal links resolve; **reference solution passes all
tests and starter code does not** (when the runtime is installed); runnable code samples
(` ```python run` blocks followed by ` ```output`) produce the declared output.

Versioning: `course.version` is semver; progress references lesson ids, never file paths, so a
content update preserves progress. Removed lesson ids are kept in progress as orphaned history.

## 5. Code Execution Engine

See [RUNTIME_ARCHITECTURE.md](RUNTIME_ARCHITECTURE.md). Contracts in
`src/DevAcademy.Execution/Contracts.cs`:

* `ILanguageRuntime` — per-language adapter: detect runtime + version, produce a `BuildPlan`
  (compile steps + run command). Adapters: python, javascript, typescript, go, java, kotlin, cpp, rust,
  csharp, php.
* `IProcessSandbox` — launches one process under `ResourceLimits` with a given
  `IsolationLevel` (ProcessLimits / OsSandbox / Container) and reports what was really applied.
* `ICodeRunner` — writes files into a fresh temp dir, compiles once, runs N times (one per
  test case), enforces a global concurrency limit, always deletes the dir and kills the
  process tree.

## 6. Evaluation (Application layer)

`IExerciseEvaluator` per evaluation kind:

* **Io** — run program per test case with stdin, compare stdout with the case comparison mode.
* **Harness** — content supplies a language-specific test harness that imports the learner's
  code and prints one line per case: `##DEVACADEMY## <nonce> {"id":"...","passed":true,"message":"..."}`.
  The nonce is random per run. A case missing from output = failed.
* **Command** — multi-file exercises graded by a real test tool (§17): `node --test` (JUnit report) or
  `go test -json`. The report is parsed and each declared case needs a matching passed test.

Rules: an exercise is passed only when **all** public and hidden tests pass; a successful exit
code alone never counts. Hidden test inputs/expected outputs are never shown in the UI (only
"hidden test N failed" + the author's failure hint). In the offline desktop app the files are
readable on disk; this is documented, not hidden.

## 7. Learning Path Engine (deterministic)

* Lesson state: `Locked` (some prerequisite not Completed) → `Available` → `InProgress` →
  `Completed`. Locked lessons can still be opened explicitly ("open anyway").
* Completion rule: exercise passed (if present) **and** best quiz score ≥ threshold (if quiz
  present). Theory-only lessons complete on explicit "Mark as read".
* Course progress = completed lessons / total lessons — shown as counts, never as "mastery %".
* Recommendation order: (1) due reviews, (2) InProgress lessons, (3) first Available lesson in
  course order, (4) `next` links of the last completed lesson.
* Reviews: fixed intervals 1, 3, 7, 16, 35 days after completion; a lesson with ≥3 failed
  attempts before passing gets its first review after 1 day regardless.

## 8. AI Integration Layer

Optional. See [AI_PROVIDERS.md](AI_PROVIDERS.md). `IModelProvider` with dynamic model discovery,
manual Model ID, credential validation, streaming. Keys live in the OS secret store only. AI
never marks exercises as solved and never runs commands or writes files on its own.
Without AI, hints come from content (`hints` 1–4 in lesson.yaml).

## 9. Persistence

SQLite (`Microsoft.Data.Sqlite`) in the per-OS app data dir; numbered SQL migrations embedded in
Infrastructure, applied at startup inside a transaction; WAL mode. Stores settings, profile,
progress, submissions, quiz attempts, editor drafts, AI request metadata. User projects are plain
directories. Export/import of progress = versioned JSON (no secrets). Backup = SQLite online
backup to a file.

## 10. Presentation (Avalonia, MVVM)

CommunityToolkit.Mvvm view models, compiled bindings, Fluent theme with light/dark. Main
screens: Onboarding, Catalog, Roadmap, Lesson workspace (navigation | theory | editor, console
and tests below; GridSplitter-resizable), Problems, Dashboard, Settings (General / Runtimes /
AI Providers). Editor: AvaloniaEdit + TextMate grammars. Markdown: Markdig → native Avalonia
controls (no WebView).

### Exercise variants, problems, visualization

An exercise may declare `variants:` (one per language, shared tests); drafts are keyed `exerciseId@lang`
(bare id for classic exercises), progress stays keyed by lesson. `ProblemService` lists exercise lessons of
Algorithms-kind tracks and gates the editorial/complexity (shown after solving or explicit confirmation).
`DevAcademy.Application.Visualization` generates sorting traces; Desktop renders them (`SortingVisualizerView`),
embedded from theory via a ```` ```visualize sorting algorithm=merge ```` block.

## 11. Learning Paths and cross-course knowledge reuse

**Implemented.** `content/paths/<id>.yaml` holds `id, title, description, goal, targets[]` (target course
ids). The path engine derives the required courses by walking `course.requires` (must exist, acyclic —
the validator checks the course graph together with lesson prerequisites). A path is therefore a view over
the dependency graph, not a hand-ordered list; two paths that share a prerequisite course share its progress,
because progress is keyed by lesson id.

* Shared topics (algorithms, SQL, networking, statistics, linear algebra, architecture) live in their own
  courses/tracks and are referenced via `requires` / lesson `prerequisites`; specializations add
  language-specific *exercise variants* (`variants:` in `lesson.yaml`) instead of repeating theory.
* **Assessment credit** (implemented): a lesson with a quiz and `assessmentFor: [lessonIds]` credits those
  lessons when passed (source `Assessment`) — the "placement check" that lets a learner skip mastered topics.
* **Stages and alternatives** (implemented): a path may add `stages: [{title, courses}]` (display grouping)
  and `alternatives: [[a,b]]` ("one of" groups). The chosen member is stored per path in settings
  (`path.alternative.<pathId>.<index>`; default = first member); unchosen members are not required.
  Validator: courses exist, alternative members exist, no cycles. Followed paths are stored in `paths.followed`.
* **UI:** onboarding offers paths for the chosen language (or none); Catalog has "Маршруты"; the path screen
  shows stages/courses/lessons, "уже освоено", assessment credit, "Следующий шаг" and a layered course graph.
* **Placement** ("Пропустить, если уже знаю"): offered only for quizzes with `assessmentFor`; passing credits
  the listed lessons (source `Assessment`), failing changes nothing.

## 12. Framework and library version metadata

**Implemented** in `course.yaml` (`framework:` block, schema in `content/schema/course.schema.json`):
`id`, `version`, `docs[]` (https only — validated), `compatibility`, `deprecated[] {feature,status,note}`,
`migrationNotes` (Markdown). `course.version` is the *curriculum* version and is independent of
`framework.version`; the runtime version (e.g. Python) is `languageId` + the runtime probe.

Rules: content updates never need an app rebuild; a new major framework version never replaces examples
automatically — a new course version is authored and its reference solutions are run against the new
framework first (`validate-content --run-solutions`). The course header renders `framework`
metadata when present. **Planned:** warning when the detected toolchain version is outside `compatibility`.

## 13. Comparison mode — Implemented

`content/comparisons/<id>.yaml` (schema `content/schema/comparison.schema.json`, authoring guide in
`docs/content-authoring`): subjects (course/framework, with version), criteria rows, Markdown cells per
subject, summary and https sources. Loaded by `SideContentLoaders`, validated by `SideContentValidator`
(every subject has every criterion; no verdict fields). UI: Catalog "Сравнения" list and a grid page with
Markdown cells; narrow windows show one subject at a time with a picker (heuristic threshold
`200 + 260*subjects` px). Authored: `python-web-frameworks`, `node-backend-frameworks`.

## 14. Project Workspaces — Implemented (Phase 3b)

A separate top-level screen ("Проекты") for real projects, deliberately **not** the sandboxed lesson runner.

* **Projects** are plain user-chosen directories or are created from `content/templates/<id>/template.yaml`
  (`ProjectTemplate`; python-cli, node-cli, go-cli, rust-cli, and the Node web templates react-vite-ts, vue-vite-ts, express-api-ts, nestjs-api). `TemplateInstaller` copies files and substitutes
  `{{name}}`; `postCreate` commands are only displayed (a button types them into a terminal without Enter).
  SQLite (migration 0003) stores only the recent-path list and a per-project session JSON (open tabs, active tab,
  chosen Python env) — never file contents.
* **Terminal** (`DevAcademy.Terminal`): Porta.Pty (MIT) gives the PTY (forkpty / ConPTY); a custom VT emulator
  (`TerminalEmulator`, per-row `Version` for dirty tracking, 5000-line scrollback) feeds the Avalonia
  `TerminalControl`. Shell = `$SHELL` (zsh/bash/sh fallback) or pwsh/powershell/cmd. Runs in the project dir with the
  user's environment and **no sandbox**; the UI says so. Kill = SIGHUP then SIGKILL to the process group.
* **Run / Test** buttons type the language command (`python main.py`, `node --test`, `go test ./...`, `cargo run` ...)
  into a terminal as visible text. Nothing is executed by the app itself and nothing typed by an AI is ever executed.
* **Editor**: one tab per file (AvaloniaEdit `TextDocument`, dirty = undo stack differs from the saved state), two
  groups (split), atomic save (temp + rename) preserving BOM, dominant line ending and Unix mode, SHA-256
  fingerprint to tell own saves from external edits (watcher -> Reload / Keep prompt), find/replace via the
  AvaloniaEdit search panel, go to line, session restore.
* **Explorer**: lazy tree, ignore rules (.git, node_modules, .venv, target, bin, obj) with a toggle, create/rename/
  delete (OS trash; permanent delete only after an explicit warning), file-name search and cancellable text search
  that skips binary/large files.
* Python projects: env selector (system, project `.venv`, managed envs from the Python Environment Manager); creating
  `.venv` and installing `requirements.txt` happen only after a confirmation showing the exact command.
* **Node web templates**: `template.yaml` may carry `scripts: {dev, test, build}` (package.json script names). The content loader
  checks offline that every named script exists in the template's `package.json` and that each `postCreate` entry is a single line
  (a newline typed into a terminal would execute it). `postCreate: [npm install]` is typed without Enter; the user's Enter is the consent
  (it needs the network). Versions are pinned exactly; the Vue template pins TypeScript 6 because `vue-tsc` has no TypeScript 7 JS API.
* **JS/TS buttons** (`ProjectWorkspaceViewModel.Node.cs`): when the project has a `package.json`, a row shows Install / Dev / Test / Build
  for the package manager read from the lockfile (`JsProjects.Detect`: pnpm-lock.yaml, yarn.lock, package-lock.json; then the
  `packageManager` field; fallback npm). Buttons are disabled when the script is missing (`dev` falls back to `start`). Install asks for
  confirmation showing the exact command; each click types a visible `<pm> run <script>` and nothing runs in the background.
* **Dev server**: Dev opens a dedicated terminal tab (so Test/Build never type into the server's stdin). `ITerminalSession.Output` exposes
  the raw decoded output; `DevServerUrlDetector` strips ANSI, looks only at complete lines and recognises local http(s) URLs
  (localhost, 127.0.0.1, 0.0.0.0 mapped to localhost, [::1]) from Vite, Express, Nest, Next. The first URL shows "Открыть в браузере" and a
  "Порт N: слушает" indicator (derived from output and process liveness; no port probing). Stop sends Ctrl+C to that terminal.
* **Web preview** is a separate module behind `IWebPreview` (`Services/WebPreview.cs`). Implemented: `SystemBrowserWebPreview` (default
  browser through `ILauncherService`, loopback http(s) URLs only). **Planned, not implemented:** an embedded native webview in its own
  window/panel as another `IWebPreview`; the main UI never becomes a WebView.
* An `IWorkspaceRunner` with sandbox limits remains future work.

## 15. Python Environment Manager and ML Runtime Manager — Implemented

Execution (no UI types): `Python/PythonInterpreters.cs` (detector), `Python/PythonEnvironmentManager.cs`,
`Ml/MlRuntimeManager.cs`. Tools run through the `IToolRunner` seam, so tests substitute canned output.
Application: `PythonEnvironmentService` (active-env setting `python.activeEnvironment`, requirement check,
accelerator gate, `IExerciseEnvironmentResolver`). Desktop: Settings -> Runtimes ("Python-окружения",
"ML-ускорение").

**Python Environment Manager.** Interpreters come from PATH, Homebrew / `/usr/local/bin`, pyenv, `uv python list`
and the Windows `py -0p`; each candidate is probed with `-I -c` (version, implementation, free-threaded, arch).
Managed environments live in `<data>/python-envs/<name>` (venv or uv). Delete is refused unless the resolved
path is a direct child of that root, not a symlink, and has `pyvenv.cfg`. Installs (package list,
requirements.txt, pyproject `[project].dependencies`) are built as a *plan* (exact command line, `--` before
specs, option-like / URL / path specs rejected), shown to the user with the PyPI download notice, and executed
only after confirmation, streaming and cancellable, into the chosen managed env only (PIP_*/PYTHON* variables
are scrubbed; never the system interpreter).

**Exercise requirements.** `requires: { python: ">=3.11", packages: ["numpy>=2"] }` and
`accelerator: none|optional|required` on a lesson or an exercise (exercise wins). Before a run the resolver
probes the env via `importlib.metadata` (nothing is imported); a miss gives `Verdict.RuntimeMissing` with
Settings instructions, never a wrong answer. A `required` accelerator that is unusable disables Run/Submit
with an explanation; `optional` only shows a note.

**Package sets.** Packages are installed per library, not wholesale: `tools/content-packages/index.json` (shipped as `content-packages/` next to the
executable) lists sets (node `react`, `typescript`, `express`; python `numpy`, `pandas`, `fastapi`, `django`; go `gomlx`). `ContentPackageIndex` (Domain,
pure) resolves the exercise's required packages to the smallest single set of the right language that covers them; the ContentValidator
rejects an exercise no set covers. Node sets live in `<data dir>/node-content/<set>/` (`npm ci --ignore-scripts --no-audit --no-fund` in that
directory only; a run mounts just that set's `node_modules` read-only). A Python set is its pinned requirements file installed through the
normal plan/consent flow into the ACTIVE managed environment (never auto-created). `ExerciseRunPlan`/`NodeRunPlan` carry `InstallSet` so the lesson
can offer «Установить пакеты: …»; the dialog lists packages with versions, the exact command and the network notice. Nothing is installed implicitly.
A Go set is the same idea for Go modules: `GoEnvironmentService` (`go mod download` into `<data dir>/go-content/<set>/pkg/mod`), `GoRunPlan` carries `InstallSet`, and
`PackageSetInstallFlow` serves Python, Node and Go alike; Settings has a Go section (`GoEnvironmentViewModel`) with Install/Remove per set.
A NuGet set is the same idea for C# (`NuGetEnvironmentService`: `dotnet restore` of the set's pinned `restore.csproj` into `<data dir>/nuget-content/<set>/packages`), `NuGetRunPlan` carries `InstallSet`,
`PackageSetInstallFlow` serves it too, and Settings has a NuGet section (`NuGetEnvironmentViewModel`). `CSharpRuntime` also supports **project mode**: exactly one root `.csproj` among the exercise files is
built as a project and its assembly run, which is how multi-file ASP.NET Core exercises (a fixed TestServer driver plus the learner's files) are graded.

A Maven set is the same idea for Java (`MavenEnvironmentService`: jars from the committed `lock.json` downloaded from repo1.maven.org and verified by sha256 into `<data dir>/maven-content/<set>/lib`),
`MavenRunSelection` rides on `ExecutionRequest.Maven`, `JavaRuntime` then compiles and runs with `-cp lib/*` (library mode), `PackageSetInstallFlow` serves it too, and Settings has a Maven section
(`MavenEnvironmentViewModel`).

**Run isolation.** A selected env replaces the auto-detected interpreter. The env directory is passed as
`ReadOnlyPaths`: the macOS profile ends with `(deny file-write* (subpath env))` (last rule wins), bwrap uses
`--ro-bind`. Container isolation is downgraded to OsSandbox (with a note) because the env is a host path.

**ML Runtime Manager.** Hardware report: OS, arch, CPU, cores, RAM; NVIDIA via `nvidia-smi` (never run on
macOS), ROCm only on Linux (Windows reports "ROCm on Windows: limited/HIP SDK — verify"), Metal on Apple
Silicon. Framework probes (torch, tensorflow, jax, onnxruntime) run inside a chosen env, only for installed
frameworks, with statuses Available / NotAvailable(reason) / NotInstalled / Unknown. An informational
accelerator note is shown before an install of accelerator-sensitive packages; there is no version table.
CPU-first: nothing requires a GPU unless an exercise says `required`. See [PLATFORM_SUPPORT.md](PLATFORM_SUPPORT.md).

**Gradient Descent Laboratory.** `Application/Visualization/GradientDescent.cs` (pure: quadratic with
conditioning, Rosenbrock, 1D double-well; GD, momentum, Adam; trace) and a Desktop view reachable from theory
via ```` ```visualize gradient-descent function= optimizer= lr= ````.

## 16. Notebook workspace — Implemented

Cell-based work over the real **Jupyter messaging protocol 5.3**; no custom kernel, no WebView.
`src/DevAcademy.Notebooks` has no UI types:

- **Transport — own ZMTP 3.0 (NULL), no ZeroMQ package.** NetMQ is LGPL-3.0. The desktop app ships as a
  self-contained `PublishSingleFile` (MIT) bundle, which statically merges the assembly and makes LGPL's
  relink/replace obligation impractical, so a minimal ZMTP 3.0 NULL subset was implemented instead
  (`Transport/ZmtpSocket.cs`: greeting, READY with Socket-Type/Identity, short/long/multipart frames, SUB
  subscription frames; DEALER/SUB/REQ-emulated heartbeat). No dependency, no license or native-binary issue.
  Tested against raw bytes, an in-process fake kernel, and real ipykernel.
- **Protocol** (`Protocol/`): connection file (tcp 127.0.0.1, 5 random free ports, random HMAC-SHA256 key,
  mode 0600 in a private temp dir), `<IDS|MSG>` wire format with HMAC signing and constant-time verification.
- **Kernel** (`Kernel/`): `KernelSession` (shell/iopub/stdin/control/heartbeat loops; execute completes on
  execute_reply + iopub idle; interrupt = SIGINT on POSIX, `interrupt_request` otherwise; restart; shutdown with
  kill fallback; heartbeat-based dead-kernel detection), `PythonKernelHost` (`python -m ipykernel_launcher -f ...`
  with the selected environment's interpreter, `JPY_PARENT_PID` so it exits with the app).
- **Provisioning:** `pip install ipykernel==<pin>` only into a virtual environment, only on explicit user
  action, with the exact command shown in the confirmation. Never the system interpreter.
- **nbformat v4** (`Nbformat/`): read/write of code/markdown/raw cells and stream/execute_result/display_data/
  error outputs; unknown fields preserved; byte-for-byte round trip of files written by Python's `json`;
  atomic save.
- **UI:** `ProjectWorkspaceViewModel.Notebook` overlay (opens for `.ipynb`; New notebook button), code cells use
  `CodeEditorControl`, markdown cells `MarkdownRenderer` with an edit toggle, ANSI-coloured text/tracebacks, PNG,
  Markdown and JSON outputs (HTML falls back to text/plain), variable inspector (silent `execute_request` with
  `user_expressions`; only primitives, containers and ndarray/DataFrame summaries), execution history.
  Shift+Enter run and advance, Ctrl/Cmd+Enter run.
- **Security:** the kernel is an ordinary process with the user's privileges and network (no sandbox); the
  notebook screen says so permanently.

Limitations: no language server inside cells; LaTeX is shown as source (math rendering planned); `input()` gets an
empty reply (allow_stdin=false); HTML/JS outputs are not rendered; interrupt via `interrupt_request` (Windows)
is covered by the fake kernel only; one notebook per project window; no external-change watching for `.ipynb`.

## 17. Command evaluator — Implemented

`evaluation: command` runs a real test tool on a multi-file exercise and maps its machine-readable report to
lesson test cases (`CommandEvaluator`, `JUnitReportParser`, `GoTestJsonParser`).

* **Workdir**: a fresh directory holding the learner's files plus the hidden test files from `tests_dir`
  (copied under `testsTarget`, overriding learner files at the same path). The learner cannot pre-create the report file.
* **Command**: `command.run` is an argv. `CommandPolicy` allows only the language tool (`node` for javascript,
  `go` for go); the runtime (`ICommandRuntime`) supplies the executable and environment. Runs in `OsSandbox` without
  network; limits are `ResourceLimits.DefaultCommand` with a per-exercise `timeoutSeconds` (max 60). Container isolation is
  downgraded to OsSandbox for commands.
* **Report**: `junit` (read from `reportFile` inside the workdir through `IPreparedProgram.TryReadFile`) or `go-test-json`
  (stdout).
* **Verdict**: a declared case passes only if a test matched by its `match` (name, `suite > name` or `classname.name`,
  trailing `*` = prefix) reported pass. Not run or skipped = failed. A non-zero exit with no report, or a build failure,
  is `CompileError` with the tool output. Hidden cases never show assertion text.
* **Desktop**: the editor shows one tab per learner file (the tests dir is excluded), drafts per file; the Run button
  and console are hidden (nothing to run), only "Submit" grades.
* `--run-solutions` checks that the solution passes all cases and the starter does not.

## 18. Language servers (LSP) — Implemented

Completion, hover, signature help, go to definition, formatting and diagnostics come from **real language servers**
(clangd, gopls, pyright, typescript-language-server, rust-analyzer, jdtls, csharp-ls, intelephense, kotlin-language-server).
The app contains no parser or completion engine; `DevAcademy.LanguageServices` (no Avalonia types) is a thin client + host:

* `JsonRpc/`: Content-Length framing (partial reads, several messages per read, UTF-8 byte lengths) and a JSON-RPC 2.0
  connection (one reader loop, one ordered writer loop; request correlation, `$/cancelRequest`, server-to-client requests answered,
  protocol errors logged). `Protocol/`: minimal typed LSP records, UTF-16/UTF-8/UTF-32 position conversion honouring the negotiated
  `positionEncoding`, incremental change computation (AvaloniaEdit reports a change after it happened, so ranges are derived from the
  after-buffer). `LspClient`: initialize (snippetSupport off), didOpen/Change/Save/Close, completion(+resolve), hover, definition,
  signatureHelp, formatting, diagnostics stream, shutdown/exit with a kill timeout.
* `Servers/`: the registry is **data** (`LanguageServerRegistry.Defaults`), a user override command per language (Settings), detection
  (PATH, `~/go/bin`, `~/.cargo/bin`, Homebrew, `.dotnet/tools`, the runner cache `language-servers/node_modules/.bin`). npm-based servers
  (pyright, typescript-language-server+typescript, intelephense) are installed **only on an explicit user action** that first shows the
  exact `npm install` command (same consent pattern as the TypeScript types provisioner); nothing is downloaded automatically.
* `Hosting/`: `LanguageServerManager` starts one session per (server, workspace root) lazily, shared by all editors, stopped when idle
  or on exit. A crashed server is restarted with backoff (max 3, documents re-opened, stale-version diagnostics dropped); the state
  (Starting/Ready/Restarting/Error/NotInstalled/Disabled) is visible in the UI.
* `Workspace/WorkspaceMirror`: lesson exercises are in-memory buffers, but servers need files. Each exercise/language has a stable
  directory `<app data>/lsp-workspaces/<exerciseId>/<lang>` that mirrors the editor files (debounced writes) plus scaffolding
  (`go.mod`, `compile_flags.txt -std=c++20`, `pyrightconfig.json`, `tsconfig.json`, ...). Project workspaces use the real project root.
* Desktop: `LanguageIntelligenceService` (settings `lsp.enabled`, `lsp.command.<language>`), `CodeEditorControl.LspTarget` + `EditorLspBinding`
  (squiggles from theme brushes, completion window with trigger characters and Ctrl/Cmd+Space, hover popup rendered by the existing
  markdown renderer, signature help, F12 / Cmd(Ctrl)+click, Shift+Alt+F), a Problems tab (lessons) / list (projects), the status item
  "LSP: gopls ✓ / не установлен (как установить) / ошибка" and Settings → Runtimes → language servers.
* Known limits: only files open in an editor are known to the server (others are read from disk); other-project-file definitions open a
  tab, files outside the project show a read-only peek; no rename/code actions/semantic tokens (TextMate keeps highlighting).

## 19. Debugger (DAP) — Implemented

* `DevAcademy.Debugging` (no UI types): `Dap/` is the transport (Content-Length framing shared with the LSP `MessageReader`/`MessageWriter`,
  `seq`/`request_seq` correlation, events, reverse requests; `runInTerminal` is answered "not supported") and a minimal typed client
  (initialize, launch, setBreakpoints incl. conditions, configurationDone, threads, stackTrace, scopes, variables (lazy), evaluate,
  continue/next/stepIn/stepOut/pause, disconnect/terminate). `Adapters/` is the registry **as data** (language -> adapter command, how it
  is provisioned, launch arguments) and `AdapterResolver`. `Session/DebugSession` runs the handshake
  (`initialize` -> `launch` (not awaited) -> wait `initialized` -> `setBreakpoints` -> `configurationDone` -> await `launch`) and exposes an
  ordered event stream (StateChanged, Stopped, Output, Terminated); `StopAsync` also ends with Terminated, so there is one end path.
* Adapters: python = `python -m debugpy.adapter` with the **selected env's interpreter**; cpp/rust = `lldb-dap` (`xcrun -f lldb-dap` on macOS,
  PATH elsewhere); go = `dlv dap` over TCP (`--listen=127.0.0.1:0`, port parsed from stdout). Provisioning is never implicit: debugpy is
  `pip install debugpy` into that env only (venv probe + `PIP_REQUIRE_VIRTUALENV=1`, never the system interpreter); Delve is
  `go install github.com/go-delve/delve/cmd/dlv@<pinned>` into the runner-cache GOBIN. The UI shows the exact command and asks first.
  javascript/typescript = vscode-js-debug v1.140.0 (`node dapDebugServer.js 0 127.0.0.1`, TCP on loopback; the release tarball is
  downloaded by a shown `curl` script, **sha256-verified before unpacking**, into `<runner cache>/debug-adapters/js-debug/<version>`). It is a
  multi-session adapter: the root session answers `launch` with a `startDebugging` reverse request and `DebugSession` opens a second
  connection to the same port for the real debuggee session (`AdapterProcess.OpenAdditionalConnectionAsync`). java = `java-jdi`, a ~800-line
  JDI DAP adapter whose **source ships in the app** (`tools/debug-adapters/java`, embedded resource) and is compiled locally with the user's
  `javac` into the runner cache on first use (no download; needs a JDK 17+ with the `jdk.jdi` module); it spawns the debuggee with a
  loopback-only JDWP socket. csharp = netcoredbg 3.2.0-1092 (Samsung, MIT, `--interpreter=vscode` over stdio), zip sha256-verified, **installed
  automatically on macOS arm64 only** (the one platform tested; other platforms get a manual instruction, labelled untested). Microsoft's vsdbg
  is not used (licence).
* Debug builds: `IDebugBuildRuntime` gives cpp `-g -O0`, rust `-g -C opt-level=0`, go `-gcflags=all=-N -l`, java `javac -g`, csharp
  `dotnet build -c Debug -p:Optimize=false` (portable pdb; file-based app or the single project); javascript/typescript need no build (TypeScript
  runs through Node type stripping, which keeps line numbers, so no source maps). Native builds return a binary; the others return the runtime plus
  the rest of the run command in `DebugBuildResult.RunArguments`. The normal run plan is unchanged.
  `DebugBuilder` builds into a throw-away directory.
* **Sandbox policy**: debug runs are NOT sandboxed (the debugger needs ptrace/attach and the program runs with the user's rights). The panel
  says "отладка запускает программу без песочницы". Debugging is available in project workspaces and lesson Practice "Run"; it is never used
  for grading or command-graded exercises.
* Desktop: `DebugService` (adapter resolution, launcher, UI marshalling), `DebugViewModel` + `IDebugHost` (`ProjectDebugHost`,
  `LessonDebugHost`), `BreakpointStore`, `EditorDebugTarget` + `BreakpointMargin` + `ExecutionLineRenderer` on `CodeEditorControl`
  (gutter click toggles, breakpoints follow edits, current-line highlight), `DebugPanelView` (toolbar F5/F10/F11/Shift+F11/Pause/Stop,
  Call Stack, Variables (lazy tree), Watch, Debug Console with REPL, adapter status/install). Breakpoints persist per file in
  `ProjectSession.Breakpoints` (projects) and in the settings key `debug.breakpoints.<exercise>.<lang>` (lessons).
* Verified here (macOS arm64): lldb-dap on C++ (breakpoint in a loop, variables, step, exit code), debugpy in a temp venv, dlv in a temp
  GOBIN, and end-to-end tests (breakpoint, locals, step, continue, evaluate, run to the end) against the real vscode-js-debug (JavaScript and
  TypeScript, Node 22), the JDI adapter (step in/out/over, deferred breakpoint, uncaught exception, packages) and netcoredbg (file-based C# app);
  they skip with a reason when the adapter is not installed. Explicit-only tests: `DEVACADEMY_ALLOW_NETWORK_INSTALL=1` for the debugpy/dlv/js-debug/
  netcoredbg installs (`DEVACADEMY_ADAPTER_INSTALL_TO_RUNNER_CACHE=1` installs into the real cache so the integration tests run).
* Known limits: No debuggee stdin. Delve, vscode-js-debug send no `exited` event and netcoredbg always reports exit code 0, so no exit code is
  shown for go, javascript, typescript and csharp. The Java adapter is small: no hot code replace, no conditional or logpoint breakpoints, watch
  expressions are variable paths (`a.b[0]`, `.length`), not arbitrary expressions. C#: Linux/Windows/macOS x64 are not tested and not
  auto-installed; project builds write `obj/` next to the sources. Go debugging builds flat source files only (no
  sub-packages). macOS shows developer-tools permission prompts for lldb. Windows/Linux unverified.

## 20. Not implemented today

Web version, accounts, sync, PostgreSQL, PTY terminal, project workspaces, Python
environment manager (in progress), ML runtime manager, notebooks, Windows OS
sandbox, installers/auto-update, comparison mode. Framework and ML courses do not exist yet (catalog
tracks are `planned`). See [ROADMAP.md](ROADMAP.md); the UI shows these as **Planned**.
