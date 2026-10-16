# Requirements digest — ecosystem expansions

Condensed, authoritative digest of the product owner's expansion specs (2026-10-16).
Agents and contributors work from this file. Common rules for every expansion:
reuse the shared engines (Content, Learning Path, Runtime Manager, Execution, Assessment,
Progress, AI Tutor, Project Workspace) — never a second learning system; desktop stays
C# + Avalonia (Electron/Tauri/PyQt etc. may be *taught*, never used for DevAcademy itself);
no stub courses (unimplemented = "Planned"); claim nothing untested; shared topics
(algorithms, SQL, networking, statistics, linear algebra, architecture) are taught once
with language-specific exercises; learning paths are dependency graphs, mastered topics
are reused across specializations and may be skipped after a placement check.

Version metadata for every framework/library curriculum: framework/library id + version,
runtime version (e.g. Python), curriculum version (independent), compatibility, deprecated
APIs, migration notes, official doc links. Content updates never need an app rebuild; a new
major framework version never auto-updates examples without compatibility testing.

Comparison mode (e.g. React vs Vue vs Angular, Express vs Fastify vs NestJS, Next.js vs Nuxt,
Prisma vs Drizzle, Django vs FastAPI vs Flask, SQLAlchemy vs Django ORM, Pandas vs Polars,
PyTorch vs TensorFlow vs JAX, Celery vs Dramatiq vs RQ, sklearn vs XGBoost vs LightGBM,
LangChain vs LlamaIndex): purpose, architecture, component/state model, performance,
ecosystem, learning curve, deployment, limits, typical use — explicit criteria, no
"X is best".

## A. JavaScript / TypeScript ecosystem

Concepts kept distinct: JavaScript (language), TypeScript (typed superset; types erased,
not runtime validation; any/unknown/never differ; no guarantee against runtime errors),
Node.js (runtime), React (UI library), Vue (progressive framework), Angular (application
framework), Next.js (React fullstack), Nuxt (Vue fullstack).

Routes (each its own Learning Path): JS Fundamentals, Advanced JS, TS Fundamentals,
Advanced TS, Node.js, Frontend, Backend, Fullstack, Desktop (Electron, Tauri, IPC, packaging,
auto-update, security), Testing & QA, Performance, Runtime Internals (V8, event loop, GC,
libuv), JS AI/ML (TensorFlow.js, Transformers.js, ONNX Runtime Web/Node, WebGPU, Wasm;
training vs inference, browser vs server, CPU/GPU/WebGPU/Wasm; not every lib trains).

Catalog: Core frontend (HTML5, CSS3, DOM, Browser APIs, Fetch, HTTP, WebSockets, a11y,
responsive, rendering, web perf); React (Hooks, Context, Router, Redux Toolkit, Zustand,
TanStack Query, React Hook Form, Next.js, React Router framework mode, Testing Library,
Vitest, Playwright); Vue (Vue 3, Composition/Options API, Router, Pinia, Nuxt, VueUse,
Vue Test Utils); Angular (CLI, components, templates, directives, pipes, services, DI,
Signals, RxJS, Router, Reactive Forms, HttpClient, testing, SSR, NgRx); Svelte/SvelteKit;
Astro, SolidJS, Qwik, Lit, Web Components. Fullstack: Next.js (App Router, layouts, Server/
Client Components, data fetching, caching, rendering strategies, Route Handlers, Server
Functions, auth, middleware/proxy per version, perf, deploy, testing), Nuxt (file routing,
auto-imports, composables, server routes, data fetching, rendering modes, middleware, state,
modules, deploy, testing), SvelteKit, React Router framework mode, Astro, Angular SSR,
TanStack Start. Rendering concepts (CSR, SSR, SSG, ISR, streaming SSR, hydration, partial
hydration, islands, RSC, server actions, edge vs Node runtime) are tied only to frameworks
that actually support them. Node.js (architecture, event loop, V8, libuv, streams, buffers,
fs, http, tcp/udp, worker threads, child processes, ESM vs CJS, npm/pnpm, perf, memory,
profiling); backend frameworks Express, Fastify, NestJS, Koa, Hono, AdonisJS (each: arch,
routing, middleware, DI, validation, errors, DB, auth, testing, perf, deploy); DBs
PostgreSQL/MySQL/SQLite/MongoDB/Redis; ORMs Prisma, Drizzle, TypeORM, Sequelize, Knex;
APIs REST, GraphQL, WebSockets, SSE, gRPC, auth, validation, rate limiting, caching, jobs,
queues; BullMQ, Socket.IO, GraphQL Yoga, Apollo Server, tRPC, OpenTelemetry.

