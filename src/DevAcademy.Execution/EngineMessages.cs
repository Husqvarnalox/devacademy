using DevAcademy.Domain;

namespace DevAcademy.Execution;

/// <summary>
/// Catalog of the user-visible messages of the execution layer. Each <c>const</c> is a resource key a UI translates (the desktop looks it up
/// in <c>Strings.resx</c>); the factory beside it holds the English template that stays the fallback for the CLI and logs. The key constants are
/// enumerated by reflection in a test, so every new message must be declared here. Paths, versions, command lines and tool output are arguments
/// or <see cref="LocalizedText.Literal"/> and are never translated.
/// </summary>
public static class EngineMessages
{
    // ------------------------------------------------------------------ runtime detection
    public const string ToolNotFoundKey = "Engine_Runtime_ToolNotFound";
    public const string ToolNotWorkingKey = "Engine_Runtime_ToolNotWorking";
    public const string JdkNotFoundKey = "Engine_Runtime_JdkNotFound";
    public const string JdkNotWorkingKey = "Engine_Runtime_JdkNotWorking";
    public const string KotlinNeedsJdkKey = "Engine_Runtime_KotlinNeedsJdk";
    public const string CppNotFoundKey = "Engine_Runtime_CppNotFound";

    public const string PythonHintKey = "Engine_Hint_Python";
    public const string NodeHintKey = "Engine_Hint_Node";
    public const string TscHintKey = "Engine_Hint_Tsc";
    public const string GoHintKey = "Engine_Hint_Go";
    public const string GoWorkingHintKey = "Engine_Hint_GoWorking";
    public const string JdkHintKey = "Engine_Hint_Jdk";
    public const string CppHintKey = "Engine_Hint_Cpp";
    public const string CppUnsupportedKey = "Engine_Runtime_CppUnsupported";
    public const string CppFallback20Key = "Engine_Runtime_CppFallback20";
    public const string CppNoExpectedKey = "Engine_Runtime_CppNoExpected";
    public const string RustHintKey = "Engine_Hint_Rust";
    public const string DotNetHintKey = "Engine_Hint_DotNet";
    public const string KotlinHintKey = "Engine_Hint_Kotlin";
    public const string PhpHintKey = "Engine_Hint_Php";
    public const string PhpOldKey = "Engine_Runtime_PhpOld";

    public const string PackageManagersKey = "Engine_Runtime_PackageManagers";
    public const string PackageManagerMissingKey = "Engine_Runtime_PackageManagerMissing";
    public const string CSharpNotesKey = "Engine_Runtime_CSharpNotes";
    public const string CSharpUnsupportedKey = "Engine_Runtime_CSharpUnsupported";
    public const string NodeTooOldKey = "Engine_Runtime_NodeTooOldForTs";
    public const string TsStrippingKey = "Engine_Runtime_TsStripping";
    public const string TsNoCheckerKey = "Engine_Runtime_TsNoChecker";
    public const string TsNoTypesKey = "Engine_Runtime_TsNoTypes";
    public const string TsCheckerEnabledKey = "Engine_Runtime_TsCheckerEnabled";
    public const string TsNoCheckerWarningKey = "Engine_Runtime_TsNoCheckerWarning";
    public const string TsNoTypesWarningKey = "Engine_Runtime_TsNoTypesWarning";
    public const string KotlinNoCoroutinesKey = "Engine_Runtime_KotlinNoCoroutines";
    public const string KotlinCoroutinesKey = "Engine_Runtime_KotlinCoroutines";

    public static LocalizedText ToolNotFound(string tool) => LocalizedText.Create(ToolNotFoundKey, "{0} was not found on PATH.", tool);
    public static LocalizedText ToolNotWorking(string tool) => LocalizedText.Create(ToolNotWorkingKey, "A working {0} was not found on PATH.", tool);
    public static LocalizedText JdkNotFound() => LocalizedText.Create(JdkNotFoundKey, "java and javac (a JDK) was not found on PATH.");
    public static LocalizedText JdkNotWorking() => LocalizedText.Create(JdkNotWorkingKey, "A working JDK was not found on PATH.");
    public static LocalizedText KotlinNeedsJdk() => LocalizedText.Create(KotlinNeedsJdkKey, "A working java (kotlinc needs a JDK) was not found on PATH.");
    public static LocalizedText CppNotFound() => LocalizedText.Create(CppNotFoundKey, "clang++ or g++ was not found on PATH.");

