# Content authoring guide

Content is data: YAML + Markdown + exercise files under `content/`, loaded at runtime. Adding or changing a lesson never
requires rebuilding the app. The **C# validator is authoritative**; JSON Schemas in `content/schema/` are editor aids
(they can be stricter or looser in corners). Loader: `src/DevAcademy.Content` (`ContentLoader`, `ContentValidator`,
`TheoryParser`). Unknown YAML keys are rejected, so typos show up as diagnostics.

Workflow: edit → `make validate-content` (= `devacademy validate-content --content content --run-solutions`) → fix every
error → commit. While authoring one course, `--only <course-id or lesson-id prefix>[,...]` limits `--run-solutions` to those
lessons (structural validation still covers all content). Pedagogical rules: [../curriculum-standards.md](../curriculum-standards.md). `--run-solutions` additionally runs each reference solution against its tests and each starter (must fail),
and executes runnable theory samples; it needs the language toolchain (a missing runtime is a warning and that language's solutions/samples are skipped) and is not run when content errors exist.

## 1. Layout

```
content/
  catalog.yaml
  paths/<path-id>.yaml
  schema/*.schema.json
  courses/<course-id>/                  directory name == course id
    course.yaml
    lessons/<NN-slug>/                  names listed in course.yaml `lessons`
      lesson.yaml
      theory.md
      tests/                            optional data files for stdinFile/expectedFile (§5)
      exercise/                         (or challenge directories, same shape)
        starter/    reference starter files given to the learner
        solution/   reference solution (never shown by default)
        harness/    only for evaluation: harness
        tests/      only for evaluation: command (hidden test files)
      editorial.md                      optional, path set by `editorial:`
```

With variants (§5) the exercise directories become `exercise/<language>/{starter,solution,harness}`.

## 2. catalog.yaml

```yaml
tracks:
  - id: python                 # unique
    kind: language             # language | framework | algorithms | computer-science | machine-learning
    title: Python
    description: >-
      ...
    languageId: python         # optional
    status: available          # available | planned (default available)
    courses: [python-fundamentals]   # an available track needs >= 1 course; planned tracks list none
```

A track is `planned` until a real course exists: **no stub courses**. A course's `track` must equal the track listing it.

## 3. course.yaml

```yaml
id: nodejs-first-steps         # == directory name
version: 1.0.0                 # curriculum version (semver); NOT the framework version
title: ...
description: ...
languageId: javascript
level: 1                       # 0-6, orientation only (see CURRICULUM.md)
track: nodejs
requires: [javascript-first-steps]   # courses this one builds on; must exist; no cycles, not itself
lessons: [01-runtime-process-and-stdin, 02-...]   # directory names, in order
framework:                     # optional, for framework/library courses
  id: react
  version: "19.x"
  docs: [https://react.dev/]   # https only (validated)
  compatibility: "Node >= 20"
  deprecated:
    - { feature: "...", status: deprecated, note: "..." }
  migrationNotes: |
    Markdown.
```

Rules: a new major framework version means a **new curriculum version** authored and its solutions run against it; never
bump examples silently. `requires` edges combine with lesson `prerequisites` into one graph that must be acyclic.

## 4. lesson.yaml

```yaml
id: python.fundamentals.functions      # globally unique, dotted
title: ...
goal: one sentence
difficulty: intro | easy | medium | hard | expert    # default intro
estimatedMinutes: 45
prerequisites: [python.fundamentals.loops]   # lesson ids; must exist; acyclic; drives Locked/Available
related: [...]          # lesson ids
next: [...]             # lesson ids, used by recommendations
seeAlso: ["free text for topics that do not exist yet"]
objectives: [...]       # what the learner can do afterwards
masteryCriteria: [...]  # observable, verifiable statements
tags: [...]
assessmentFor: [lesson ids]   # check: completing this lesson (exercise and quiz) credits those lessons; needs a quiz or an exercise; not self
formerIds: [old lesson ids]   # ONLY for a rename with equivalent content: progress under these ids is carried over (see §4a)
concepts:                     # what the lesson teaches and relies on (see §4a)
  introduces: [go.for-loop]
  uses: [go.variable, go.println]
facts:
  - kind: internals | historical | misconception | performance | security | real-world
    text: ...
    sources: [https://...]    # >= 1 https URL REQUIRED for historical and internals
quiz:
  passThreshold: 0.7          # (0, 1]
  questions:
    - id: q1                  # unique within quiz
      type: single | multiple | short | numeric
      prompt: ...
      options: [...]          # single/multiple: >= 2
      answer: 1               # single: index; multiple: [indices]; short: [accepted strings]; numeric: number
      tolerance: 0.01         # numeric, >= 0
      explanation: ...
complexity:                   # algorithm problems; shown after solving
  time: O(n log n)
  space: O(n)
  explanation: Markdown
editorial: editorial.md       # shown only after solving or explicit confirmation; requires an exercise
exercise: { ... }             # §5
challenge: { ... }            # optional harder exercise, same shape
```

Lesson completion: exercise passed (if any) **and** best quiz score ≥ threshold (if any); theory-only lessons use "Mark as read".

## 4a. Concepts, former ids, the `programmer` section

* **`concepts`.** `introduces` lists the concepts this lesson teaches, `uses` the ones its example, exercise or quiz needs.
  The validator reports an ERROR when a used concept is introduced neither by the lesson nor by a lesson it depends on
  (its prerequisites, transitively, plus every lesson of the courses its course `requires`). A concept introduced again
  by a lesson that already depends on its introduction is a warning. Ids: `<language>.<concept>`, no spaces. Lessons
  without `concepts` are not checked.
* **`formerIds`.** When a lesson is renamed **and still teaches and checks the same skill**, list the old ids. At startup
  `ProgressMigrationService` copies the stored progress of a former id to the new id if the new id has none (idempotent;
  the old record is kept as history). Never use it for new material that only looks similar: then the old progress stays
  history and credits nothing. Errors: a former id that is a current lesson id, the lesson's own id, or claimed twice.
  Keep exercise ids unchanged on a rename so editor drafts survive.
* **`programmer` section** (`<!-- section: programmer -->`): a 5-15 line summary for people who know another language.
  Learners with prior knowledge 1+ open the lesson on it; absolute beginners see it last. See docs/learning-levels.md.
* **Progression warnings** (heuristics): in a level-0 course, a lesson after the first without prerequisites, or with
  difficulty above `easy`; a prerequisite from a course of a higher level.

## 5. Exercises

```yaml
exercise:
  id: python.fundamentals.functions.exercise   # unique
  title: ...
  prompt: |                  # Markdown
    ...
  languageId: python         # default: course languageId
  entryFile: functions_task.py
  starter: exercise/starter  # defaults: exercise/starter, exercise/solution, exercise/harness
  solution: exercise/solution
  evaluation: io | harness | command    # default io
  harness: exercise/harness  # harness only
  harnessEntry: check.py     # harness only; the file executed instead of the entry
  tests_dir: exercise/tests  # command only (default exercise/tests)
  command: {...}             # command only, see below
  hints: [h1, h2, h3, h4]    # 1-4 progressive hints (direction -> location -> concrete -> partial explanation)
  tests: [...]
```

**Tests.** Each exercise needs >= 1 public and >= 1 hidden test (`hidden: true`); ids unique.

```yaml
tests:
  - id: basic
    title: ...
    stdin: "3\n"             # io (or stdinFile, see below)
    expected: "6\n"          # required for io (or expectedFile)
    # stdinFile: tests/big.in      # alternative to stdin: path relative to the lesson directory
    # expectedFile: tests/big.out  # alternative to expected
    # match: "suite > name"        # command only
    compare: trim            # exact | trim (default; ignore trailing whitespace) | tokens | numeric
    tolerance: 1e-6          # numeric
    hidden: false
    hint: shown on failure (also for hidden tests)
```

Hidden inputs and expected output are never displayed; the learner sees "hidden test N failed" plus your `hint`. Offline, the
files are readable on disk — that is documented, not hidden.

**Data files.** `stdinFile` / `expectedFile` replace the inline `stdin` / `expected` (mutually exclusive per field; use
a file when the value exceeds about 4 KB). Paths are relative to the lesson directory, must stay inside it and exist; each
file is at most 16 MB and valid UTF-8 (a BOM is stripped). Convention: `tests/<test-id>.in` and `tests/<test-id>.out`.
The validator reports missing, oversized, non-UTF-8 and escaping paths.

**io evaluation**: the program is run once per test with `stdin`; stdout is compared per `compare`.

**harness evaluation**: files in `harness/` are copied next to the learner's files and `harnessEntry` is executed once
(the harness `import`s the learner module). It must print one line per test case:

```
##DEVACADEMY## <nonce> {"id":"clamp-basic","passed":true,"message":""}
```

`<nonce>` is read from the environment variable `DEVACADEMY_NONCE` (random per run). Lines with a wrong nonce are ignored; a
test id missing from the output counts as failed; `tests:` ids in the YAML must match the ids the harness reports.
Harness file names must not collide with starter/solution files. Catch exceptions per case and report them as failures. See
`content/courses/python-fundamentals/lessons/06-functions/exercise/harness/check.py` for a complete pattern.

**command evaluation** (multi-file exercises graded by a real test tool; javascript and go only). The learner edits
every file under `starter/` in tabs; the hidden tests live in `tests_dir` and are copied into the work directory at
`testsTarget` (default `tests`; use `"."` to put them next to the sources, as Go requires).

```yaml
exercise:
  evaluation: command
  languageId: javascript
  entryFile: src/money.mjs
  starter: exercise/starter
  solution: exercise/solution
  tests_dir: exercise/tests
  command:
    run: ["node", "--test", "--test-reporter=junit", "--test-reporter-destination=report.xml",
          "--test-reporter=spec", "--test-reporter-destination=stdout", "tests/*.test.mjs"]
    report: junit               # junit | go-test-json
    reportFile: report.xml      # junit: file inside the work directory; go-test-json reads stdout
    timeoutSeconds: 30          # 1-60, default 20
    testsTarget: tests          # where tests_dir files are copied
  tests:
    - { id: parse, title: ..., match: "parsePrice > целая цена", hint: ... }
    - { id: edge, title: ..., match: "parsePrice*", hidden: true, hint: ... }
```

Rules: `run[0]` must be the language tool (`node`, `go`); no other executables; no network. A declared case passes only if a
reported test matches `match` (test name, `suite > test`, or `classname.name`; a trailing `*` is a prefix match) and passed:
unmatched = "тест не был запущен", skipped/todo = failed. Non-zero exit with no report (syntax error, `go build` failure)
is a compile error with the tool output. Hints for Node: `node --test tests/` does not work on Node 22, use a glob in quotes;
JUnit hides syntax errors, so add the `spec` reporter to stdout. Go: `["go","test","-json","-count=1","./..."]`, `go-test-json`,
`testsTarget: "."`, put a `go.mod` in the starter and solution. Test file names must not collide with starter files.
The validator requires at least one public and one hidden test, a non-empty `match` per test and a solution that passes
(`--run-solutions` also requires the starter to fail).

**Variants (several languages, one exercise).**

```yaml
exercise:
  id: algo.binary-search.exercise
  title: ...
  evaluation: io
  variants:
    python: { entryFile: main.py }                       # starter/solution/harness default to exercise/python/...
    javascript: { entryFile: main.js, starter: exercise/js/starter }
  tests: [...]               # shared by all variants; prompt and hints are shared
```

A variants exercise cannot also set top-level `languageId/entryFile/starter/solution/harness/harnessEntry`. Each variant
needs a non-empty starter containing its `entryFile`, and a solution containing it. Drafts are stored per
`exerciseId@language`; progress stays per lesson.

Validator checks per exercise: starter/solution exist and contain the entry file; harness entry exists; 1-4 hints; public and
hidden tests; no duplicate ids; `command` set when `evaluation: command`; data files resolve.

## 6. theory.md

Required section markers (each on its own line, non-empty body): `simple`, `precise`, `examples`, `mistakes`, `summary`.
Optional: `facts`, `deep-dive`. Other names produce a warning.

```markdown
<!-- section: simple -->
Plain-language explanation...
<!-- section: precise -->
...
```

* **Runnable samples**: a fenced block whose info string is `<lang> run`, optionally followed immediately by a block
  tagged `output`:

  ````markdown
  ```python run
  print(2 + 2)
  ```
  ```output
  4
  ```
  ````

  With `--run-solutions` each sample is executed and must produce the declared output.
* **Internal links**: `[text](lesson:python.fundamentals.loops)` — must resolve to an existing lesson id.
* **Visualizations**: a fenced block `visualize sorting algorithm=<bubble|insertion|selection|merge|quick>` embeds the
  step-by-step sorting visualizer; traces are generated from a real run of the algorithm, not hand-drawn. Only `sorting` exists;
  other `visualize` kinds are a validation error until implemented (ML visualizations are Planned).
* Facts of kind `historical`/`internals` need a source; do not state versions, dates or APIs you have not verified —
  "verify against current docs" is better than a confident error.

## 7. paths/<id>.yaml

```yaml
id: python-web
title: Python для веба
description: ...
goal: backend          # backend | frontend | fullstack | ml | general | ...
targets: [python-fundamentals]   # existing course ids (required)
stages:                # optional; backwards compatible
  - title: Основа
    courses: [python-fundamentals]
  - title: Веб
    courses: [web-django, web-fastapi]
alternatives:          # optional; "one of" groups
  - [web-django, web-fastapi]
```

The required courses and lessons are derived from `requires`/`prerequisites`, so a path is a view over the dependency graph.
Without `stages` the screen groups courses by dependency layer. A course appears in at most one stage.

**Alternatives.** Each group has at least two existing courses; the learner picks one (stored per path in settings, default is
the first member until a choice is saved). Unpicked members are shown but are not required. Errors: a member that is a target,
a member that requires another member of its group, a member required by a target or by a non-alternative stage course, a
duplicate member, or a member in two groups. Course dependency cycles are already errors.

**Placement check.** A lesson with `assessmentFor: [lesson ids]` and a `quiz` and/or `exercise` is offered as "skip if you
already know" on its course and on every path containing it. When its completion rule holds (exercise passed if it has one,
quiz at the threshold if it has one) the listed lessons are credited (source Assessment); failing changes nothing. Prefer a
short exercise plus a code-reading quiz: a quiz alone is weak evidence of a skill.

