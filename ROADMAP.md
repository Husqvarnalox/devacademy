# DevAcademy — Roadmap

Status markers: **Done** (implemented and covered by tests), **Partial** (some of it works; the gap is
stated), **In progress**, **Planned** (design only, nothing in code). A feature is "Done" only when it is
verified on at least macOS arm64; broader platform claims are in [PLATFORM_SUPPORT.md](PLATFORM_SUPPORT.md).

Last reviewed: 2026-10-22. Authoritative requirement digest: [docs/requirements/ecosystem-expansions.md](docs/requirements/ecosystem-expansions.md).

## 1. Core phases

Phases 1–4 are the numbering used in [ARCHITECTURE.md](ARCHITECTURE.md). The labels for phases 5–12 were
reconstructed from the implemented feature set and the original plan's end points; treat the grouping as
indicative, the status column as factual.

| # | Phase | Status | Notes |
|---|---|---|---|
| 1 | Architecture, ADR, solution layout | Done | [ARCHITECTURE.md](ARCHITECTURE.md), [ADR 0001](docs/adr/0001-csharp-avalonia.md), 8 src + 7 test projects |
| 2 | Domain model + Content Engine (YAML/Markdown loader, validator, JSON schemas) | Done | `validate-content` CLI; schemas are editor aids, the C# validator is authoritative |
| 3 | Code Execution Engine | Done (macOS arm64) | 10 language adapters, 3 isolation levels, limits, concurrency cap. Linux bubblewrap and Windows paths unverified/absent — see [RUNTIME_ARCHITECTURE.md](RUNTIME_ARCHITECTURE.md) |
| 4 | AI Integration Layer | Done (fake-HTTP tested only) | OpenAI, Anthropic, Gemini, OpenRouter, OpenAI-compatible; no live API call has been made in tests — [AI_PROVIDERS.md](AI_PROVIDERS.md) |
| 5 | Evaluation + Learning Path Engine + progress | Done | Io and Harness evaluators; locking, completion, reviews 1/3/7/16/35 days; assessment credit. Command evaluator: Implemented for node --test (JUnit) and go test -json; other tools Planned |
| 6 | Persistence (SQLite), settings, secret store, backup/export | Done (secret store: macOS only verified) | Keychain verified; Windows Credential Manager and Linux libsecret compiled, unverified |
| 7 | Desktop UI (Avalonia): onboarding, catalog, roadmap, lesson workspace, dashboard, settings, AI tutor panel | Done | Native controls, no WebView |
| 8 | First curriculum | Partial | Curriculum standard ([docs/curriculum-standards.md](docs/curriculum-standards.md)) applied to Go (from zero, for programmers, skill checks, professional), the Python core, and JavaScript, TypeScript, C#, Java, Kotlin, C++, Rust and PHP (from zero, for programmers, skill checks, route each). Node.js `first-steps` stays a course for programmers; professional-level courses beyond Go Planned. Status per course: [docs/curriculum-audit.md](docs/curriculum-audit.md) |
| 8a | Adaptive learning: prior knowledge, depth, competency checks | Done | Level and depth drive the start lesson, recommendations and theory section order; routes with competencies; Diagnostic page; `concepts` and `formerIds` validated; progress migration on rename ([docs/learning-levels.md](docs/learning-levels.md)) |
| 9 | Algorithms: problems list, exercise language variants, complexity/editorial, sorting visualization | Partial | Mechanisms Done; the Algorithms track itself is `planned` in the catalog |
| 10 | Framework and ML courses | Planned | All framework/ML tracks are `planned`; no stub courses |
| 11 | Developer-IDE features: PTY terminal, LSP, DAP, project workspaces, notebooks | In progress | PTY terminal, project workspaces, LSP, DAP (python/debugpy, cpp+rust/lldb-dap, go/dlv, js+ts/vscode-js-debug, java/JDI adapter, csharp/netcoredbg on macOS arm64) implemented; notebooks planned |
| 12 | Packaging, CI, updates | Partial | GitHub Actions CI (build, format check, tests on 5 RIDs, content validation, self-contained single-file publish for 6 RIDs); no installers, signing, notarization or auto-update. CI results on non-macOS runners are not yet reported as verified here |
| 13 | Web platform (ASP.NET Core host, PostgreSQL, sandboxed runners) | Planned | Requires a separate security model ([SECURITY_MODEL.md](SECURITY_MODEL.md) §6) |

## 2. Cross-cutting engines for the ecosystem expansions

| Item | Status |
|---|---|
| Learning paths as dependency graphs (`course.requires`, path `targets`) | Done (paths have no optional/alternative branches yet — Planned) |
| Framework/library version metadata in `course.yaml` | Done in schema/validator; no UI surfacing, no course uses it yet |
| Assessment credit (placement quiz) | Done |
| Exercise language variants | Done |
| Comparison mode content type | Planned (design in ARCHITECTURE §13) |
| Python Environment Manager | In progress |
| Project Workspaces (npm/pnpm/yarn, venv/uv), web preview module | Planned |
| ML Runtime Manager (CPU/CUDA/ROCm/MPS capability probe) | Planned |
| Notebook workspace (Jupyter protocol) | Planned |
| Command evaluator (Vitest/Jest/pytest/tsc) | node:test and go test implemented; Vitest/Jest/pytest/tsc Planned (need tool adapters) |
| ML visualizations (gradient descent lab, etc.) | Planned (only `visualize sorting` exists) |
| AI tutor with ML context (dataset metadata, metrics, config) | Planned |

