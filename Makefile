# DevAcademy developer commands. Every target here is real; see CONTRIBUTING.md.
SLN      := DevAcademy.slnx
DESKTOP  := src/DevAcademy.Desktop
CLI      := src/DevAcademy.Cli
CONTENT  := content
RID      ?= $(shell dotnet --info 2>/dev/null | awk '/^ *RID:/ {print $$2; exit}')
CONFIG   ?= Release
PYENV    ?= .venv-content
PYTHON   ?= python3
# Windows venvs keep the interpreter in Scripts/; the CLI finds either layout, this only decides how to create it.
PYENV_BIN := $(if $(filter Windows_NT,$(OS)),Scripts,bin)
# Use the content venv automatically when it exists (otherwise exercises that need packages only warn).
PYENV_ARG = $(if $(wildcard $(PYENV)),--python-env $(PYENV),)
# Node content environment: node_modules for React/Vitest exercises, pinned by tools/node-content/package-lock.json.
NODEENV  ?= .node-content
NODEENV_ARG = $(if $(wildcard $(NODEENV)/node_modules),--node-env $(NODEENV),)

.PHONY: help setup dev build test test-fast test-runners lint format validate-content \
        python-env node-env runtimes publish package checksums version notices clean

help:
	@echo "setup             restore packages and report detected language runtimes"
	@echo "dev               run the desktop app (Debug)"
	@echo "build             build the whole solution"
	@echo "test              run all test projects"
	@echo "test-fast         all tests except the slow runner suite"
	@echo "test-runners      code execution engine tests (real toolchains, sandbox)"
	@echo "lint              whitespace/format verification (no changes)"
	@echo "format            apply formatting"
	@echo "validate-content  validate content + run reference solutions and samples"
	@echo "python-env        create $(PYENV) and pip-install tools/python-dev-requirements.txt (DOWNLOADS from PyPI)"
	@echo "node-env          npm ci tools/node-content into $(NODEENV) (Vitest, React, Testing Library; DOWNLOADS from npm)"
	@echo "runtimes          list language runtimes the app can detect"
	@echo "publish           self-contained desktop build for RID=$(RID) into artifacts/"
	@echo "package           installer(s) for RID=$(RID) on this OS into artifacts/package (.app+.dmg on macOS, AppImage+.deb on Linux)"
	@echo "checksums         SHA256SUMS for artifacts/package"
	@echo "version           print the product version (Directory.Build.props)"
	@echo "notices           regenerate THIRD-PARTY-NOTICES.md"
	@echo "clean             remove build outputs"

setup:
	dotnet restore $(SLN)
	@$(MAKE) --no-print-directory runtimes

dev:
	dotnet run --project $(DESKTOP)

build:
	dotnet build $(SLN) -c $(CONFIG)

test:
	dotnet test $(SLN)

test-fast:
	@for p in tests/*/*.csproj; do \
	  case $$p in *Execution.Tests*) continue;; esac; \
	  dotnet test $$p || exit 1; \
	done

test-runners:
	dotnet test tests/DevAcademy.Execution.Tests

lint:
	dotnet format whitespace $(SLN) --verify-no-changes

format:
	dotnet format whitespace $(SLN)

validate-content:
	dotnet run --project $(CLI) -- validate-content --content $(CONTENT) --run-solutions $(PYENV_ARG) $(NODEENV_ARG)
	@if [ -z "$(PYENV_ARG)" ]; then echo "note: no $(PYENV); exercises that need Python packages were skipped (run: make python-env)"; fi
	@if [ -z "$(NODEENV_ARG)" ]; then echo "note: no $(NODEENV); exercises that need npm packages were skipped (run: make node-env)"; fi

python-env:
	@echo "NOTE: this downloads packages from PyPI into $(PYENV) (nothing is installed globally)."
	$(PYTHON) -m venv $(PYENV)
	$(PYENV)/$(PYENV_BIN)/python -m pip install --disable-pip-version-check -r tools/python-dev-requirements.txt
	-dotnet run --project $(CLI) -- python-env-check --python-env $(PYENV) --content $(CONTENT)

node-env:
	@echo "NOTE: this downloads packages from npm into $(NODEENV) (nothing is installed globally; install scripts are disabled)."
	mkdir -p $(NODEENV)
	cp tools/node-content/package.json tools/node-content/package-lock.json $(NODEENV)/
	cd $(NODEENV) && npm ci --ignore-scripts --no-audit --no-fund

runtimes:
	dotnet run --project $(CLI) -- runtimes

publish:
	dotnet publish $(DESKTOP) -c Release -r $(RID) --self-contained true \
	  -p:PublishSingleFile=true -o artifacts/$(RID)
	cp -R $(CONTENT) artifacts/$(RID)/content
	mkdir -p artifacts/$(RID)/node-content && cp tools/node-content/package.json tools/node-content/package-lock.json artifacts/$(RID)/node-content/
	cd artifacts && shasum -a 256 -b $$(find $(RID) -maxdepth 1 -type f) > $(RID).sha256

version:
	@packaging/version.sh

notices:
	python3 tools/generate-third-party-notices.py

# Runs the packaging scripts for the current OS (Windows: run packaging/windows/build-windows.sh in Git Bash).
package:
	@case "$$(uname -s)" in \
	  Darwin) packaging/macos/build-app.sh $(RID) ;; \
	  Linux)  packaging/linux/build-appimage.sh $(RID) && packaging/linux/build-deb.sh $(RID) ;; \
	  *)      packaging/windows/build-windows.sh $(RID) ;; \
	esac

checksums:
	packaging/checksums.sh artifacts/package

clean:
	dotnet clean $(SLN) -v q
	rm -rf artifacts TestResults