Example paths (graph, not fixed lists): React dev (JS → Adv JS → TS → HTML/CSS/Browser →
React → Hooks → component arch → state → routing → data fetching → Next.js → testing → perf
→ project); Vue dev (… Vue → Composition API → Router → Pinia → Nuxt …); Angular dev (… DI →
Signals → RxJS → Router → Forms → HTTP → testing → SSR …); Node backend (JS → Adv JS → TS →
Node → HTTP → Express|Fastify → PostgreSQL → ORM → auth → testing → NestJS (optional) →
caching → queues → project).

Projects per framework at Beginner/Intermediate/Advanced (React: todo, weather dashboard,
admin dashboard, e-commerce frontend, real-time collab; Vue: task manager, CRM, analytics,
complex SPA; Angular: task app, enterprise dashboard, multi-module app; Next.js: personal
site, blog/CMS, auth app, SaaS dashboard, fullstack e-commerce; Nuxt: content site, SSR app,
fullstack; Node: CLI, HTTP API, auth service, WebSocket chat, distributed job queue,
event-driven backend; NestJS: REST API, modular backend, DB app, production service). Each:
requirements, architecture, acceptance criteria, unit/integration/E2E tests, code review,
performance and security considerations.

Workspaces: create Node/React/Vue/Angular/Next/Nuxt/NestJS projects with real package
managers (npm, pnpm, yarn) and current stable templates; install deps; run dev server; run
tests; production build; terminal. Web preview is a separate module; the app never becomes a
WebView. Assessment uses real tools (tsc, Vitest, Jest, Playwright, framework test utils);
a successful build alone never passes an exercise.

JS roadmap phases: A Foundations (JS, TS, Node, HTML/CSS/Browser) · B Core frameworks (React,
Vue, Angular, Express, Fastify, NestJS) · C Fullstack (Next.js, Nuxt, SvelteKit, Astro) ·
D Advanced ecosystem (state, ORM, testing, perf, security, deploy) · E Additional (SolidJS,
Qwik, Hono, AdonisJS, TanStack Start) · F AI/ML · G Internals (V8, event loop, GC, engine
optimization, advanced TS, browser rendering).

## B. Python ecosystem

Specializations (own paths): Fundamentals, Advanced, Backend, Automation, Data Science,
ML, Deep Learning, AI/LLM Engineering, Computer Vision, NLP, Data Engineering, DevOps,
Desktop, Testing, Performance, Security, Internals. Concepts learned once (e.g. functions)
are reused; new specializations add domain-specific exercises instead of repeating theory.

Fundamentals levels: L0 getting started (CPython, interpreter, REPL, scripts, terminal,
IDE, pip, uv, venv) · L1 syntax · L2 data structures (incl. tuples, frozensets, hashability,
complexity) · L3 functions & abstractions (first-class functions, lambda, closures,
decorators, recursion, generators/iterators/yield, context managers, functools, itertools)
· L4 OOP (classmethod/staticmethod, inheritance vs composition, ABC, Protocol, properties,
dataclasses, dunder methods, descriptors) · L5 exceptions & files (hierarchy, chaining,
pathlib/os/shutil/tempfile, json, csv, serialization, logging) · L6 modules & packages
(import system, __init__.py, resolution, PYTHONPATH, pyproject.toml, packaging) · L7 typing
(hints, generics, TypeVar, Protocol, TypedDict, Literal, Union/Optional, Callable,
ParamSpec, narrowing, mypy/Pyright; runtime typing vs static checking).

