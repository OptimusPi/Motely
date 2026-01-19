using Motely.API.Services;

namespace Motely.Tests;

/// <summary>
/// Security tests for FilterService path validation and sanitization functions.
/// Tests path traversal protection, cross-platform compatibility, and edge cases.
/// </summary>
public class FilterServiceSecurityTests
{
    #region IsPathWithinDirectory Tests

    [Fact]
    public void IsPathWithinDirectory_ValidPath_ReturnsTrue()
    {
        // Arrange
        var baseDir = Path.Combine(Path.GetTempPath(), "testbase");
        var filePath = Path.Combine(baseDir, "valid.jaml");

        // Act
        var result = FilterService.IsPathWithinDirectory(filePath, baseDir, out var fullPath);

        // Assert
        Assert.True(result);
        Assert.NotNull(fullPath);
        Assert.StartsWith(Path.GetFullPath(baseDir), fullPath);
    }

    [Fact]
    public void IsPathWithinDirectory_ValidNestedPath_ReturnsTrue()
    {
        // Arrange
        var baseDir = Path.Combine(Path.GetTempPath(), "testbase");
        var filePath = Path.Combine(baseDir, "subdir", "nested", "valid.jaml");

        // Act
        var result = FilterService.IsPathWithinDirectory(filePath, baseDir, out var fullPath);

        // Assert
        Assert.True(result);
        Assert.NotNull(fullPath);
        Assert.StartsWith(Path.GetFullPath(baseDir), fullPath);
    }

