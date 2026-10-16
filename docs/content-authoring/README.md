# Content authoring guide

Content is data: YAML + Markdown + exercise files under `content/`, loaded at runtime. Adding or changing a lesson never
requires rebuilding the app. The **C# validator is authoritative**; JSON Schemas in `content/schema/` are editor aids
(they can be stricter or looser in corners). Loader: `src/DevAcademy.Content` (`ContentLoader`, `ContentValidator`,
`TheoryParser`). Unknown YAML keys are rejected, so typos show up as diagnostics.

Workflow: edit → `make validate-content` (= `devacademy validate-content --content content --run-solutions`) → fix every
error → commit. `--run-solutions` additionally runs each reference solution against its tests and each starter (must fail),
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
assessmentFor: [lesson ids]   # placement quiz: passing credits those lessons; requires `quiz`; not self
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

**Placement check.** A lesson with `assessmentFor: [lesson ids]` and a `quiz` is offered as "skip if you already know" on its
course and on every path containing it. Passing credits the listed lessons (source Assessment) through the normal progress
path; failing changes nothing. Make it theory-only with conceptual questions (see `python.advanced.placement`).

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
Theory can embed the optimiser lab: a fenced block `visualize gradient-descent function=rosenbrock optimizer=adam lr=0.02`
(all arguments optional; functions `quadratic|rosenbrock|double-well`, optimizers `gd|momentum|adam`).
