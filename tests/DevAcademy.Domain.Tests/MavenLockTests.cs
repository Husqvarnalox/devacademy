using DevAcademy.Domain;

namespace DevAcademy.Domain.Tests;

public class MavenLockTests
{
    private static readonly string Sha = new('a', 64);

    private static string Json(string jar = null!, string source = "https://repo1.maven.org/maven2") => $$"""
        { "set": "s", "source": "{{source}}", "jars": [ {{jar ?? Jar()}} ] }
        """;

    private static string Jar(string group = "org.example", string artifact = "lib", string version = "1.2", string? file = null, string sha = null!, string size = "10") =>
        $$"""{ "groupId": "{{group}}", "artifactId": "{{artifact}}", "version": "{{version}}", "file": "{{file ?? artifact + "-" + version + ".jar"}}", "sha256": "{{sha ?? Sha}}", "size": {{size}} }""";

    [Fact]
    public void ParsesAJarAndBuildsItsRepositoryUrl()
    {
        Assert.True(MavenLock.TryParse(Json(), out var lockFile, out var error), error);
        var jar = Assert.Single(lockFile.Jars);
        Assert.Equal("org.example:lib", jar.Coordinates);
        Assert.Equal("org/example/lib/1.2/lib-1.2.jar", jar.RepositoryPath);
        Assert.Equal("https://repo1.maven.org/maven2/org/example/lib/1.2/lib-1.2.jar", lockFile.UrlOf(jar).ToString());
        Assert.Equal("repo1.maven.org", lockFile.Host);
        Assert.Equal(10, lockFile.TotalBytes);
    }

    [Theory]
    [InlineData("http://repo1.maven.org/maven2")]
    [InlineData("not a url")]
    public void TheSourceMustBeHttps(string source) => Assert.False(MavenLock.TryParse(Json(source: source), out _, out _));

    [Fact]
    public void RejectsMalformedEntries()
    {
        Assert.False(MavenLock.TryParse(Json(Jar(sha: "ABC")), out _, out _));
        Assert.False(MavenLock.TryParse(Json(Jar(size: "0")), out _, out _));
        Assert.False(MavenLock.TryParse(Json(Jar(file: "other.jar")), out _, out _));
        Assert.False(MavenLock.TryParse(Json(Jar(group: "../etc")), out _, out _));
        Assert.False(MavenLock.TryParse(Json(Jar(artifact: "a/b")), out _, out _));
        Assert.False(MavenLock.TryParse("""{ "set": "s", "source": "https://x.org", "jars": [] }""", out _, out _));
        Assert.False(MavenLock.TryParse("[", out _, out _));
    }

    [Fact]
    public void RejectsADuplicateJar() =>
        Assert.False(MavenLock.TryParse(Json(Jar() + "," + Jar()), out _, out var error) || error is null);

    [Fact]
    public void ASetCoversAnExerciseThatNamesOnlyItsArtifactsAndNeverAFromAnotherLanguage()
    {
        var web = new ContentPackageSet("web", "Web", PackageSetLanguage.Maven, "", ["org.springframework:spring-test", "org.springframework.boot:spring-boot-starter-webmvc"]);
        var node = new ContentPackageSet("node", "Node", PackageSetLanguage.Node, "", ["org.springframework:spring-test"]);
        var index = new ContentPackageIndex([node, web]);
        Assert.Equal("web", index.Resolve(PackageSetLanguage.Maven, ["org.springframework:spring-test"]).Set!.Id);
        Assert.Null(index.Resolve(PackageSetLanguage.Maven, ["org.springframework:missing"]).Set);
    }

    [Fact]
    public void RequirementsKnowMavenAndValidateCoordinates()
    {
        var needs = new ExerciseRequirements(null, [], MavenArtifacts: ["org.springframework:spring-test"]);
        Assert.True(needs.HasMaven);
        Assert.False(needs.IsEmpty);
        Assert.False(new ExerciseRequirements(null, []).HasMaven);
    }
}