## 7a0. routes/<track>.yaml

How a track is entered and which competencies can be checked (docs/learning-levels.md). File name == track id.

```yaml
track: go
entry:
  beginner: go.first-steps.first-program      # level-0 learners start here (should be in a level-0 course, no prerequisites)
  programmer: go.programmers.syntax-and-control-flow   # learners who know another language
competencies:                                 # ordered from simple to advanced
  - id: basics
    title: Синтаксис, типы, условия и циклы
    description: ...
    check: go.check.basics                    # a lesson with assessmentFor (exercise + quiz recommended)
    after: []                                 # competencies whose checks are suggested first
```

Errors: unknown track or lesson, entry or check lesson outside the track's courses, a check without `assessmentFor`, a
check lesson used twice, unknown or cyclic `after`. Warnings: a check without an exercise, an entry lesson with
prerequisites, a beginner entry outside a level-0 course. Competency states (Confirmed / NeedsWork / NotChecked) are
computed from progress by `DiagnosticEvaluator`; a check's quiz and exercise follow the normal completion rule.

## 7a. comparisons/<id>.yaml

```yaml
id: python-web-frameworks          # must equal the file name
title: ...
subjects:                          # >= 2, unique ids
  - { id: django, title: Django, kind: framework, version: "5.2 LTS", docs: [https://docs.djangoproject.com/en/5.2/] }
criteria:                          # >= 1, unique ids
  - { id: scope, title: ..., description: ... }
cells:                             # EVERY criterion x subject must be filled (Markdown)
  scope:
    django: "..."
verdictGuidance: |                 # Markdown: when to choose which; never "X is best"
  ...
sources: [https://...]             # >= 1, https only
```

