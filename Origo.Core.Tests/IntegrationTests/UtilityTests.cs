using System;
using System.IO;
using System.Linq;
using Origo.Core.Abstractions;
using Origo.Core.Abstractions.Logging;
using Origo.Core.Logging;
using Origo.Core.Save;
using Origo.Core.Utils;
using Origo.Core.Utils.DataStructures;
using Xunit;

namespace Origo.Core.Tests;

// ── KeyValueFileParser ─────────────────────────────────────────────────

public class KeyValueFileParserTests
{
    [Fact]
    public void KeyValueFileParser_Parse_BasicKeyValue()
    {
        var logger = new TestLogger();
        var result = KeyValueFileParser.Parse("key1: value1\nkey2: value2", "test", false, logger);
        Assert.Equal(2, result.Count);
        Assert.Equal("value1", result["key1"]);
        Assert.Equal("value2", result["key2"]);
    }

    [Fact]
    public void KeyValueFileParser_Parse_SkipsCommentsAndBlanks()
    {
        var logger = new TestLogger();
        var content = "# comment\n\nkey1: value1\n# another comment\nkey2: value2";
        var result = KeyValueFileParser.Parse(content, "test", false, logger);
        Assert.Equal(2, result.Count);
        Assert.Equal("value1", result["key1"]);
    }

    [Fact]
    public void KeyValueFileParser_Parse_EmptyContent_ReturnsEmpty()
    {
        var logger = new TestLogger();
        var result = KeyValueFileParser.Parse("", "test", false, logger);
        Assert.Empty(result);
    }

    [Fact]
    public void KeyValueFileParser_Parse_NullContent_ReturnsEmpty()
    {
        var logger = new TestLogger();
        var result = KeyValueFileParser.Parse(null, "test", false, logger);
        Assert.Empty(result);
    }

    [Fact]
    public void KeyValueFileParser_Parse_StrictMode_ThrowsOnInvalidLine()
    {
        var logger = new TestLogger();
        Assert.Throws<FormatException>(() =>
            KeyValueFileParser.Parse("invalid_line_no_colon", "test", true, logger));
    }

    [Fact]
    public void KeyValueFileParser_Parse_LenientMode_LogsWarningOnInvalidLine()
    {
        var logger = new TestLogger();
        var result = KeyValueFileParser.Parse("invalid_line_no_colon", "test", false, logger);
        Assert.Empty(result);
        Assert.NotEmpty(logger.Warnings);
    }

    [Fact]
    public void KeyValueFileParser_Parse_StrictMode_ThrowsOnEmptyKeyOrValue()
    {
        var logger = new TestLogger();
        Assert.Throws<FormatException>(() =>
            KeyValueFileParser.Parse(": value", "test", true, logger));
    }

    [Fact]
    public void KeyValueFileParser_Parse_LenientMode_LogsWarningOnEmptyKey()
    {
        var logger = new TestLogger();
        var result = KeyValueFileParser.Parse(": value", "test", false, logger);
        Assert.Empty(result);
        Assert.NotEmpty(logger.Warnings);
    }

    [Fact]
    public void KeyValueFileParser_Parse_DuplicateKey_LogsWarning()
    {
        var logger = new TestLogger();
        var result = KeyValueFileParser.Parse("key: v1\nkey: v2", "test", false, logger);
        Assert.Equal("v2", result["key"]);
        Assert.NotEmpty(logger.Warnings);
    }

    [Fact]
    public void KeyValueFileParser_Parse_ValueContainsColon_PreservesFullValue()
    {
        var logger = new TestLogger();
        var result = KeyValueFileParser.Parse("url: http://example.com:8080", "test", false, logger);
        Assert.Equal("http://example.com:8080", result["url"]);
    }

    [Fact]
    public void KeyValueFileParser_Parse_ThrowsOnNullLogger()
    {
        Assert.Throws<ArgumentNullException>(() =>
            KeyValueFileParser.Parse("key: value", "test", false, null!));
    }
}

// ── ConcurrentActionQueue ──────────────────────────────────────────────

public class ConcurrentActionQueueTests
{
    [Fact]
    public void ConcurrentActionQueue_Enqueue_IncreasesCount()
    {
        var queue = new ConcurrentActionQueue(new TestLogger());
        queue.Enqueue(() => { });
        queue.Enqueue(() => { });
        Assert.Equal(2, queue.Count);
    }

