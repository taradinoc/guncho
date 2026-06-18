using Guncho;
using Guncho.Services;
using Moq;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Guncho.Engine.Tests;

public class Inform6RealmFactoryTests
{
    private readonly Mock<IServerConfiguration> _mockConfig;
    private readonly Mock<ILogger> _mockLogger;
    private readonly string _testCompilerPath;
    private readonly string _testLibraryPath;
    private readonly string _testIndexPath;

    public Inform6RealmFactoryTests()
    {
        _mockConfig = new Mock<IServerConfiguration>();
        _mockLogger = new Mock<ILogger>();
        _testCompilerPath = "e:\\guncho\\Factories\\Inform6";
        _testLibraryPath = "e:\\guncho\\Factories\\Inform6\\library";
        _testIndexPath = Path.Combine(Path.GetTempPath(), "guncho-test-index");

        // Setup basic config
        _mockConfig.Setup(c => c.CompilerTimeout).Returns(300000);
        _mockConfig.Setup(c => c.CachePath).Returns(Path.GetTempPath());
    }

    [Fact]
    public void Constructor_SetsPropertiesCorrectly()
    {
        // Arrange & Act
        var factory = new Inform6RealmFactory(
            _mockConfig.Object,
            _mockLogger.Object,
            "Inform 6",
            _testCompilerPath,
            _testLibraryPath,
            _testIndexPath);

        // Assert
        Assert.Equal("Inform 6", factory.Name);
        Assert.Equal(".inf", factory.SourceFileExtension);
        Assert.Equal("Inform 6 with Glulx support", factory.Description);
    }

    [Fact]
    public void GetInitialSourceText_ReturnsValidInform6Code()
    {
        // Arrange
        var factory = new Inform6RealmFactory(
            _mockConfig.Object,
            _mockLogger.Object,
            "Inform 6",
            _testCompilerPath,
            _testLibraryPath,
            _testIndexPath);

        // Act
        var source = factory.GetInitialSourceText("TestOwner", "TestRealm");

        // Assert
        Assert.Contains("Constant Story \"TestRealm\"", source);
        Assert.Contains("Include \"Parser\"", source);
        Assert.Contains("Include \"VerbLib\"", source);
        Assert.Contains("[ Initialise;", source);
        Assert.Contains("Object Room1", source);
        Assert.Contains("created by TestOwner", source);
    }

    [Fact]
    public void ConstructAll_WithValidPaths_CreatesFactory()
    {
        // Arrange
        var compilerPath = _testCompilerPath;
        var libraryPath = _testLibraryPath;

        // Skip test if compiler/library don't exist
        if (!Directory.Exists(compilerPath) || !Directory.Exists(libraryPath))
        {
            // Skip test in CI or environments without Inform 6 installed
            return;
        }

        // Act
        var factories = Inform6RealmFactory.ConstructAll(
            _mockConfig.Object,
            _mockLogger.Object,
            compilerPath,
            libraryPath,
            _testIndexPath);

        // Assert
        Assert.Single(factories);
        Assert.Equal("Inform 6", factories[0].Name);
    }

    [Fact]
    public void ConstructAll_WithDirectoryPath_FindsCompiler()
    {
        // Arrange - use directory path to test platform-specific binary detection
        var compilerDir = _testCompilerPath;
        var libraryPath = _testLibraryPath;

        // Skip test if directory or library don't exist
        if (!Directory.Exists(compilerDir) || !Directory.Exists(libraryPath))
        {
            return;
        }

        // Act
        var factories = Inform6RealmFactory.ConstructAll(
            _mockConfig.Object,
            _mockLogger.Object,
            compilerDir,
            libraryPath,
            _testIndexPath);

        // Assert - Should find platform-appropriate binary
        if (Directory.GetFiles(compilerDir, "inform6*").Any())
        {
            Assert.Single(factories);
        }
    }

    [Fact]
    public void ConstructAll_WithInvalidPaths_ReturnsEmptyArray()
    {
        // Arrange
        var invalidCompilerPath = "e:\\nonexistent\\inform6.exe";
        var invalidLibraryPath = "e:\\nonexistent\\library";

        // Act
        var factories = Inform6RealmFactory.ConstructAll(
            _mockConfig.Object,
            _mockLogger.Object,
            invalidCompilerPath,
            invalidLibraryPath,
            _testIndexPath);

        // Assert
        Assert.Empty(factories);
    }

    [Fact]
    public async Task CompileRealmAsync_WithInvalidCompilerPath_ReturnsInfError()
    {
        // Arrange
        var factory = new Inform6RealmFactory(
            _mockConfig.Object,
            _mockLogger.Object,
            "Inform 6",
            "e:\\nonexistent\\inform6.exe",
            _testLibraryPath,
            _testIndexPath);

        var tempSource = Path.GetTempFileName();
        var tempOutput = Path.ChangeExtension(Path.GetTempFileName(), ".ulx");

        try
        {
            File.WriteAllText(tempSource, factory.GetInitialSourceText("Test", "Test"));

            // Stage as assets dictionary using new API
            var fileName = Path.GetFileName(tempSource);
            var assets = new Dictionary<string, byte[]>
            {
                [fileName] = await File.ReadAllBytesAsync(tempSource)
            };

            // Act
            var result = await factory.CompileRealmAsync("TestRealm", assets, fileName, tempOutput);

            // Assert
            Assert.Equal(RealmEditingOutcome.InfError, result);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempSource)) File.Delete(tempSource);
            if (File.Exists(tempOutput)) File.Delete(tempOutput);
        }
    }
}