`kind` is `library | framework | language | runtime`. Subject `docs` are required https links; a subject without `version`
or a guidance text that names none of the subjects produces a warning. Only state claims you have verified against the official
docs, and give no benchmark numbers without a source. The Catalog lists comparisons ("Сравнения"); the page renders a native
grid (criteria rows x subject columns) and, when narrow, one subject at a time with a picker. Schema: `content/schema/comparison.schema.json`.

## 7b. Framework metadata in course.yaml

When `course.yaml` declares framework metadata (id, version, docs, compatibility, deprecated features, migration notes), the course
page header shows it together with the curriculum version; with none declared nothing is rendered.

## 8. Quality rules

* Language of learner-facing text: Russian today; keep technical names (identifiers, flags, API names) untranslated.
* One concept per lesson; objectives and mastery criteria must be checkable by the exercise or quiz.
* An exercise passes only when **all** tests pass; a clean exit code alone never counts. For ML and numeric work use fixed seeds,
  tolerances, shape/dtype checks, and no mandatory GPU.
* Teach shared topics (algorithms, SQL, networking, statistics) once and add language variants.
* Anything not implemented is described as Planned; do not add a course just to occupy a catalog slot.

## Python packages and accelerators (lesson or exercise level)

```yaml
requires: { python: ">=3.11", packages: ["numpy>=2"] }   # PEP 440 / PEP 508 subset; checked via importlib.metadata
accelerator: none            # none (default) | optional | required
```