    public static LocalizedText PythonHint() => LocalizedText.Create(PythonHintKey, "Install Python 3 from https://www.python.org/downloads/ (macOS: brew install python; Debian/Ubuntu: apt install python3).");
    public static LocalizedText NodeHint() => LocalizedText.Create(NodeHintKey, "Install Node.js from https://nodejs.org/ (macOS: brew install node; Debian/Ubuntu: apt install nodejs).");
    public static LocalizedText TscHint() => LocalizedText.Create(TscHintKey, "Install the TypeScript compiler for type-checking: npm install -g typescript (https://www.typescriptlang.org/download).");
    public static LocalizedText GoHint() => LocalizedText.Create(GoHintKey, "Install Go from https://go.dev/dl/ (macOS: brew install go; Debian/Ubuntu: apt install golang).");
    public static LocalizedText GoWorkingHint() => LocalizedText.Create(GoWorkingHintKey, "Install Go from https://go.dev/dl/.");
    public static LocalizedText JdkHint() => LocalizedText.Create(JdkHintKey, "Install a JDK (17+) from https://adoptium.net/ (macOS: brew install openjdk; Debian/Ubuntu: apt install default-jdk).");
    public static LocalizedText CppHint() => LocalizedText.Create(CppHintKey, "Install a C++23-capable compiler (courses build with -std=c++23 and use std::expected: GCC 13+ or Clang 17+; Apple clang from Xcode 16): macOS: xcode-select --install; Debian/Ubuntu: apt install g++ (g++-13 or newer); Windows: https://visualstudio.microsoft.com/downloads/ or MSYS2.");
    public static LocalizedText CppFallback20() => LocalizedText.Create(CppFallback20Key, "This compiler does not support C++23, so programs are built as C++20. Most lessons work; examples that use C++23 (std::expected) will not compile. Update to GCC 13+ or Clang 17+ to build everything.");
    public static LocalizedText CppNoExpected() => LocalizedText.Create(CppNoExpectedKey, "This compiler builds C++23 but its standard library has no std::expected (for example clang with libstdc++ 13), so examples that use it will not compile. Install g++ 13 or newer, or Clang 17+ with libc++, to build everything.");
    public static LocalizedText CppUnsupported() => LocalizedText.Create(CppUnsupportedKey, "The compiler accepts neither -std=c++23 nor -std=c++20; update it (GCC 13+ or Clang 17+).");
    public static LocalizedText RustHint() => LocalizedText.Create(RustHintKey, "https://rustup.rs");
    public static LocalizedText DotNetHint() => LocalizedText.Create(DotNetHintKey, "Install the .NET 10 SDK from https://dotnet.microsoft.com/download (macOS: brew install --cask dotnet-sdk; Debian/Ubuntu: apt install dotnet-sdk-10.0).");
    public static LocalizedText KotlinHint() => LocalizedText.Create(KotlinHintKey, "https://kotlinlang.org/docs/command-line.html (also requires a JDK).");
    public static LocalizedText PhpOld() => LocalizedText.Create(PhpOldKey, "This PHP is older than 8.4. The PHP courses were verified on PHP 8.5 and use functions added in 8.4 (mb_trim, array_find, property hooks); those examples will fail on this version. Update PHP to 8.4 or newer.");
    public static LocalizedText PhpHint() => LocalizedText.Create(PhpHintKey, "Install PHP 8 from https://www.php.net/downloads (macOS: brew install php; Debian/Ubuntu: apt install php-cli).");

    /// <param name="items">One entry per manager: a literal "npm 10.9.2" or <see cref="PackageManagerMissing"/>.</param>
    public static LocalizedText PackageManagers(IEnumerable<LocalizedText> items)
    {
        LocalizedText list = LocalizedText.Join(", ", items);
        return LocalizedText.Create(PackageManagersKey, "Package managers: {0}.", list);
    }