## 3. Track: JavaScript / TypeScript ecosystem

Curriculum details: [docs/curriculum/javascript-ecosystem.md](docs/curriculum/javascript-ecosystem.md).
Runtime today: `javascript` and `typescript` single-file execution (Node ≥ 22.6 for TS). Everything needing
a dev server or a browser is blocked on Project Workspaces; exercises graded by Vitest use the pinned node content environment (`make node-env`).

| Phase | Scope | Status |
|---|---|---|
| A. Foundations | JavaScript, TypeScript, Node.js, HTML/CSS/Browser | JavaScript and TypeScript: from-zero (23 and 24 lessons), for-programmers, skill checks, then `*-first-steps` and `*-fundamentals`. Node.js: `nodejs-first-steps` (2 lessons, for programmers). HTML/CSS/Browser: Planned (needs web preview module) |
| B. Core frameworks | React, Vue, Angular, Express, Fastify, NestJS | Partial: `react-fundamentals` (6 lessons, graded by real Vitest + Testing Library in the sandbox, packages from the pinned node content environment). Others: Planned |
| C. Fullstack | Next.js, Nuxt, SvelteKit, Astro | Partial: `nextjs-fundamentals` (7 lessons, Next.js 16 App Router; graded by real Next.js + Vitest in the sandbox with the pinned `nextjs` package set: route handlers, server/client components, server actions, cache declarations, proxy). `next dev`/`next build` do not run in the sandbox (theory only, needs Project Workspaces). Nuxt, SvelteKit, Astro: Planned |
| D. Advanced ecosystem | State management, ORMs, testing (Vitest/Jest/Playwright), performance, security, deploy | Planned |
| E. Additional | SolidJS, Qwik, Hono, AdonisJS, TanStack Start | Planned |
| F. AI/ML in JS | TensorFlow.js, Transformers.js, ONNX Runtime Web/Node, WebGPU, Wasm | Planned |
| G. Internals | V8, event loop, GC, engine optimization, advanced TS, browser rendering | Planned |
| Enabler: Project Workspaces + web preview | | Planned |
| Enabler: Command evaluator | | Planned |

## 4. Track: Python ecosystem

Curriculum: [docs/curriculum/python-ecosystem.md](docs/curriculum/python-ecosystem.md) and the specialization
documents linked from [CURRICULUM.md](CURRICULUM.md).

| Phase | Scope | Status |
|---|---|---|
| A. Core | Fundamentals, stdlib, OOP, typing, async, testing | Partial: `python-fundamentals` (10 lessons: execution model, variables/input, numbers, conditions, loops, functions, strings, lists, dicts/sets, errors/files). Remaining fundamentals levels (data structures depth, OOP, modules, typing), stdlib, async, testing: Planned |
| B. Backend | Django, FastAPI, Flask, SQL/ORM, async, jobs | Planned (catalog tracks `django`, `fastapi` are `planned`) |
| C. Automation | Files, subprocess, HTTP, scraping, scheduling, office files | Planned |
| D. Data Science | NumPy, Pandas/Polars, visualization, Jupyter | Planned (needs Python Environment Manager) |
| E. Machine Learning | Math for ML, classical ML (from scratch, then library) | Planned |
| F. Deep Learning | PyTorch/TensorFlow/JAX fundamentals | Planned (ML Runtime Manager) |
| G. LLM engineering | Inference, fine-tuning, RAG, agents, LLM security | Partial: `llm-fundamentals` (9 lessons on NumPy/stdlib, offline and CPU-only: byte-level BPE, embeddings, bigram LM and perplexity, sampling, attention, mini-GPT forward with KV-cache, TF-IDF retrieval for RAG with recall@k, chat templates and validated tool calls, evaluation metrics and limits). Provider APIs (fake server), local inference of real models, fine-tuning, agents: Planned |
| H. MLOps | Tracking, serving, containers, monitoring | Partial: `mlops-fundamentals` (8 lessons, stdlib only, offline): reproducibility, hand-made experiment tracking, content-hash data versioning, cached step pipeline, model registry with stages, serving with validation and batching, drift (PSI/KS), quality monitoring and alert rules. Real MLflow/DVC/BentoML installs, Docker images, Kubernetes, feature stores: Planned (theory only) |
| I. Advanced Python | Internals, memory, performance, C extensions, advanced concurrency | Planned |
| J. Additional | CV, NLP, RL, data engineering, desktop GUI, security | Planned |
| Enabler: Python Environment Manager | venv/uv, explicit installs, run in env | In progress |
| Enabler: ML Runtime Manager | capability probe | Planned |
| Enabler: Notebook workspace | ipykernel over ZeroMQ | Planned |

Acceptance criteria from the digest, current state: choose a language and see its path — works; study theory,
write code, run, run tests (single-file, stdlib-only), AI hints, save progress, resume after restart,
offline exercises — works for the available courses. Choosing an interpreter, creating a venv, installing
dependencies, multi-file projects, notebooks, ML datasets/CPU ML exercises, GPU, LLM inference/RAG/fine-tuning
and deploying a service are **not** met yet.

## 5. Explicit non-goals

Electron, Tauri, Qt/PyQt or a WebView as DevAcademy's own UI (they may be *taught*); microservices for the
desktop app; accounts or cloud sync in the desktop product; claims of support for any platform, GPU or
provider that has not been verified.