An exercise value overrides the lesson value. Missing packages give a `RuntimeMissing` result that points to
Settings -> Runtimes (nothing is installed automatically). `accelerator: required` disables Run/Submit when no usable
accelerator is found in the selected environment; use it only for GPU-only exercises. The validator rejects
bad specifiers, option-like or URL packages, duplicates, and the fields on non-Python exercises.

`requires.memoryMb` (256-1024, Python exercises only, default 256) raises the memory limit of a run of that exercise (Run button, grading, theory samples
checked by `validate-content`); use it only for libraries whose import alone nears the default (PyTorch: `import torch` is ~215 MB resident). Time limits, the
network ban and the write restrictions are not affected.

Validating such content: `make python-env` creates `.venv-content` from `tools/python-dev-requirements.txt` (downloads from PyPI;
nothing is installed globally), after which `make validate-content` passes `--python-env .venv-content` automatically.
`devacademy validate-content --run-solutions --python-env <venv dir>` runs exercises and theory samples whose lesson declares
`requires` with that environment's interpreter (bin/python or Scripts/python.exe) and checks the Python version and packages against it;
without the flag they only produce a warning ("requires packages ...; pass --python-env"). `devacademy python-env-check --python-env <dir>`
lists every requirement found in the content and whether the environment satisfies it.