    public static LocalizedText PackageManagerMissing(string name) => LocalizedText.Create(PackageManagerMissingKey, "{0} (not found)", name);
    public static LocalizedText CSharpNotes() => LocalizedText.Create(CSharpNotesKey, "Only the entry file is compiled (file-based app), unless the files hold exactly one .csproj (then the project is built).");
    public static LocalizedText CSharpUnsupported() => LocalizedText.Create(CSharpUnsupportedKey, ".NET SDK 10 or newer is required for file-based apps.");
    public static LocalizedText NodeTooOldForTs(string version) => LocalizedText.Create(NodeTooOldKey, "Node.js {0} cannot run TypeScript directly; Node.js 22.6 or newer is required.", version);
    public static LocalizedText TsStripping() => LocalizedText.Create(TsStrippingKey, "Runs via Node type stripping (no enums/namespaces).");
    public static LocalizedText TsNoChecker() => LocalizedText.Create(TsNoCheckerKey, "Type-checking is UNAVAILABLE (tsc not found): type errors will not be reported.");
    public static LocalizedText TsNoTypes(string tscVersion) => LocalizedText.Create(TsNoTypesKey, "{0} found, but type-checking unavailable: @types/node not provisioned.", tscVersion);
    public static LocalizedText TsCheckerEnabled(string tscVersion) => LocalizedText.Create(TsCheckerEnabledKey, "Type-checking with tsc ({0}) is enabled.", tscVersion);
    public static LocalizedText TsNoCheckerWarning() => LocalizedText.Create(TsNoCheckerWarningKey, "type-checking unavailable: tsc not installed");
    public static LocalizedText TsNoTypesWarning() => LocalizedText.Create(TsNoTypesWarningKey, "type-checking unavailable: @types/node not provisioned");
    public static LocalizedText KotlinNoCoroutines() => LocalizedText.Create(KotlinNoCoroutinesKey, "kotlinx.coroutines: not found next to kotlinc (programs can use only the standard library).");
    public static LocalizedText KotlinCoroutines() => LocalizedText.Create(KotlinCoroutinesKey, "kotlinx.coroutines: available (jar shipped with the kotlinc distribution).");

    // ------------------------------------------------------------------ run preparation
    public const string UnknownLanguageKey = "Engine_Run_UnknownLanguage";
    public const string PythonNoInterpreterKey = "Engine_Run_PythonNoInterpreter";
    public const string PythonEnvMissingKey = "Engine_Run_PythonEnvMissing";
    public const string PythonEnvWritableKey = "Engine_Run_PythonEnvWritable";
    public const string NodeOnlyJsKey = "Engine_Run_NodeOnlyJs";
    public const string NodeNoModulesKey = "Engine_Run_NodeNoModules";
    public const string NodeEnvWritableKey = "Engine_Run_NodeEnvWritable";
    public const string NodeLinkFailedKey = "Engine_Run_NodeLinkFailed";
    public const string GoOnlyGoKey = "Engine_Run_GoOnlyGo";
    public const string GoNoModulesKey = "Engine_Run_GoNoModules";
    public const string GoCacheWritableKey = "Engine_Run_GoCacheWritable";
    public const string NuGetOnlyCSharpKey = "Engine_Run_NuGetOnlyCSharp";
    public const string NuGetNoPackagesKey = "Engine_Run_NuGetNoPackages";
    public const string NuGetWritableKey = "Engine_Run_NuGetWritable";
    public const string MavenOnlyJavaKey = "Engine_Run_MavenOnlyJava";
    public const string MavenNoJarsKey = "Engine_Run_MavenNoJars";
    public const string MavenWritableKey = "Engine_Run_MavenWritable";
    public const string NoTestCommandKey = "Engine_Run_NoTestCommand";
    public const string CommandNotAllowedKey = "Engine_Run_CommandNotAllowed";
    public const string RuntimeUnavailableKey = "Engine_Run_RuntimeUnavailable";
    public const string CancelledWhileWaitingKey = "Engine_Run_CancelledWhileWaiting";
    public const string CompileStartFailedKey = "Engine_Run_CompileStartFailed";
    public const string CompileTimeoutKey = "Engine_Run_CompileTimeout";
    public const string CompileMemoryKey = "Engine_Run_CompileMemory";
    public const string CompileOutputKey = "Engine_Run_CompileOutput";
    public const string CompileCancelledKey = "Engine_Run_CompileCancelled";
    public const string RunStartFailedKey = "Engine_Run_StartFailed";