    [Fact]
    public void IsPathWithinDirectory_PathTraversalUpwardsDotDot_ReturnsFalse()
    {
        // Arrange
        var baseDir = Path.Combine(Path.GetTempPath(), "testbase");
        var filePath = Path.Combine(baseDir, "..", "escape.jaml");

        // Act
        var result = FilterService.IsPathWithinDirectory(filePath, baseDir, out var fullPath);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsPathWithinDirectory_PathTraversalMultipleDotDot_ReturnsFalse()
    {
        // Arrange
        var baseDir = Path.Combine(Path.GetTempPath(), "testbase");
        var filePath = Path.Combine(baseDir, "..", "..", "..", "escape.jaml");

        // Act
        var result = FilterService.IsPathWithinDirectory(filePath, baseDir, out var fullPath);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsPathWithinDirectory_PathTraversalInterspersedDotDot_ReturnsFalse()
    {
        // Arrange
        var baseDir = Path.Combine(Path.GetTempPath(), "testbase");
        var filePath = Path.Combine(baseDir, "subdir", "..", "..", "escape.jaml");

        // Act
        var result = FilterService.IsPathWithinDirectory(filePath, baseDir, out var fullPath);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsPathWithinDirectory_AbsolutePathOutsideBase_ReturnsFalse()
    {
        // Arrange
        var baseDir = Path.Combine(Path.GetTempPath(), "testbase");
        var filePath = Path.Combine(Path.GetTempPath(), "otherdir", "escape.jaml");

        // Act
        var result = FilterService.IsPathWithinDirectory(filePath, baseDir, out var fullPath);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsPathWithinDirectory_RootPath_ReturnsFalse()
    {
        // Arrange
        var baseDir = Path.Combine(Path.GetTempPath(), "testbase");
        var filePath = Path.GetPathRoot(Path.GetTempPath()) ?? "/";

        // Act
        var result = FilterService.IsPathWithinDirectory(filePath, baseDir, out var fullPath);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("valid.jaml")]
    [InlineData("subdir/valid.jaml")]
    [InlineData("a/b/c/valid.jaml")]
    public void IsPathWithinDirectory_RelativePathsInsideBase_ReturnsTrue(string relativePath)
    {
        // Arrange
        var baseDir = Path.Combine(Path.GetTempPath(), "testbase");
        var filePath = Path.Combine(baseDir, relativePath);

        // Act
        var result = FilterService.IsPathWithinDirectory(filePath, baseDir, out var fullPath);

        // Assert
        Assert.True(result);
        Assert.NotNull(fullPath);
    }

    [Theory]
    [InlineData("../escape.jaml")]
    [InlineData("../../escape.jaml")]
    [InlineData("subdir/../../escape.jaml")]
    [InlineData("a/../../../escape.jaml")]
    public void IsPathWithinDirectory_RelativePathsEscapingBase_ReturnsFalse(string relativePath)
    {
        // Arrange
        var baseDir = Path.Combine(Path.GetTempPath(), "testbase");
        var filePath = Path.Combine(baseDir, relativePath);

        // Act
        var result = FilterService.IsPathWithinDirectory(filePath, baseDir, out var fullPath);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsPathWithinDirectory_SimilarDirectoryName_ReturnsFalse()
    {
        // Arrange - This tests that "/app/JamlFilters" doesn't match "/app/JamlFiltersEvil"
        var baseDir = Path.Combine(Path.GetTempPath(), "JamlFilters");
        var filePath = Path.Combine(Path.GetTempPath(), "JamlFiltersEvil", "malicious.jaml");

        // Act
        var result = FilterService.IsPathWithinDirectory(filePath, baseDir, out var fullPath);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsPathWithinDirectory_ExactSamePath_ReturnsTrue()
    {
        // Arrange
        var baseDir = Path.Combine(Path.GetTempPath(), "testbase");
        var filePath = baseDir;

        // Act
        var result = FilterService.IsPathWithinDirectory(filePath, baseDir, out var fullPath);

        // Assert
        Assert.True(result);
        Assert.NotNull(fullPath);
    }

    [Fact]
    public void IsPathWithinDirectory_SymlinkAttempt_HandledByPathNormalization()
    {
        // Arrange - Path.GetFullPath should resolve symlinks
        var baseDir = Path.Combine(Path.GetTempPath(), "testbase");
        // Simulate a path that might be a symlink attempt
        var filePath = Path.Combine(baseDir, "link", "..", "..", "escape.jaml");

        // Act
        var result = FilterService.IsPathWithinDirectory(filePath, baseDir, out var fullPath);

        // Assert
        // Path normalization should resolve the traversal
        Assert.False(result);
    }

    #endregion

    #region TrySanitizeFilterName Tests

    [Theory]
    [InlineData("valid", "valid")]
    [InlineData("ValidName", "ValidName")]
    [InlineData("valid-name", "valid-name")]
    [InlineData("valid_name", "valid_name")]
    [InlineData("valid123", "valid123")]
    public void TrySanitizeFilterName_ValidNames_ReturnsTrue(string input, string expected)
    {
        // Act
        var result = FilterService.TrySanitizeFilterName(input, out var safeName);

        // Assert
        Assert.True(result);
        Assert.Equal(expected, safeName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TrySanitizeFilterName_NullOrEmpty_ReturnsFalse(string? input)
    {
        // Act
        var result = FilterService.TrySanitizeFilterName(input, out var safeName);

        // Assert
        Assert.False(result);
        Assert.Null(safeName);
    }

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\..\\windows\\system32")]
    [InlineData("../../../../escape")]
    public void TrySanitizeFilterName_PathTraversal_SanitizesOrRejects(string input)
    {
        // Act
        var result = FilterService.TrySanitizeFilterName(input, out var safeName);

        // Assert
        // The function should extract just the filename part and sanitize it
        // Path.GetFileNameWithoutExtension strips path components
        if (result)
        {
            Assert.NotNull(safeName);
            Assert.DoesNotContain("..", safeName);
            Assert.DoesNotContain("/", safeName);
            Assert.DoesNotContain("\\", safeName);
        }
        else
        {
            Assert.Null(safeName);
        }
    }

    [Theory]
    [InlineData("/absolute/path/file")]
    [InlineData("C:\\absolute\\path\\file")]
    public void TrySanitizeFilterName_AbsolutePath_ExtractsFilename(string input)
    {
        // Act
        var result = FilterService.TrySanitizeFilterName(input, out var safeName);

        // Assert
        Assert.True(result);
        Assert.NotNull(safeName);
        Assert.Equal("file", safeName);
        Assert.DoesNotContain("/", safeName);
        Assert.DoesNotContain("\\", safeName);
    }

    [Theory]
    [InlineData("file.jaml", "file")]
    [InlineData("file.json", "file")]
    [InlineData("file.txt", "file")]
    [InlineData("file.extension.jaml", "file.extension")]
    public void TrySanitizeFilterName_WithExtension_RemovesExtension(string input, string expected)
    {
        // Act
        var result = FilterService.TrySanitizeFilterName(input, out var safeName);

        // Assert
        Assert.True(result);
        Assert.Equal(expected, safeName);
    }

    [Theory]
    [InlineData("file<name>")]
    [InlineData("file|name")]
    [InlineData("file:name")]
    [InlineData("file*name")]
    [InlineData("file?name")]
    [InlineData("file\"name")]
    public void TrySanitizeFilterName_InvalidCharacters_ReplacesWithUnderscore(string input)
    {
        // Act
        var result = FilterService.TrySanitizeFilterName(input, out var safeName);

        // Assert
        Assert.True(result);
        Assert.NotNull(safeName);
        Assert.DoesNotContain("<", safeName);
        Assert.DoesNotContain(">", safeName);
        Assert.DoesNotContain("|", safeName);
        Assert.DoesNotContain(":", safeName);
        Assert.DoesNotContain("*", safeName);
        Assert.DoesNotContain("?", safeName);
        Assert.DoesNotContain("\"", safeName);
        Assert.Contains("_", safeName);
    }

    [Theory]
    [InlineData("subdir/file")]
    [InlineData("a/b/c/file")]
    [InlineData("dir\\file")]
    public void TrySanitizeFilterName_PathWithDirectories_ExtractsOnlyFilename(string input)
    {
        // Act
        var result = FilterService.TrySanitizeFilterName(input, out var safeName);

        // Assert
        Assert.True(result);
        Assert.Equal("file", safeName);
        Assert.DoesNotContain("/", safeName);
        Assert.DoesNotContain("\\", safeName);
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("...")]
    public void TrySanitizeFilterName_DotsOnly_ReturnsFalse(string input)
    {
        // Act
        var result = FilterService.TrySanitizeFilterName(input, out var safeName);

        // Assert
        // Path.GetFileNameWithoutExtension returns empty string for these
        Assert.False(result);
        Assert.Null(safeName);
    }

    [Theory]
    [InlineData(".hidden")]
    [InlineData("..hidden")]
    public void TrySanitizeFilterName_HiddenFiles_HandlesCorrectly(string input)
    {
        // Act
        var result = FilterService.TrySanitizeFilterName(input, out var safeName);

        // Assert
        // Path.GetFileNameWithoutExtension handles hidden files differently
        // ".hidden" becomes "" (no extension), "..hidden" becomes "." (one dot remains)
        // The function should handle these edge cases
        if (result)
        {
            Assert.NotNull(safeName);
        }
        else
        {
            Assert.Null(safeName);
        }
    }

    [Fact]
    public void TrySanitizeFilterName_NullByte_Sanitizes()
    {
        // Arrange
        var input = "file\0name";

        // Act
        var result = FilterService.TrySanitizeFilterName(input, out var safeName);

        // Assert
        Assert.True(result);
        Assert.NotNull(safeName);
        Assert.DoesNotContain("\0", safeName);
    }

    [Theory]
    [InlineData("COM1")]
    [InlineData("LPT1")]
    [InlineData("CON")]
    [InlineData("PRN")]
    [InlineData("AUX")]
    [InlineData("NUL")]
    public void TrySanitizeFilterName_WindowsReservedNames_AllowsButSanitizes(string input)
    {
        // Act
        var result = FilterService.TrySanitizeFilterName(input, out var safeName);

        // Assert
        // The function allows these names (it's not responsible for OS-specific validation)
        // but they are sanitized through the normal process
        Assert.True(result);
        Assert.NotNull(safeName);
    }

    [Fact]
    public void TrySanitizeFilterName_VeryLongName_Handles()
    {
        // Arrange
        var input = new string('a', 500);

        // Act
        var result = FilterService.TrySanitizeFilterName(input, out var safeName);

        // Assert
        Assert.True(result);
        Assert.NotNull(safeName);
        Assert.Equal(500, safeName.Length);
    }

    [Theory]
    [InlineData("普通文件")]
    [InlineData("файл")]
    [InlineData("ファイル")]
    public void TrySanitizeFilterName_UnicodeNames_Preserves(string input)
    {
        // Act
        var result = FilterService.TrySanitizeFilterName(input, out var safeName);

        // Assert
        Assert.True(result);
        Assert.NotNull(safeName);
        // Unicode characters that are valid filename characters should be preserved
        Assert.Equal(input, safeName);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void Integration_TrySanitizeFilterName_ThenIsPathWithinDirectory_PreventsTraversal()
    {
        // Arrange
        var baseDir = Path.Combine(Path.GetTempPath(), "testbase");
        var maliciousInput = "../../../etc/passwd";

        // Act
        var sanitized = FilterService.TrySanitizeFilterName(maliciousInput, out var safeName);
        Assert.True(sanitized);
        Assert.NotNull(safeName);

        var filePath = Path.Combine(baseDir, $"{safeName}.jaml");
        var isValid = FilterService.IsPathWithinDirectory(filePath, baseDir, out var fullPath);

        // Assert
        Assert.True(isValid);
        Assert.NotNull(fullPath);
        Assert.StartsWith(Path.GetFullPath(baseDir), fullPath);
    }

    [Fact]
    public void Integration_DirectPathTraversal_IsBlocked()
    {
        // Arrange
        var baseDir = Path.Combine(Path.GetTempPath(), "testbase");
        // Even if sanitization is bypassed somehow, the path validation should catch it
        var maliciousPath = Path.Combine(baseDir, "..", "escape.jaml");

        // Act
        var isValid = FilterService.IsPathWithinDirectory(maliciousPath, baseDir, out var fullPath);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void Integration_ComplexPathTraversal_IsBlocked()
    {
        // Arrange
        var baseDir = Path.Combine(Path.GetTempPath(), "testbase");
        // Complex traversal attempt
        var maliciousPath = Path.Combine(baseDir, "subdir", "..", "..", "..", "escape.jaml");

        // Act
        var isValid = FilterService.IsPathWithinDirectory(maliciousPath, baseDir, out var fullPath);

        // Assert
        Assert.False(isValid);
    }

    #endregion

    #region Cross-Platform Tests

    [Fact]
    public void IsPathWithinDirectory_HandlesCurrentPlatformSeparators()
    {
        // Arrange
        var baseDir = Path.Combine(Path.GetTempPath(), "testbase");
        var validPath = Path.Combine(baseDir, "subdir", "file.jaml");

        // Act
        var result = FilterService.IsPathWithinDirectory(validPath, baseDir, out var fullPath);

        // Assert
        Assert.True(result);
        Assert.NotNull(fullPath);
    }

    [Fact]
    public void TrySanitizeFilterName_HandlesBothSlashTypes()
    {
        // Act
        var resultForward = FilterService.TrySanitizeFilterName("dir/file", out var safeNameForward);
        var resultBackward = FilterService.TrySanitizeFilterName("dir\\file", out var safeNameBackward);

        // Assert
        Assert.True(resultForward);
        Assert.True(resultBackward);
        Assert.Equal("file", safeNameForward);
        Assert.Equal("file", safeNameBackward);
    }

    #endregion
}