Framework courses pin their package in `tools/python-dev-requirements.txt` (for example `Django==6.1.2` for `django-fundamentals`,
`fastapi` for the FastAPI course). Django exercises need no server or manage.py: the learner file calls `settings.configure(...)`
and `django.setup()` (SQLite `:memory:`, the app is an `AppConfig` subclass defined in the same file, tables are created with
`connection.schema_editor().create_model`) and the harness drives views through `django.test.Client`.
## Go modules for Go exercises (GoMLX)

```yaml
requires: { go: ">=1.26.5", goModules: [github.com/gomlx/gomlx, github.com/gomlx/compute] }   # go exercises only
```

`goModules` are module paths (no versions: versions come from the set). The module list must be covered by ONE `go` set in `tools/content-packages/index.json`
(`go/<id>/go.mod` + `go.sum`, same smallest-covering-set rule as the other languages). The exercise's own `starter/` and `solution/` carry a `go.mod` and `go.sum`
with the **same pins as the set**: the build runs offline (`GOPROXY=off`, `GOSUMDB=off`, `GOFLAGS=-mod=mod`, `GOTOOLCHAIN=local`, `CGO_ENABLED=0`) against the
set's read-only module cache, so a missing or different version fails to build. Create them from the set (`cp tools/content-packages/go/<id>/go.{mod,sum}`) and
change only the `module` line; the `go` directive must be `1.26.5` or lower. `requires.go` is an optional lower bound (`>=1.26.5`); a version-only requirement is
checked against the detected `go` even without a module cache.

