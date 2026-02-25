using FileShare.Infrastructure.FileSystem;

namespace FileShare.Tests.Infrastructure.FileSystem;

public sealed class FileStateTrackerTests
{
    [Fact]
    public void TryAdd_NewFile_ReturnsTrue()
    {
        // Arrange
        var tracker = new FileStateTracker();

        // Act
        var result = tracker.TryAdd("alpha.txt");

        // Assert
        Assert.True(result);
        Assert.Contains("alpha.txt", tracker.CurrentFiles);
    }

    [Fact]
    public void TryAdd_DuplicateFile_ReturnsFalse()
    {
        // Arrange
        var tracker = new FileStateTracker();
        tracker.TryAdd("alpha.txt");

        // Act
        var result = tracker.TryAdd("alpha.txt");

        // Assert
        Assert.False(result);
        Assert.Single(tracker.CurrentFiles);
    }

    [Fact]
    public void TryAdd_IsCaseInsensitive()
    {
        // Arrange
        var tracker = new FileStateTracker();
        tracker.TryAdd("Alpha.TXT");

        // Act
        var result = tracker.TryAdd("alpha.txt");

        // Assert
        Assert.False(result);
        Assert.Single(tracker.CurrentFiles);
    }

    [Fact]
    public void TryRemove_ExistingFile_ReturnsTrue()
    {
        // Arrange
        var tracker = new FileStateTracker();
        tracker.TryAdd("alpha.txt");

        // Act
        var result = tracker.TryRemove("alpha.txt");

        // Assert
        Assert.True(result);
        Assert.Empty(tracker.CurrentFiles);
    }

    [Fact]
    public void TryRemove_NonExistingFile_ReturnsFalse()
    {
        // Arrange
        var tracker = new FileStateTracker();

        // Act
        var result = tracker.TryRemove("nonexistent.txt");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void TryRemove_IsCaseInsensitive()
    {
        // Arrange
        var tracker = new FileStateTracker();
        tracker.TryAdd("Alpha.TXT");

        // Act
        var result = tracker.TryRemove("alpha.txt");

        // Assert
        Assert.True(result);
        Assert.Empty(tracker.CurrentFiles);
    }

    [Fact]
    public void Initialize_SetsCurrentFiles()
    {
        // Arrange
        var tracker = new FileStateTracker();

        // Act
        tracker.Initialize(["alpha.txt", "beta.pdf"]);

        // Assert
        Assert.Equal(2, tracker.CurrentFiles.Count);
        Assert.Contains("alpha.txt", tracker.CurrentFiles);
        Assert.Contains("beta.pdf", tracker.CurrentFiles);
    }

    [Fact]
    public void Initialize_ClearsPreviousFiles()
    {
        // Arrange
        var tracker = new FileStateTracker();
        tracker.TryAdd("old.txt");

        // Act
        tracker.Initialize(["new.txt"]);

        // Assert
        Assert.Single(tracker.CurrentFiles);
        Assert.Contains("new.txt", tracker.CurrentFiles);
        Assert.DoesNotContain("old.txt", tracker.CurrentFiles);
    }

    [Fact]
    public void Initialize_EmptyList_ClearsAllFiles()
    {
        // Arrange
        var tracker = new FileStateTracker();
        tracker.TryAdd("alpha.txt");

        // Act
        tracker.Initialize([]);

        // Assert
        Assert.Empty(tracker.CurrentFiles);
    }
}
