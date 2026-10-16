# DevAcademy developer commands. Every target here is real; see CONTRIBUTING.md.
SLN      := DevAcademy.slnx
DESKTOP  := src/DevAcademy.Desktop
CLI      := src/DevAcademy.Cli
CONTENT  := content
RID      ?= $(shell dotnet --info 2>/dev/null | awk '/^ *RID:/ {print $$2; exit}')
CONFIG   ?= Release

.PHONY: help setup dev build test test-fast test-runners lint format validate-content \
        runtimes publish clean

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
	@echo "runtimes          list language runtimes the app can detect"
	@echo "publish           self-contained desktop build for RID=$(RID) into artifacts/"
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
	dotnet run --project $(CLI) -- validate-content --content $(CONTENT) --run-solutions

runtimes:
	dotnet run --project $(CLI) -- runtimes

publish:
	dotnet publish $(DESKTOP) -c Release -r $(RID) --self-contained true \
	  -p:PublishSingleFile=true -o artifacts/$(RID)
	cp -R $(CONTENT) artifacts/$(RID)/content
	cd artifacts && shasum -a 256 -b $$(find $(RID) -maxdepth 1 -type f) > $(RID).sha256

clean:
	dotnet clean $(SLN) -v q
	rm -rf artifacts TestResults