    [Fact]
    public void ConcurrentActionQueue_ExecuteAll_RunsAllActions()
    {
        var queue = new ConcurrentActionQueue(new TestLogger());
        var callCount = 0;
        queue.Enqueue(() => callCount++);
        queue.Enqueue(() => callCount++);
        queue.Enqueue(() => callCount++);
        var executed = queue.ExecuteAll();
        Assert.Equal(3, executed);
        Assert.Equal(3, callCount);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void ConcurrentActionQueue_ExecuteAll_EmptyQueue_ReturnsZero()
    {
        var queue = new ConcurrentActionQueue(new TestLogger());
        Assert.Equal(0, queue.ExecuteAll());
    }

    [Fact]
    public void ConcurrentActionQueue_Clear_EmptiesQueue()
    {
        var queue = new ConcurrentActionQueue(new TestLogger());
        queue.Enqueue(() => { });
        queue.Enqueue(() => { });
        queue.Clear();
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void ConcurrentActionQueue_ExecuteAll_ActionThatReenqueues()
    {
        var queue = new ConcurrentActionQueue(new TestLogger());
        var executed = false;
        queue.Enqueue(() => queue.Enqueue(() => executed = true));
        var count = queue.ExecuteAll();
        Assert.Equal(2, count);
        Assert.True(executed);
    }

    [Fact]
    public void ConcurrentActionQueue_Enqueue_ThrowsOnNull()
    {
        var queue = new ConcurrentActionQueue(new TestLogger());
        Assert.Throws<ArgumentNullException>(() => queue.Enqueue(null!));
    }

    [Fact]
    public void ConcurrentActionQueue_Constructor_ThrowsOnNullLogger() =>
        Assert.Throws<ArgumentNullException>(() => new ConcurrentActionQueue(null!));

    [Fact]
    public void ConcurrentActionQueue_ExecuteAll_PropagatesException()
    {
        var queue = new ConcurrentActionQueue(new TestLogger());
        queue.Enqueue(() => throw new InvalidOperationException("boom"));
        Assert.Throws<InvalidOperationException>(() => queue.ExecuteAll());
    }
}

// ── LogMessageBuilder ──────────────────────────────────────────────────

public class LogMessageBuilderTests
{
    [Fact]
    public void LogMessageBuilder_Build_PlainMessage()
    {
        var msg = new LogMessageBuilder().Build("hello");
        Assert.Equal("hello", msg);
    }

    [Fact]
    public void LogMessageBuilder_SetElapsedMs_IncludesTimestamp()
    {
        var msg = new LogMessageBuilder().SetElapsedMs(12.345).Build("test");
        Assert.StartsWith("[+12.34ms]", msg);
        Assert.Contains("test", msg);
    }

    [Fact]
    public void LogMessageBuilder_AddPrefix_IncludesPrefix()
    {
        var msg = new LogMessageBuilder().AddPrefix("ctx", "val").Build("test");
        Assert.Contains("ctx=val", msg);
        Assert.Contains(" | test", msg);
    }

    [Fact]
    public void LogMessageBuilder_AddSuffix_IncludesSuffix()
    {
        var msg = new LogMessageBuilder().AddSuffix("key", "val").Build("test");
        Assert.Contains("test | key=val", msg);
    }

    [Fact]
    public void LogMessageBuilder_AddPrefix_NullKey_Skipped()
    {
        var msg = new LogMessageBuilder().AddPrefix(null!, "val").Build("test");
        Assert.Equal("test", msg);
    }

    [Fact]
    public void LogMessageBuilder_AddPrefix_NullValue_Skipped()
    {
        var msg = new LogMessageBuilder().AddPrefix("key", null).Build("test");
        Assert.Equal("test", msg);
    }

    [Fact]
    public void LogMessageBuilder_AddSuffix_WhitespaceKey_Skipped()
    {
        var msg = new LogMessageBuilder().AddSuffix("  ", "val").Build("test");
        Assert.Equal("test", msg);
    }

    [Fact]
    public void LogMessageBuilder_CombinedPrefixSuffix()
    {
        var msg = new LogMessageBuilder()
            .SetElapsedMs(1.0)
            .AddPrefix("p", "1")
            .AddSuffix("s", "2")
            .Build("msg");
        Assert.Contains("[+1ms]", msg);
        Assert.Contains("p=1 | msg", msg);
        Assert.Contains("msg | s=2", msg);
    }
}

// ── NullLogger ─────────────────────────────────────────────────────────

public class NullLoggerTests
{
    [Fact]
    public void NullLogger_Instance_IsSingleton() => Assert.Same(NullLogger.Instance, NullLogger.Instance);

    [Fact]
    public void NullLogger_Log_DoesNotThrow()
    {
        var ex = Record.Exception(() =>
        {
            NullLogger.Instance.Log(LogLevel.Info, "tag", "message");
            NullLogger.Instance.Log(LogLevel.Warning, "tag", "message");
            NullLogger.Instance.Log(LogLevel.Error, "tag", "message");
        });
        Assert.Null(ex);
    }

    [Fact]
    public void NullLogger_ImplementsILogger()
    {
        ILogger logger = NullLogger.Instance;
        Assert.NotNull(logger);
    }
}

// ── WellKnownKeys ──────────────────────────────────────────────────────

public class WellKnownKeysTests
{
    [Fact]
    public void WellKnownKeys_ActiveSaveId_HasExpectedValue() =>
        Assert.Equal("origo.active_save_id", WellKnownKeys.ActiveSaveId);

    [Fact]
    public void WellKnownKeys_SessionTopology_HasExpectedValue() =>
        Assert.Equal("origo.session_topology", WellKnownKeys.SessionTopology);
}

// ── TestFileSystem coverage ────────────────────────────────────────────

public class TestFileSystemAdditionalTests
{
    [Fact]
    public void TestFileSystem_WriteAllText_And_ReadAllText()
    {
        var fs = new TestFileSystem();
        fs.WriteAllText("root/file.txt", "content", false);
        Assert.Equal("content", fs.ReadAllText("root/file.txt"));
    }

    [Fact]
    public void TestFileSystem_WriteAllText_Overwrite()
    {
        var fs = new TestFileSystem();
        fs.WriteAllText("file.txt", "v1", false);
        fs.WriteAllText("file.txt", "v2", true);
        Assert.Equal("v2", fs.ReadAllText("file.txt"));
    }

    [Fact]
    public void TestFileSystem_WriteAllText_NoOverwrite_Throws()
    {
        var fs = new TestFileSystem();
        fs.WriteAllText("file.txt", "v1", false);
        Assert.Throws<IOException>(() => fs.WriteAllText("file.txt", "v2", false));
    }

    [Fact]
    public void TestFileSystem_Delete_RemovesFile()
    {
        var fs = new TestFileSystem();
        fs.SeedFile("file.txt", "data");
        Assert.True(fs.Exists("file.txt"));
        fs.Delete("file.txt");
        Assert.False(fs.Exists("file.txt"));
    }

    [Fact]
    public void TestFileSystem_CombinePath_CombinesCorrectly()
    {
        var fs = new TestFileSystem();
        Assert.Equal("root/sub/file.txt", fs.CombinePath("root/sub", "file.txt"));
    }

    [Fact]
    public void TestFileSystem_GetParentDirectory()
    {
        var fs = new TestFileSystem();
        Assert.Equal("root/sub", fs.GetParentDirectory("root/sub/file.txt"));
    }

    [Fact]
    public void TestFileSystem_EnumerateDirectories()
    {
        var fs = new TestFileSystem();
        fs.SeedFile("root/a/file1.txt", "");
        fs.SeedFile("root/b/file2.txt", "");

        var dirs = fs.EnumerateDirectories("root").ToList();
        Assert.Contains("root/a", dirs);
        Assert.Contains("root/b", dirs);
    }

    [Fact]
    public void TestFileSystem_Rename_MovesAllFilesAndDirectories()
    {
        var fs = new TestFileSystem();
        fs.SeedFile("src/a.txt", "hello");
        fs.SeedFile("src/sub/b.txt", "world");

        fs.Rename("src", "dst");

        Assert.False(fs.Exists("src/a.txt"));
        Assert.False(fs.Exists("src/sub/b.txt"));
        Assert.True(fs.Exists("dst/a.txt"));
        Assert.True(fs.Exists("dst/sub/b.txt"));
        Assert.Equal("hello", fs.ReadAllText("dst/a.txt"));
        Assert.Equal("world", fs.ReadAllText("dst/sub/b.txt"));
    }

    [Fact]
    public void TestFileSystem_DeleteDirectory_RemovesAllContents()
    {
        var fs = new TestFileSystem();
        fs.SeedFile("dir/a.txt", "1");
        fs.SeedFile("dir/sub/b.txt", "2");
        fs.CreateDirectory("dir/empty");

        Assert.True(fs.DirectoryExists("dir"));

        fs.DeleteDirectory("dir");

        Assert.False(fs.Exists("dir/a.txt"));
        Assert.False(fs.Exists("dir/sub/b.txt"));
        Assert.False(fs.DirectoryExists("dir"));
        Assert.False(fs.DirectoryExists("dir/empty"));
    }
}