`make go-env` runs `go mod download` for EVERY go set into the gitignored `.go-content/<set>/pkg/mod` (DOWNLOADS from proxy.golang.org; about 82 MB of
archives for `gomlx`; nothing is global). `make validate-content` passes `--go-env .go-content` when that directory exists; without it, the Go-module exercises and
theory samples are skipped with a warning. CI has no `go-env` step (no verified `setup-go` configuration), so CI skips them as well; run
`make go-env && make validate-content` locally before changing such a course. In the app a blocked lesson offers «Установить пакеты: <набор>», Settings has a Go
section with Install/Remove per set, and each install shows the modules, the exact command (`go mod download`) and that it needs the network.

Authoring rules learned from `go-ml-gomlx`:

* Theory samples (` ```go run `) of a lesson whose exercise has `goModules` are built beside the exercise **solution's** `go.mod` and `go.sum`, so they may import the
  same modules and must not need others. A sample is `package main` and is compared with `output` after trimming; keep floats short (`%.3f`) because `float32`
  can differ in the last digits between CPUs.
* Exercises with floating-point output use `compare: numeric` with a `tolerance` (non-numeric tokens still match exactly, so print `w = 2.000` rather than `w=2.000`).
  Derive the expected output from a real run of the solution, avoid values near a rounding boundary, and keep the run well under the 5 s default limit.
* Pre-1.0 libraries: pin exact versions, say so in `framework.deprecated`/`migrationNotes`, and describe tradeoffs honestly (the pure-Go GoMLX backend is slower
  than XLA; the course teaches principles, not performance).

## C# projects and NuGet packages (ASP.NET Core)

```yaml
requires: { nugetPackages: [Microsoft.AspNetCore.TestHost] }   # csharp exercises only
```

**Project mode.** A C# exercise normally builds its entry file as a file-based app. When the exercise files hold **exactly one `*.csproj` in the root**, the runner builds that
project instead (`dotnet build <project> -o out` with the same flags) and runs `out/<AssemblyName>.dll` (`<AssemblyName>` from the project, else the project file name), so
an exercise may have several `.cs` files, a fixed driver `Program.cs` and a learner file. Without a project nothing changes. Harness, io and command evaluation all work with multiple files.

**NuGet sets.** `nugetPackages` are package ids (no versions: the pins come from the set). They must be covered by ONE `nuget` set in `tools/content-packages/index.json`
(`nuget/<id>/restore.csproj`, language `nuget`, exact `Version="..."` pins; same smallest-covering-set rule). The exercise's own project references the **same pinned versions**
(copy the `PackageReference` lines of the set); the validator requires exactly one root `.csproj` in the starter and in the solution. The build runs offline against the set's
read-only packages folder: `NUGET_PACKAGES=<set>/packages` and `-p:RestoreSources=<set>/packages` (no `NuGet.Config`: the build never consults the network), and such a run never
uses a container. The install is `dotnet restore restore.csproj --packages <data>/nuget-content/<set>/packages --source https://api.nuget.org/v3/index.json` after the learner confirms
(packages with versions, the exact command, the network notice); nothing is downloaded implicitly.

## Java and Maven artifacts (Spring Boot)

```yaml
requires: { mavenArtifacts: [org.springframework.boot:spring-boot, org.springframework:spring-webmvc], memoryMb: 512 }   # java exercises only
```

`mavenArtifacts` are `group:artifact` coordinates (no versions). They must be covered by ONE `maven` set in `tools/content-packages/index.json` (`maven/<id>/{pom.xml,lock.json}`, language `maven`;
same smallest-covering-set rule). `lock.json` lists every jar of the resolved runtime classpath with `sha256` and `size`; regenerate it with `python3 tools/content-packages/maven/generate-lock.py <id>`
(needs Maven, developers only). The learner's machine needs no Maven: after confirmation the app downloads exactly the locked jars from repo1.maven.org into `<data>/maven-content/<set>/lib` and
verifies every sha256. Runs use `-cp lib/*` with a read-only `lib` and no container. `make maven-env` downloads EVERY maven set into the gitignored `.maven-content/<set>/lib`; `make validate-content`
passes `--maven-env .maven-content` when it exists, otherwise Maven exercises and theory samples are skipped with a warning.

Authoring rules for Spring Boot courses (`spring-boot-fundamentals`): the sandbox denies sockets, so the context starts without Tomcat (`GenericWebApplicationContext` over `MockServletContext`) and
`MockMvc` is driven by a fixed `demo/Main.java` that reads requests from stdin (an exception that escapes a request prints `500 - <ExceptionClass>`; a context that fails to start prints `startup failed: <ExceptionClass>: <root cause message>` to stderr and exits 1); exercises live in package `demo`, theory samples are one default-package `Main.java`. Code is compiled with `-parameters`.

`make nuget-env` runs that restore for EVERY nuget set into the gitignored `.nuget-content/<set>/packages` (DOWNLOADS from api.nuget.org). `make validate-content` passes
`--nuget-env .nuget-content` when that directory exists; without it, NuGet exercises and theory samples are skipped with a warning (as `--go-env` does for Go).

Authoring rules for ASP.NET Core courses (`aspnetcore-fundamentals`):

* The sandbox denies listening sockets, so the app runs in memory under `TestServer` (`builder.WebHost.UseTestServer()`) and a fixed driver `Program.cs` feeds it requests from stdin
  through `app.GetTestClient()`. The driver clears logging providers and strips nondeterministic data (`traceId`, timestamps, GUIDs) from what it prints.
* Theory samples (` ```csharp run `) of a lesson whose exercise has `nugetPackages` are built as a `Program.cs` next to the **solution's** `.csproj`, so a sample is one top-level program
  (the Web SDK implicit usings are on) that needs no other file. Output blocks come from real runs.
* A build takes about 2 s and 230 MB of memory, a run about 0.2 s and 90 MB; the default 256 MB run limit is enough (the compile step has its own limit).

## npm packages for JavaScript exercises (React, Vitest, Testing Library)

```yaml
requires: { node: ">=22", npmPackages: [vitest, jsdom, react, react-dom, "@testing-library/react"] }   # javascript exercises only
command:
  tool: vitest                 # the runner builds `node node_modules/vitest/vitest.mjs run ...`; no npx, no path from content
  args: ["--reporter=default", "--reporter=junit", "--outputFile=report.xml"]
  testsTarget: "."             # tests_dir holds tests/*.test.jsx and vitest.config.mjs
  report: junit
  reportFile: report.xml       # must equal --outputFile
```

The packages live in **package sets** (see below): `make node-env` runs `npm ci --ignore-scripts` for EVERY node set from
`tools/content-packages/node/<set>/package-lock.json` into the gitignored `.node-content/<set>/` (DOWNLOADS from npm; nothing is global). At grading time the
runner links `node_modules` read-only into the sandboxed work directory (writes are denied by the OS sandbox; there is no network), keeps Vite's
cache inside the work directory (`cacheDir: ".cache/vite"` in `vitest.config.mjs`) and never installs anything. Learner files may not live
under `node_modules`. `vitest.config.mjs` for React: `oxc: { jsx: { runtime: "automatic" } }`, `test: { environment: "jsdom", globals: true,
include: ["tests/**/*.test.jsx"] }` (globals only so Testing Library cleans up between tests; jest-dom is not installed).
Express courses (`express-fundamentals`) pin `express` (5.x) and `light-my-request` in the `express` set and use `npmPackages: [vitest, express,
light-my-request]`. Requests are injected into the app function without opening a port: supertest was rejected because its `listen(0)` is denied
by the no-network sandbox (EPERM), while light-my-request works under it. `vitest.config.mjs` for them: `test: { environment: "node",
fsModuleCache: false, include: ["tests/**/*.test.mjs"] }` (`fsModuleCache: false` stops Vitest from creating `node_modules/.vitest-cache`, which is
read-only at grading time).
Allowed Vitest flags are an allowlist (`--reporter` junit/default/verbose/dot, `--outputFile`, `--config`, `--pool`, `--environment`,
`--testTimeout`, `--maxWorkers`, `--no-file-parallelism`, `--silent`, `--passWithNoTests`, `--globals`); watch, ui, browser, coverage and update
flags are rejected. The validator requires every package the tool needs to be in `npmPackages`. Theory samples of a lesson with such an
exercise run with the same environment (`javascript run` blocks are plain Node scripts, so `require("react")` works and JSX does not).

Validating: `make node-env && make validate-content` (passes `--node-env .node-content`, the directory holding the set subdirectories). Without it
the exercises are skipped with a warning. In the desktop app a blocked lesson offers «Установить пакеты: <набор>», and Settings -> Runtimes
lists every set with Install/Remove; each install shows the packages, the exact command and that it needs the network, and runs only after a
confirmation.

### Package sets (`tools/content-packages/`)

A learner's machine downloads only the packages of the library being studied, so packages are grouped into per-course **sets**.
`index.json` is an array of `{ id, title (Russian), language: python|node|go, description, packages }` (`id` matches `^[a-z0-9][a-z0-9-]{0,40}$`).
Node sets are `node/<id>/{package.json,package-lock.json}` (lock generated with `npm install --package-lock-only --ignore-scripts`; shared
versions must be identical across sets); Python sets are `python/<id>.txt` with pinned requirements; Go sets are `go/<id>/{go.mod,go.sum}` (`packages` lists
the top-level module paths). Current sets: node `react`, `typescript`,
`express`; python `numpy`, `pandas`, `fastapi`, `django`; go `gomlx`; nuget `aspnetcore-testing`; maven `spring-boot-web-test`. `tools/python-dev-requirements.txt` includes every Python set (`-r`) plus ipykernel.

A set may carry an optional `platforms` list of the .NET RIDs it can be installed on (`osx-arm64`, `osx-x64`, `linux-x64`, `linux-arm64`, `win-x64`,
`win-arm64`); absent means every platform. Use it when an upstream library has no builds for a platform (`pytorch`: PyTorch publishes no macOS Intel wheels, so
its set lists `osx-arm64`, `linux-x64`, `linux-arm64`, `win-x64`). On an unsupported platform the app offers no Install button and says why, Settings shows the set as
unavailable, and `devacademy validate-content --run-solutions` skips the affected solutions and samples with a Warning (`package set '<id>' is not available on
<rid>`) instead of an Error; `python-env-check` lists such packages as "unsupported on <rid>", not MISSING. Structural validation ignores `platforms`. A Python
set whose requirements file must still install everywhere (via `tools/python-dev-requirements.txt`) puts a PEP 508 environment marker on the affected line
(`torch==2.14.1; sys_platform != "darwin" or platform_machine != "x86_64"`; markers have no `not`).

An exercise's required packages (`requires.packages` for Python; `requires.npmPackages` plus the package of its command tool for Node; `requires.goModules` for Go; `requires.nugetPackages` for C#; `requires.mavenArtifacts` for Java) must be
covered by ONE set of the right language: the smallest covering set wins, ties go to index order. The validator reports an error for an
exercise no set covers, so when adding a course that needs a new package, add it to an existing set (or create a set) in the same change.
If the index cannot be found the validator only warns.

Theory can embed the optimiser lab: a fenced block `visualize gradient-descent function=rosenbrock optimizer=adam lr=0.02`
(all arguments optional; functions `quadratic|rosenbrock|double-well`, optimizers `gd|momentum|adam`).

Two more labs for the classical-ML lessons:

* `visualize kmeans k=3 init=kmeans++ seed=1` — k-means with alternating assignment / centroid-update steps, centroid trail, inertia and a
  convergence message (at most 50 iterations). `k` is an integer `2..6` (default 3), `init` is `random|kmeans++` (default `kmeans++`),
  `seed` is an integer (default 1); all optional. Without a body it uses a built-in dataset of three blobs.
* `visualize pca` — steps through raw data, centring, principal axes, projection on PC1 and 1-D reconstruction, with the explained variance of
  PC1/PC2. No arguments. Without a body it uses a built-in correlated 2-D dataset.

Both take an optional body of points, one `x y` pair of numbers per line (at least 2 points, at most 500, coordinates up to 1e6 in absolute
value; with `kmeans` there must be at least `k`). Bad arguments or a malformed body are content errors reported by `validate-content`.