Standard library catalog (each module: purpose, core APIs, examples, mistakes, performance,
limits, exercises): collections, heapq, bisect, array, enum · functools, itertools,
operator · pathlib, os, sys, shutil, tempfile, subprocess, glob · json, csv, pickle, struct,
configparser · datetime, time, math, statistics, decimal, fractions, random · threading,
multiprocessing, concurrent.futures, asyncio, queue · socket, ssl, urllib, http, email ·
logging, traceback, pdb, unittest, doctest, cProfile, timeit, tracemalloc · inspect,
importlib, ast, dis, gc, weakref, ctypes, contextlib. Prefer stdlib over third-party where
reasonable.

Backend: Django (+DRF, Ninja, Channels, Celery, Redis), FastAPI (Pydantic, DI, async,
OpenAPI, WebSockets, background tasks), Flask (app factory, blueprints, request context,
extensions), Litestar, Starlette, Sanic, aiohttp — each: purpose, architecture, problems
solved, project layout, APIs, DB, auth, testing, security, deploy; not hello-world only.
DB/ORM: SQLAlchemy, Django ORM, SQLModel, Tortoise, Peewee, Alembic, psycopg, asyncpg,
redis-py, Motor/PyMongo; PostgreSQL, MySQL, SQLite, Redis, MongoDB; SQL, design, relations,
transactions/ACID, indexes, query optimization (show the SQL the ORM emits; query-analysis
exercises), pooling, async access, migrations, isolation, deadlocks, replication, caching.
Async & concurrency: asyncio (event loop, coroutines, tasks, futures, cancellation,
timeouts, semaphores, locks, queues, TaskGroup/structured concurrency), aiohttp, HTTPX,
AnyIO, Trio, uvloop; threading, multiprocessing, executors; concurrency vs parallelism,
CPU- vs I/O-bound, GIL incl. free-threaded builds (PEP 703), races, deadlocks — no outdated
oversimplifications. Jobs/messaging: Celery, Dramatiq, RQ, ARQ, APScheduler, FastStream,
aio-pika, confluent-kafka; Redis, RabbitMQ, Kafka; retries, scheduling, idempotency, DLQ,
acks, event-driven design.
Automation: pathlib, shutil, subprocess, requests, HTTPX, BeautifulSoup, Scrapy, Playwright,
Selenium, lxml, PyAutoGUI, pynput, watchdog, APScheduler, openpyxl, python-docx, pypdf,
Pillow, Paramiko, Fabric; rate limiting, retries, logging, credential security,
validation, idempotency. Never teach bypassing protections or unauthorized access.
Data Science: NumPy, Pandas, Polars, SciPy, DuckDB, PyArrow; Matplotlib, Seaborn, Plotly,
Bokeh, Altair; Jupyter. Vectorization, broadcasting, DataFrames, joins, missing values,
EDA, memory; lists vs ndarrays, Pandas vs Polars, eager vs lazy, columnar/Arrow/Parquet.
Math for ML: linear algebra, calculus, probability, statistics, optimization — each taught
intuitively, mathematically, visually, in Python, and applied to ML.
Classical ML: scikit-learn, XGBoost, LightGBM, CatBoost, Optuna, SciPy, statsmodels,
imbalanced-learn; every main algorithm in two stages: A from scratch (Python/NumPy), B with
the library; compare correctness, numerical stability, performance, complexity.
Deep learning: PyTorch, TensorFlow, Keras, JAX, Flax, Lightning, Accelerate; NN fundamentals
(backprop, autodiff, optimizers, regularization, BN), CNN/RNN/LSTM/GRU/attention/
transformers/embeddings/transfer learning — learner must understand what training does,
not just model.fit(). CV: OpenCV, Pillow, torchvision, Ultralytics, Detectron2, timm,
Albumentations. NLP: NLTK, spaCy, Gensim, Sentence Transformers, HF Transformers,
tokenizers. LLM engineering: Transformers, Diffusers, PEFT, TRL, Accelerate, bitsandbytes,
llama-cpp-python, vLLM, ONNX Runtime, LangChain, LangGraph, LlamaIndex, FAISS, Chroma,
Qdrant, pgvector — fundamentals, inference (sampling, KV cache, quantization, batching,
streaming, serving), fine-tuning (LoRA, QLoRA, PEFT, SFT, preference optimization), RAG,
agents (tool calling, structured output, permissions, error recovery, evaluation), LLM
security (prompt injection, leakage, unsafe tools, output validation); always distinguish
using a model / inference / fine-tuning / training from scratch / serving.
RL: Gymnasium, Stable-Baselines3, RLlib, PettingZoo; MDP → PPO, multi-agent; reproducible
environments. MLOps: MLflow, DVC, Optuna, W&B, BentoML, ONNX Runtime, Docker, Kubernetes,
FastAPI; project "Production ML Service" (data → train → eval → tracking → export → API →
container → tests → deploy → monitoring). Data engineering: Pandas, Polars, PyArrow,
DuckDB, SQLAlchemy, Airflow, Prefect, Dagster, PySpark, dbt — basics must not need Spark or
Kubernetes. Testing & quality: pytest, unittest, Hypothesis, pytest-asyncio, coverage,
tox, nox, Ruff, mypy, Pyright, Black, pre-commit. Performance: profiling (cProfile, timeit,
tracemalloc), refcounting, GC, GIL, vectorization, memory layout, C extensions, Cython,
pybind11, Numba; CPython internals (bytecode, AST, import system, interpreter
architecture). Desktop: Tkinter, PySide6, PyQt6, Kivy, Flet, Textual.