    public static LocalizedText UnknownLanguage(string id) => LocalizedText.Create(UnknownLanguageKey, "Unknown language '{0}'.", id);
    public static LocalizedText PythonNoInterpreter(string path) => LocalizedText.Create(PythonNoInterpreterKey, "The selected Python environment has no interpreter at '{0}'.", path);
    public static LocalizedText PythonEnvMissing(string path) => LocalizedText.Create(PythonEnvMissingKey, "The selected Python environment '{0}' does not exist.", path);
    public static LocalizedText PythonEnvWritable() => LocalizedText.Create(PythonEnvWritableKey, "The selected Python environment lies inside a writable sandbox area and cannot be mounted read-only.");
    public static LocalizedText NodeOnlyJs() => LocalizedText.Create(NodeOnlyJsKey, "A node environment only applies to JavaScript and TypeScript.");
    public static LocalizedText NodeNoModules(string path) => LocalizedText.Create(NodeNoModulesKey, "The node environment '{0}' has no node_modules (run `make node-env` or install the course packages in Settings).", path);
    public static LocalizedText NodeEnvWritable() => LocalizedText.Create(NodeEnvWritableKey, "The node environment lies inside a writable sandbox area and cannot be mounted read-only.");
    public static LocalizedText NodeLinkFailed(string error) => LocalizedText.Create(NodeLinkFailedKey, "Could not link the node environment into the work directory: {0}", error);
    public static LocalizedText GoOnlyGo() => LocalizedText.Create(GoOnlyGoKey, "A Go module cache only applies to Go.");
    public static LocalizedText GoNoModules(string path) => LocalizedText.Create(GoNoModulesKey, "The Go module cache '{0}' has no downloaded modules (run `make go-env` or install the course packages in Settings).", path);
    public static LocalizedText GoCacheWritable() => LocalizedText.Create(GoCacheWritableKey, "The Go module cache lies inside a writable sandbox area and cannot be mounted read-only.");
    public static LocalizedText NuGetOnlyCSharp() => LocalizedText.Create(NuGetOnlyCSharpKey, "A NuGet packages folder only applies to C#.");
    public static LocalizedText NuGetNoPackages(string path) => LocalizedText.Create(NuGetNoPackagesKey, "The NuGet packages folder '{0}' has no restored packages (run `make nuget-env` or install the course packages in Settings).", path);
    public static LocalizedText NuGetWritable() => LocalizedText.Create(NuGetWritableKey, "The NuGet packages folder lies inside a writable sandbox area and cannot be mounted read-only.");
    public static LocalizedText MavenOnlyJava() => LocalizedText.Create(MavenOnlyJavaKey, "A Maven jar folder only applies to Java.");
    public static LocalizedText MavenNoJars(string path) => LocalizedText.Create(MavenNoJarsKey, "The Maven jar folder '{0}' has no jars (run `make maven-env` or install the course libraries in Settings).", path);
    public static LocalizedText MavenWritable() => LocalizedText.Create(MavenWritableKey, "The Maven jar folder lies inside a writable sandbox area and cannot be mounted read-only.");
    public static LocalizedText NoTestCommand(string language) => LocalizedText.Create(NoTestCommandKey, "{0} has no test command support.", language);
    public static LocalizedText CommandNotAllowed(string command, string language, string tool) => LocalizedText.Create(CommandNotAllowedKey, "Command '{0}' is not allowed for {1} (only '{2}').", command, language, tool);
    /// <summary>"{language} is not available: {notes} {hint}" with the runtime's own (already localizable) notes and install hint.</summary>
    public static LocalizedText RuntimeUnavailable(string language, LocalizedText? notes, LocalizedText? hint)
    {
        LocalizedText head = LocalizedText.Create(RuntimeUnavailableKey, "{0} is not available: {1}", language, notes ?? LocalizedText.Literal(""));
        return LocalizedText.Join(" ", head, hint);
    }

    public static LocalizedText CancelledWhileWaiting() => LocalizedText.Create(CancelledWhileWaitingKey, "Cancelled while waiting to compile.");
    public static LocalizedText CompileStartFailed(Exception error) => LocalizedText.Create(CompileStartFailedKey, "Could not start compile step: {0}", Describe(error));
    public static LocalizedText CompileStopped(ExecutionStatus status) => status switch
    {
        ExecutionStatus.Timeout => LocalizedText.Create(CompileTimeoutKey, "Compilation: Timeout."),
        ExecutionStatus.MemoryLimitExceeded => LocalizedText.Create(CompileMemoryKey, "Compilation: MemoryLimitExceeded."),
        ExecutionStatus.OutputLimitExceeded => LocalizedText.Create(CompileOutputKey, "Compilation: OutputLimitExceeded."),
        _ => LocalizedText.Create(CompileCancelledKey, "Compilation: Cancelled."),
    };

    public static LocalizedText RunStartFailed(Exception error) => LocalizedText.Create(RunStartFailedKey, "Could not run program: {0}", Describe(error));