Python Environment Manager (part of Runtime Manager): detect Python versions, choose
interpreter, create/delete venv (venv, uv), install deps (pip/uv, requirements.txt,
pyproject.toml) only on explicit user action, list installed packages, validate
environment, run programs/tests inside it; never install globally without explicit
decision; consider Python version / OS / CPU arch / accelerator compatibility.

ML Runtime Manager: detect CPU, NVIDIA/CUDA, AMD/ROCm, Apple Silicon/Metal/MPS, memory,
drivers; a GPU does not imply framework support — check OS, arch, framework version, Python
version, backend actually available (probe inside the env). CUDA never on macOS; ROCm
support differs Linux vs Windows; MPS not in every framework. Capability-based execution;
clear explanation when acceleration is unavailable; CPU-first exercises work everywhere;
GPU exercises separate and optional.

Interactive ML workspace: notebook-style cells, persistent kernel state, outputs,
DataFrames, charts, formulas, history, variable inspection — via existing Jupyter
protocols (no custom kernel); .ipynb support designed in; can be a later phase; no WebView
main UI.
ML visualizations (interactive, mathematically real): linear regression, gradient descent
(lab: choose function, learning rate, start point, run, watch parameters, loss history),
decision trees, K-Means, PCA, neural nets, backprop, matrix multiplication, activations,
losses, attention, transformer, training curves, over/underfitting.

Python projects catalog (Fundamentals, Backend, Automation, DS, ML, DL, LLM, MLOps,
Performance) — each with description, requirements, prerequisites, acceptance criteria,
milestones, tests, extra tasks, architecture guidance, evaluation. AI tutor helps without
revealing solutions unless asked; for ML it may use dataset metadata, metrics, loss,
accuracy, training config — never replaces deterministic checks.

ML exercise quality: reproducible datasets, fixed seeds, numeric tolerances, shape/dtype
checks, correctness of computations, no mandatory GPU; a training script that merely
finishes never counts as success.

Python roadmap phases: A Core (fundamentals, stdlib, OOP, typing, async, testing) · B Backend
· C Automation · D Data Science · E ML (math, classical, from scratch) · F Deep Learning ·
G LLM engineering · H MLOps · I Advanced Python (internals, memory, perf, C extensions,
distributed, advanced concurrency) · J Additional (CV, NLP, RL, data engineering, desktop,
security).

Python acceptance criteria (verify each for real): choose Python and a specialization, see
the path, study theory, write code, choose interpreter, create venv, install deps, run,
run tests, multi-file project, study a library, exercise, AI code review, save progress,
switch specialization, resume after restart, offline exercises, ML datasets, CPU ML
exercises; advanced: notebooks, training, metrics, GPU when supported, LLM inference, RAG,
fine-tuning, deploy inference service.

## C. Languages added

Rust and Kotlin are supported languages alongside Python, PHP, JavaScript, TypeScript, C#,
Java, Go, C++.