    /// <summary>The translatable message of an exception that carries one, otherwise its text as verbatim (operating-system) text.</summary>
    public static LocalizedText Describe(Exception error) => error is ILocalizedMessage l ? l.Text : LocalizedText.Literal(error.Message);

    // ------------------------------------------------------------------ isolation
    public const string PythonUsesOsSandboxKey = "Engine_Isolation_PythonEnv";
    public const string NodeUsesOsSandboxKey = "Engine_Isolation_NodeEnv";
    public const string GoUsesOsSandboxKey = "Engine_Isolation_GoCache";
    public const string NuGetUsesOsSandboxKey = "Engine_Isolation_NuGetFolder";
    public const string MavenUsesOsSandboxKey = "Engine_Isolation_MavenFolder";
    public const string CommandUsesOsSandboxKey = "Engine_Isolation_TestCommand";
    public const string ContainerUnsupportedKey = "Engine_Isolation_ContainerUnsupported";
    public const string IsolationUnavailableKey = "Engine_Isolation_Unavailable";
    public const string FallbackDisabledKey = "Engine_Isolation_FallbackDisabled";
    public const string NoSandboxKey = "Engine_Isolation_NoSandbox";
    public const string DockerImageMissingKey = "Engine_Isolation_DockerImageMissing";
    public const string DockerNotFoundKey = "Engine_Isolation_DockerNotFound";

    public static LocalizedText PythonUsesOsSandbox() => LocalizedText.Create(PythonUsesOsSandboxKey, "A Python environment is selected, so the run uses OsSandbox instead of a container.");
    public static LocalizedText NodeUsesOsSandbox() => LocalizedText.Create(NodeUsesOsSandboxKey, "A node environment is selected, so the run uses OsSandbox instead of a container.");
    public static LocalizedText GoUsesOsSandbox() => LocalizedText.Create(GoUsesOsSandboxKey, "A Go module cache is selected, so the run uses OsSandbox instead of a container.");
    public static LocalizedText NuGetUsesOsSandbox() => LocalizedText.Create(NuGetUsesOsSandboxKey, "A NuGet packages folder is selected, so the run uses OsSandbox instead of a container.");
    public static LocalizedText MavenUsesOsSandbox() => LocalizedText.Create(MavenUsesOsSandboxKey, "A Maven jar folder is selected, so the run uses OsSandbox instead of a container.");
    public static LocalizedText CommandUsesOsSandbox() => LocalizedText.Create(CommandUsesOsSandboxKey, "A test command runs on the host, so the run uses OsSandbox instead of a container.");
    public static LocalizedText ContainerUnsupported(string language) => LocalizedText.Create(ContainerUnsupportedKey, "Container isolation is not supported for {0}.", language);
    public static LocalizedText IsolationUnavailable(IsolationLevel level) => LocalizedText.Create(IsolationUnavailableKey, "{0} isolation is not available on this machine.", level.ToString());
    public static LocalizedText FallbackDisabled() => LocalizedText.Create(FallbackDisabledKey, "Fallback is disabled.");
    public static LocalizedText NoSandbox() => LocalizedText.Create(NoSandboxKey, "No sandbox could be selected.");
    public static LocalizedText DockerImageMissing(string image) => LocalizedText.Create(DockerImageMissingKey, "Docker image '{0}' is not available locally (docker pull {0}).", image);
    public static LocalizedText DockerNotFound() => LocalizedText.Create(DockerNotFoundKey, "docker executable not found.");

    // ------------------------------------------------------------------ provisioning and debug builds
    public const string NodeUnavailableKey = "Engine_Provision_NodeUnavailable";
    public const string NpmNotFoundKey = "Engine_Provision_NpmNotFound";
    public const string NpmTimedOutKey = "Engine_Provision_NpmTimedOut";
    public const string NoDebugBuildKey = "Engine_Debug_NoBuild";
    public const string CompilerFailedKey = "Engine_Debug_CompilerFailed";

    public static LocalizedText NodeUnavailable() => LocalizedText.Create(NodeUnavailableKey, "Node.js is not available.");
    public static LocalizedText NpmNotFound() => LocalizedText.Create(NpmNotFoundKey, "npm was not found on PATH.");
    public static LocalizedText NpmTimedOut() => LocalizedText.Create(NpmTimedOutKey, "npm install timed out.");
    public static LocalizedText NoDebugBuild(string language) => LocalizedText.Create(NoDebugBuildKey, "No debug build for '{0}'.", language);
    public static LocalizedText CompilerFailed() => LocalizedText.Create(CompilerFailedKey, "The compiler failed.");
}
