using System;
using System.Collections.Generic;
using System.Linq;
using Origo.Core.Abstractions;
using Origo.Core.Logging;
using Origo.Core.Random;
using Origo.Core.Runtime;
using Origo.Core.Runtime.Console;
using Origo.Core.Save;
using Origo.Core.Serialization;
using Origo.Core.Snd;
using Origo.Core.Utils;
using Origo.Core.Utils.DataStructures;
using Xunit;


namespace Origo.Core.Tests;

// ── SavePathLayout ─────────────────────────────────────────────────────

public class SavePathLayoutTests
{
    [Fact]
    public void SavePathLayout_GetCurrentDirectory_ReturnsCurrent()
    {
        Assert.Equal("current", SavePathLayout.GetCurrentDirectory());
    }

    [Fact]
    public void SavePathLayout_CurrentDirectoryName_Constant()
    {
        Assert.Equal("current", SavePathLayout.CurrentDirectoryName);
    }

    [Theory]
    [InlineData("001", "save_001")]
    [InlineData("abc", "save_abc")]
    [InlineData("my-save", "save_my-save")]
    public void SavePathLayout_GetSaveDirectory_FormatsCorrectly(string saveId, string expected)
    {
        Assert.Equal(expected, SavePathLayout.GetSaveDirectory(saveId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SavePathLayout_GetSaveDirectory_ThrowsOnInvalidId(string? saveId)
    {
        Assert.Throws<ArgumentException>(() => SavePathLayout.GetSaveDirectory(saveId!));
    }

    [Fact]
    public void SavePathLayout_GetProgressFile_CombinesCorrectly()
    {
        Assert.Equal("mybase/progress.json", SavePathLayout.GetProgressFile("mybase"));
    }

    [Fact]
    public void SavePathLayout_GetProgressFile_ThrowsOnEmpty()
    {
        Assert.Throws<ArgumentException>(() => SavePathLayout.GetProgressFile(""));
    }

    [Fact]
    public void SavePathLayout_GetProgressStateMachinesFile_CombinesCorrectly()
    {
        Assert.Equal("base/progress_state_machines.json", SavePathLayout.GetProgressStateMachinesFile("base"));
    }

    [Fact]
    public void SavePathLayout_GetProgressStateMachinesFile_ThrowsOnWhitespace()
    {
        Assert.Throws<ArgumentException>(() => SavePathLayout.GetProgressStateMachinesFile("  "));
    }

    [Fact]
    public void SavePathLayout_GetCustomMetaFile_CombinesCorrectly()
    {
        Assert.Equal("base/meta.map", SavePathLayout.GetCustomMetaFile("base"));
    }

    [Fact]
    public void SavePathLayout_GetCustomMetaFile_ThrowsOnNull()
    {
        Assert.Throws<ArgumentException>(() => SavePathLayout.GetCustomMetaFile(null!));
    }

    [Fact]
    public void SavePathLayout_GetLevelDirectory_CombinesCorrectly()
    {
        Assert.Equal("base/level_town", SavePathLayout.GetLevelDirectory("base", "town"));
    }

    [Theory]
    [InlineData("", "level1")]
    [InlineData("base", "")]
    [InlineData("", "")]
    public void SavePathLayout_GetLevelDirectory_ThrowsOnInvalidArgs(string baseDir, string levelId)
    {
        Assert.Throws<ArgumentException>(() => SavePathLayout.GetLevelDirectory(baseDir, levelId));
    }

    [Fact]
    public void SavePathLayout_GetLevelSndSceneFile_CombinesCorrectly()
    {
        Assert.Equal("level_dir/snd_scene.json", SavePathLayout.GetLevelSndSceneFile("level_dir"));
    }

    [Fact]
    public void SavePathLayout_GetLevelSndSceneFile_ThrowsOnEmpty()
    {
        Assert.Throws<ArgumentException>(() => SavePathLayout.GetLevelSndSceneFile(""));
    }

    [Fact]
    public void SavePathLayout_GetLevelSessionFile_CombinesCorrectly()
    {
        Assert.Equal("level_dir/session.json", SavePathLayout.GetLevelSessionFile("level_dir"));
    }

    [Fact]
    public void SavePathLayout_GetLevelSessionFile_ThrowsOnWhitespace()
    {
        Assert.Throws<ArgumentException>(() => SavePathLayout.GetLevelSessionFile("   "));
    }

    [Fact]
    public void SavePathLayout_GetLevelSessionStateMachinesFile_CombinesCorrectly()
    {
        Assert.Equal("level_dir/session_state_machines.json",
            SavePathLayout.GetLevelSessionStateMachinesFile("level_dir"));
    }

    [Fact]
    public void SavePathLayout_GetLevelSessionStateMachinesFile_ThrowsOnNull()
    {
        Assert.Throws<ArgumentException>(() => SavePathLayout.GetLevelSessionStateMachinesFile(null!));
    }

    [Fact]
    public void SavePathLayout_GetWriteInProgressMarker_CombinesCorrectly()
    {
        Assert.Equal("base/.write_in_progress", SavePathLayout.GetWriteInProgressMarker("base"));
    }

    [Fact]
    public void SavePathLayout_GetWriteInProgressMarker_ThrowsOnEmpty()
    {
        Assert.Throws<ArgumentException>(() => SavePathLayout.GetWriteInProgressMarker(""));
    }

    [Fact]
    public void SavePathLayout_WriteInProgressMarkerName_Constant()
    {
        Assert.Equal(".write_in_progress", SavePathLayout.WriteInProgressMarkerName);
    }
}

// ── SavePathResolver ───────────────────────────────────────────────────

public class SavePathResolverTests
{
    [Fact]
    public void SavePathResolver_EnsureParentDirectory_CreatesParent()
    {
        var fs = new TestFileSystem();
        SavePathResolver.EnsureParentDirectory(fs, "root/sub/file.txt");
        Assert.True(fs.DirectoryExists("root/sub"));
    }

    [Fact]
    public void SavePathResolver_EnsureParentDirectory_NoOpForRootFile()
    {
        var fs = new TestFileSystem();
        // File at root has empty parent; should not throw
        SavePathResolver.EnsureParentDirectory(fs, "file.txt");
    }

    [Fact]
    public void SavePathResolver_GetRelativePath_ExtractsRelative()
    {
        var result = SavePathResolver.GetRelativePath("root/saves", "root/saves/file.json");
        Assert.Equal("file.json", result);
    }

    [Fact]
    public void SavePathResolver_GetRelativePath_NestedPath()
    {
        var result = SavePathResolver.GetRelativePath("root", "root/sub/file.json");
        Assert.Equal("sub/file.json", result);
    }

    [Fact]
    public void SavePathResolver_GetRelativePath_ExactMatch_ReturnsEmpty()
    {
        var result = SavePathResolver.GetRelativePath("root/saves", "root/saves");
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void SavePathResolver_GetRelativePath_NoMatch_ReturnsFullPath()
    {
        var result = SavePathResolver.GetRelativePath("root/a", "root/b/file.json");
        Assert.Equal("root/b/file.json", result);
    }

    [Fact]
    public void SavePathResolver_GetRelativePath_EmptyBase_ReturnsFullPath()
    {
        var result = SavePathResolver.GetRelativePath("", "root/file.json");
        Assert.Equal("root/file.json", result);
    }

    [Fact]
    public void SavePathResolver_GetLeafDirectoryName_ReturnsLastSegment()
    {
        Assert.Equal("child", SavePathResolver.GetLeafDirectoryName("root/parent/child"));
    }

    [Fact]
    public void SavePathResolver_GetLeafDirectoryName_SingleSegment()
    {
        Assert.Equal("single", SavePathResolver.GetLeafDirectoryName("single"));
    }

    [Fact]
    public void SavePathResolver_GetLeafDirectoryName_TrailingSlash()
    {
        Assert.Equal("child", SavePathResolver.GetLeafDirectoryName("root/child/"));
    }

    [Fact]
    public void SavePathResolver_GetLeafDirectoryName_EmptyOrWhitespace_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, SavePathResolver.GetLeafDirectoryName(""));
        Assert.Equal(string.Empty, SavePathResolver.GetLeafDirectoryName("  "));
    }

    [Fact]
    public void SavePathResolver_RejectPathTraversal_ThrowsOnDotDot()
    {
        Assert.Throws<ArgumentException>(() => SavePathResolver.RejectPathTraversal("../evil"));
        Assert.Throws<ArgumentException>(() => SavePathResolver.RejectPathTraversal("some/../evil"));
        Assert.Throws<ArgumentException>(() => SavePathResolver.RejectPathTraversal(".."));
        Assert.Throws<ArgumentException>(() => SavePathResolver.RejectPathTraversal("path/.."));
    }

    [Fact]
    public void SavePathResolver_RejectPathTraversal_AllowsSafePaths()
    {
        // Should not throw
        SavePathResolver.RejectPathTraversal("safe/path");
        SavePathResolver.RejectPathTraversal("file.json");
        SavePathResolver.RejectPathTraversal("");
    }
}

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
    public void ConcurrentActionQueue_Constructor_ThrowsOnNullLogger()
    {
        Assert.Throws<ArgumentNullException>(() => new ConcurrentActionQueue(null!));
    }

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

// ── BlackboardJsonSerializer ───────────────────────────────────────────

public class BlackboardJsonSerializerTests
{
    private static SndWorld CreateWorld()
    {
        return new SndWorld(new TypeStringMapping(), NullLogger.Instance);
    }

    [Fact]
    public void BlackboardJsonSerializer_RoundTrip_PreservesData()
    {
        var world = CreateWorld();
        var serializer = new BlackboardJsonSerializer(world.JsonOptions);
        var bb = new Origo.Core.Blackboard.Blackboard();
        bb.Set("intKey", 42);
        bb.Set("strKey", "hello");

        var json = serializer.Serialize(bb);
        var bb2 = new Origo.Core.Blackboard.Blackboard();
        serializer.DeserializeInto(bb2, json);

        Assert.Equal(42, bb2.TryGet<int>("intKey").value);
        Assert.Equal("hello", bb2.TryGet<string>("strKey").value);
    }

    [Fact]
    public void BlackboardJsonSerializer_Serialize_EmptyBlackboard_ReturnsValidJson()
    {
        var world = CreateWorld();
        var serializer = new BlackboardJsonSerializer(world.JsonOptions);
        var bb = new Origo.Core.Blackboard.Blackboard();

        var json = serializer.Serialize(bb);
        Assert.NotNull(json);
        Assert.Contains("{", json);
    }

    [Fact]
    public void BlackboardJsonSerializer_DeserializeInto_OverwritesExisting()
    {
        var world = CreateWorld();
        var serializer = new BlackboardJsonSerializer(world.JsonOptions);
        var bb = new Origo.Core.Blackboard.Blackboard();
        bb.Set("key1", "old");
        bb.Set("key2", "keep");

        var source = new Origo.Core.Blackboard.Blackboard();
        source.Set("key1", "new");
        var json = serializer.Serialize(source);
        serializer.DeserializeInto(bb, json);

        Assert.Equal("new", bb.TryGet<string>("key1").value);
        // DeserializeAll replaces all data
        Assert.False(bb.TryGet<string>("key2").found);
    }
}

// ── ConsoleCommandParser ───────────────────────────────────────────────

public class ConsoleCommandParserTests
{
    [Fact]
    public void ConsoleCommandParser_TryParse_EmptyLine_Fails()
    {
        var ok = ConsoleCommandParser.TryParse("", out var inv, out var err);
        Assert.False(ok);
        Assert.Null(inv);
        Assert.NotNull(err);
    }

    [Fact]
    public void ConsoleCommandParser_TryParse_WhitespaceLine_Fails()
    {
        var ok = ConsoleCommandParser.TryParse("   ", out _, out var err);
        Assert.False(ok);
        Assert.NotNull(err);
    }

    [Fact]
    public void ConsoleCommandParser_TryParse_SingleCommand()
    {
        var ok = ConsoleCommandParser.TryParse("help", out var inv, out var err);
        Assert.True(ok);
        Assert.Null(err);
        Assert.NotNull(inv);
        Assert.Equal("help", inv!.Command);
        Assert.Empty(inv.PositionalArgs);
        Assert.Empty(inv.NamedArgs);
    }

    [Fact]
    public void ConsoleCommandParser_TryParse_PositionalArgs()
    {
        var ok = ConsoleCommandParser.TryParse("spawn myName myTemplate", out var inv, out _);
        Assert.True(ok);
        Assert.Equal("spawn", inv!.Command);
        Assert.Equal(2, inv.PositionalArgs.Count);
        Assert.Equal("myName", inv.PositionalArgs[0]);
        Assert.Equal("myTemplate", inv.PositionalArgs[1]);
    }

    [Fact]
    public void ConsoleCommandParser_TryParse_NamedArgs()
    {
        var ok = ConsoleCommandParser.TryParse("spawn name=myName template=myTpl", out var inv, out _);
        Assert.True(ok);
        Assert.Equal("spawn", inv!.Command);
        Assert.Empty(inv.PositionalArgs);
        Assert.Equal("myName", inv.NamedArgs["name"]);
        Assert.Equal("myTpl", inv.NamedArgs["template"]);
    }

    [Fact]
    public void ConsoleCommandParser_TryParse_InvalidNamedArg_Fails()
    {
        var ok = ConsoleCommandParser.TryParse("cmd =value", out _, out var err);
        Assert.False(ok);
        Assert.NotNull(err);
    }

    [Fact]
    public void ConsoleCommandParser_TryParse_NamedArgMissingValue_Fails()
    {
        var ok = ConsoleCommandParser.TryParse("cmd key=", out _, out var err);
        Assert.False(ok);
        Assert.NotNull(err);
    }
}

// ── ConsoleCommandRouter ───────────────────────────────────────────────

public class ConsoleCommandRouterTests
{
    private sealed class StubHandler : IConsoleCommandHandler
    {
        public string Name { get; }
        public bool WasExecuted { get; private set; }

        public StubHandler(string name) => Name = name;

        public bool TryExecute(CommandInvocation invocation, IConsoleOutputChannel outputChannel, out string? errorMessage)
        {
            WasExecuted = true;
            errorMessage = null;
            return true;
        }
    }

    [Fact]
    public void ConsoleCommandRouter_Register_And_TryExecute_Success()
    {
        var router = new ConsoleCommandRouter();
        var handler = new StubHandler("test");
        router.Register(handler);

        var invocation = new CommandInvocation
        {
            Command = "test",
            PositionalArgs = Array.Empty<string>(),
            NamedArgs = new Dictionary<string, string>()
        };
        var channel = new ConsoleOutputChannel();

        var ok = router.TryExecute(invocation, channel, out var err);
        Assert.True(ok);
        Assert.Null(err);
        Assert.True(handler.WasExecuted);
    }

    [Fact]
    public void ConsoleCommandRouter_TryExecute_UnknownCommand_ReturnsFalse()
    {
        var router = new ConsoleCommandRouter();
        var invocation = new CommandInvocation
        {
            Command = "unknown",
            PositionalArgs = Array.Empty<string>(),
            NamedArgs = new Dictionary<string, string>()
        };
        var channel = new ConsoleOutputChannel();

        var ok = router.TryExecute(invocation, channel, out var err);
        Assert.False(ok);
        Assert.Contains("Unknown command", err);
    }

    [Fact]
    public void ConsoleCommandRouter_Register_NullHandler_Throws()
    {
        var router = new ConsoleCommandRouter();
        Assert.Throws<ArgumentNullException>(() => router.Register(null!));
    }

    [Fact]
    public void ConsoleCommandRouter_Register_CaseInsensitive()
    {
        var router = new ConsoleCommandRouter();
        var handler = new StubHandler("Test");
        router.Register(handler);

        var invocation = new CommandInvocation
        {
            Command = "TEST",
            PositionalArgs = Array.Empty<string>(),
            NamedArgs = new Dictionary<string, string>()
        };
        var channel = new ConsoleOutputChannel();

        var ok = router.TryExecute(invocation, channel, out _);
        Assert.True(ok);
    }
}

// ── ConsoleInputQueue ──────────────────────────────────────────────────

public class ConsoleInputQueueTests
{
    [Fact]
    public void ConsoleInputQueue_Enqueue_And_TryDequeue()
    {
        var queue = new ConsoleInputQueue();
        queue.Enqueue("help");

        var ok = queue.TryDequeueCommand(out var line);
        Assert.True(ok);
        Assert.Equal("help", line);
    }

    [Fact]
    public void ConsoleInputQueue_TryDequeue_EmptyQueue_ReturnsFalse()
    {
        var queue = new ConsoleInputQueue();
        var ok = queue.TryDequeueCommand(out var line);
        Assert.False(ok);
        Assert.Null(line);
    }

    [Fact]
    public void ConsoleInputQueue_Enqueue_WhitespaceIgnored()
    {
        var queue = new ConsoleInputQueue();
        queue.Enqueue("  ");
        queue.Enqueue("");

        var ok = queue.TryDequeueCommand(out _);
        Assert.False(ok);
    }

    [Fact]
    public void ConsoleInputQueue_Enqueue_TrimsInput()
    {
        var queue = new ConsoleInputQueue();
        queue.Enqueue("  hello  ");

        queue.TryDequeueCommand(out var line);
        Assert.Equal("hello", line);
    }

    [Fact]
    public void ConsoleInputQueue_FIFO_Order()
    {
        var queue = new ConsoleInputQueue();
        queue.Enqueue("first");
        queue.Enqueue("second");

        queue.TryDequeueCommand(out var line1);
        queue.TryDequeueCommand(out var line2);
        Assert.Equal("first", line1);
        Assert.Equal("second", line2);
    }

    [Fact]
    public void ConsoleInputQueue_Clear_EmptiesQueue()
    {
        var queue = new ConsoleInputQueue();
        queue.Enqueue("a");
        queue.Enqueue("b");
        queue.Clear();

        Assert.False(queue.TryDequeueCommand(out _));
    }
}

// ── ConsoleOutputChannel ───────────────────────────────────────────────

public class ConsoleOutputChannelTests
{
    [Fact]
    public void ConsoleOutputChannel_Subscribe_And_Publish()
    {
        var channel = new ConsoleOutputChannel();
        var received = new List<string>();
        channel.Subscribe(msg => received.Add(msg));

        channel.Publish("hello");
        Assert.Single(received);
        Assert.Equal("hello", received[0]);
    }

    [Fact]
    public void ConsoleOutputChannel_Unsubscribe_StopsReceiving()
    {
        var channel = new ConsoleOutputChannel();
        var received = new List<string>();
        var id = channel.Subscribe(msg => received.Add(msg));

        channel.Publish("first");
        channel.Unsubscribe(id);
        channel.Publish("second");

        Assert.Single(received);
        Assert.Equal("first", received[0]);
    }

    [Fact]
    public void ConsoleOutputChannel_Unsubscribe_InvalidId_ReturnsFalse()
    {
        var channel = new ConsoleOutputChannel();
        Assert.False(channel.Unsubscribe(9999));
    }

    [Fact]
    public void ConsoleOutputChannel_MultipleSubscribers()
    {
        var channel = new ConsoleOutputChannel();
        var list1 = new List<string>();
        var list2 = new List<string>();
        channel.Subscribe(msg => list1.Add(msg));
        channel.Subscribe(msg => list2.Add(msg));

        channel.Publish("msg");
        Assert.Single(list1);
        Assert.Single(list2);
    }

    [Fact]
    public void ConsoleOutputChannel_Publish_NullBroadcastsEmpty()
    {
        var channel = new ConsoleOutputChannel();
        var received = new List<string>();
        channel.Subscribe(msg => received.Add(msg));

        channel.Publish(null!);
        Assert.Single(received);
        Assert.Equal(string.Empty, received[0]);
    }

    [Fact]
    public void ConsoleOutputChannel_Subscribe_ThrowsOnNull()
    {
        var channel = new ConsoleOutputChannel();
        Assert.Throws<ArgumentNullException>(() => channel.Subscribe(null!));
    }
}

// ── SaveMetaMapCodec ───────────────────────────────────────────────────

public class SaveMetaMapCodecExtendedTests
{
    [Fact]
    public void SaveMetaMapCodec_Parse_BasicContent()
    {
        var logger = new TestLogger();
        var result = SaveMetaMapCodec.Parse("key1: value1\nkey2: value2", logger);
        Assert.Equal(2, result.Count);
        Assert.Equal("value1", result["key1"]);
    }

    [Fact]
    public void SaveMetaMapCodec_Parse_NullContent_ReturnsEmpty()
    {
        var logger = new TestLogger();
        var result = SaveMetaMapCodec.Parse(null, logger);
        Assert.Empty(result);
    }

    [Fact]
    public void SaveMetaMapCodec_Serialize_SortsByKey()
    {
        var map = new Dictionary<string, string> { ["b"] = "2", ["a"] = "1" };
        var text = SaveMetaMapCodec.Serialize(map);
        Assert.StartsWith("a: 1", text);
        Assert.Contains("b: 2", text);
    }

    [Fact]
    public void SaveMetaMapCodec_Serialize_NullMap_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, SaveMetaMapCodec.Serialize(null));
    }

    [Fact]
    public void SaveMetaMapCodec_Serialize_EmptyMap_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, SaveMetaMapCodec.Serialize(new Dictionary<string, string>()));
    }

    [Fact]
    public void SaveMetaMapCodec_RoundTrip()
    {
        var logger = new TestLogger();
        var original = new Dictionary<string, string> { ["name"] = "Test", ["score"] = "100" };
        var serialized = SaveMetaMapCodec.Serialize(original);
        var parsed = SaveMetaMapCodec.Parse(serialized, logger);
        Assert.Equal("Test", parsed["name"]);
        Assert.Equal("100", parsed["score"]);
    }
}

// ── Blackboard ─────────────────────────────────────────────────────────

public class BlackboardTests
{
    [Fact]
    public void Blackboard_Set_And_TryGet_Int()
    {
        var bb = new Origo.Core.Blackboard.Blackboard();
        bb.Set("score", 100);
        var (found, value) = bb.TryGet<int>("score");
        Assert.True(found);
        Assert.Equal(100, value);
    }

    [Fact]
    public void Blackboard_Set_And_TryGet_String()
    {
        var bb = new Origo.Core.Blackboard.Blackboard();
        bb.Set("name", "player");
        var (found, value) = bb.TryGet<string>("name");
        Assert.True(found);
        Assert.Equal("player", value);
    }

    [Fact]
    public void Blackboard_TryGet_MissingKey_ReturnsFalse()
    {
        var bb = new Origo.Core.Blackboard.Blackboard();
        var (found, _) = bb.TryGet<int>("missing");
        Assert.False(found);
    }

    [Fact]
    public void Blackboard_TryGet_WrongType_ReturnsFalse()
    {
        var bb = new Origo.Core.Blackboard.Blackboard();
        bb.Set("key", 42);
        var (found, _) = bb.TryGet<string>("key");
        Assert.False(found);
    }

    [Fact]
    public void Blackboard_Clear_RemovesAll()
    {
        var bb = new Origo.Core.Blackboard.Blackboard();
        bb.Set("a", 1);
        bb.Set("b", 2);
        bb.Clear();
        Assert.Empty(bb.GetKeys());
    }

    [Fact]
    public void Blackboard_GetKeys_ReturnsAllKeys()
    {
        var bb = new Origo.Core.Blackboard.Blackboard();
        bb.Set("a", 1);
        bb.Set("b", "x");
        var keys = bb.GetKeys();
        Assert.Equal(2, keys.Count);
        Assert.Contains("a", keys);
        Assert.Contains("b", keys);
    }

    [Fact]
    public void Blackboard_SerializeAll_And_DeserializeAll_RoundTrip()
    {
        var bb = new Origo.Core.Blackboard.Blackboard();
        bb.Set("key1", 42);
        bb.Set("key2", "val");

        var data = bb.SerializeAll();
        var bb2 = new Origo.Core.Blackboard.Blackboard();
        bb2.DeserializeAll(data);

        Assert.Equal(42, bb2.TryGet<int>("key1").value);
        Assert.Equal("val", bb2.TryGet<string>("key2").value);
    }

    [Fact]
    public void Blackboard_Set_OverwriteExisting()
    {
        var bb = new Origo.Core.Blackboard.Blackboard();
        bb.Set("key", 1);
        bb.Set("key", 2);
        Assert.Equal(2, bb.TryGet<int>("key").value);
    }

    [Fact]
    public void Blackboard_Set_ThrowsOnNullKey()
    {
        var bb = new Origo.Core.Blackboard.Blackboard();
        Assert.Throws<ArgumentException>(() => bb.Set<int>(null!, 1));
    }

    [Fact]
    public void Blackboard_TryGet_ThrowsOnNullKey()
    {
        var bb = new Origo.Core.Blackboard.Blackboard();
        Assert.Throws<ArgumentException>(() => bb.TryGet<int>(null!));
    }

    [Fact]
    public void Blackboard_Set_ThrowsOnWhitespaceKey()
    {
        var bb = new Origo.Core.Blackboard.Blackboard();
        Assert.Throws<ArgumentException>(() => bb.Set("  ", 1));
    }
}

// ── DataObserverManager ────────────────────────────────────────────────

public class DataObserverManagerExtendedTests
{
    [Fact]
    public void DataObserverManager_Subscribe_And_Notify()
    {
        var mgr = new DataObserverManager();
        object? receivedOld = null, receivedNew = null;
        mgr.Subscribe("hp", (o, n) => { receivedOld = o; receivedNew = n; });

        mgr.NotifyObservers("hp", 100, 80);
        Assert.Equal(100, receivedOld);
        Assert.Equal(80, receivedNew);
    }

    [Fact]
    public void DataObserverManager_Unsubscribe_StopsNotification()
    {
        var mgr = new DataObserverManager();
        var callCount = 0;
        Action<object?, object?> callback = (_, _) => callCount++;

        mgr.Subscribe("hp", callback);
        mgr.NotifyObservers("hp", null, null);
        Assert.Equal(1, callCount);

        mgr.Unsubscribe("hp", callback);
        mgr.NotifyObservers("hp", null, null);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void DataObserverManager_Subscribe_WithFilter_SkipsFiltered()
    {
        var mgr = new DataObserverManager();
        var callCount = 0;
        mgr.Subscribe("hp", (_, _) => callCount++, (o, n) => (int)n! > 50);

        mgr.NotifyObservers("hp", 0, 80);
        Assert.Equal(1, callCount);

        mgr.NotifyObservers("hp", 0, 30);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void DataObserverManager_Clear_RemovesAllSubscriptions()
    {
        var mgr = new DataObserverManager();
        var callCount = 0;
        mgr.Subscribe("hp", (_, _) => callCount++);
        mgr.Clear();
        mgr.NotifyObservers("hp", null, null);
        Assert.Equal(0, callCount);
    }

    [Fact]
    public void DataObserverManager_NotifyObservers_UnknownName_DoesNotThrow()
    {
        var mgr = new DataObserverManager();
        mgr.NotifyObservers("nonexistent", null, null);
    }

    [Fact]
    public void DataObserverManager_Unsubscribe_UnknownName_DoesNotThrow()
    {
        var mgr = new DataObserverManager();
        mgr.Unsubscribe("nonexistent", (_, _) => { });
    }

    [Fact]
    public void DataObserverManager_MultipleSubscribers_AllNotified()
    {
        var mgr = new DataObserverManager();
        var count1 = 0;
        var count2 = 0;
        mgr.Subscribe("hp", (_, _) => count1++);
        mgr.Subscribe("hp", (_, _) => count2++);

        mgr.NotifyObservers("hp", null, null);
        Assert.Equal(1, count1);
        Assert.Equal(1, count2);
    }
}

// ── SndSceneJsonSerializer ─────────────────────────────────────────────

public class SndSceneJsonSerializerTests
{
    private static SndWorld CreateWorld()
    {
        return new SndWorld(new TypeStringMapping(), NullLogger.Instance);
    }

    [Fact]
    public void SndSceneJsonSerializer_Serialize_EmptyScene()
    {
        var world = CreateWorld();
        var serializer = new SndSceneJsonSerializer(world);
        var host = new TestSndSceneHost();

        var json = serializer.Serialize(host);
        Assert.NotNull(json);
        Assert.Contains("[", json);
    }

    [Fact]
    public void SndSceneJsonSerializer_RoundTrip_PreservesMetaList()
    {
        var world = CreateWorld();
        var serializer = new SndSceneJsonSerializer(world);

        var host1 = new TestSndSceneHost();
        host1.Spawn(new SndMetaData { Name = "entity1" });

        var json = serializer.Serialize(host1);

        var host2 = new TestSndSceneHost();
        serializer.DeserializeInto(host2, json, true);

        var metaList = host2.SerializeMetaList();
        Assert.Single(metaList);
        Assert.Equal("entity1", metaList[0].Name);
    }

    [Fact]
    public void SndSceneJsonSerializer_DeserializeInto_ClearsBeforeLoad()
    {
        var world = CreateWorld();
        var serializer = new SndSceneJsonSerializer(world);
        var host = new TestSndSceneHost();
        host.Spawn(new SndMetaData { Name = "existing" });

        serializer.DeserializeInto(host, "[]", true);
        Assert.Equal(1, host.ClearAllCount);
    }

    [Fact]
    public void SndSceneJsonSerializer_DeserializeInto_NoClearWhenFalse()
    {
        var world = CreateWorld();
        var serializer = new SndSceneJsonSerializer(world);
        var host = new TestSndSceneHost();

        serializer.DeserializeInto(host, "[]", false);
        Assert.Equal(0, host.ClearAllCount);
    }

    [Fact]
    public void SndSceneJsonSerializer_DeserializeInto_InvalidJson_Throws()
    {
        var world = CreateWorld();
        var serializer = new SndSceneJsonSerializer(world);
        var host = new TestSndSceneHost();

        Assert.ThrowsAny<Exception>(() => serializer.DeserializeInto(host, "{}", true));
    }

    [Fact]
    public void SndSceneJsonSerializer_Constructor_ThrowsOnNullWorld()
    {
        Assert.Throws<ArgumentNullException>(() => new SndSceneJsonSerializer(null!));
    }
}

// ── DelegateSaveMetaContributor ────────────────────────────────────────

public class DelegateSaveMetaContributorTests
{
    [Fact]
    public void DelegateSaveMetaContributor_Contribute_InvokesDelegate()
    {
        var invoked = false;
        var contributor = new DelegateSaveMetaContributor((ctx, meta) =>
        {
            invoked = true;
            meta["custom_key"] = "custom_value";
        });

        var bb = new Origo.Core.Blackboard.Blackboard();
        var host = new TestSndSceneHost();
        var context = new SaveMetaBuildContext("save1", "level1", bb, bb, host);
        var dict = new Dictionary<string, string>();

        contributor.Contribute(context, dict);
        Assert.True(invoked);
        Assert.Equal("custom_value", dict["custom_key"]);
    }

    [Fact]
    public void DelegateSaveMetaContributor_Constructor_ThrowsOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => new DelegateSaveMetaContributor(null!));
    }
}

// ── SaveContext ─────────────────────────────────────────────────────────

public class SaveContextTests
{
    private static SndWorld CreateWorld()
    {
        return new SndWorld(new TypeStringMapping(), NullLogger.Instance);
    }

    [Fact]
    public void SaveContext_SerializeProgress_And_DeserializeProgress_RoundTrip()
    {
        var world = CreateWorld();
        var progress = new Origo.Core.Blackboard.Blackboard();
        var session = new Origo.Core.Blackboard.Blackboard();
        progress.Set("stage", 3);

        var ctx = new SaveContext(progress, session, world);
        var json = ctx.SerializeProgress();

        var progress2 = new Origo.Core.Blackboard.Blackboard();
        var ctx2 = new SaveContext(progress2, new Origo.Core.Blackboard.Blackboard(), world);
        ctx2.DeserializeProgress(json);

        Assert.Equal(3, progress2.TryGet<int>("stage").value);
    }

    [Fact]
    public void SaveContext_SerializeSession_And_DeserializeSession_RoundTrip()
    {
        var world = CreateWorld();
        var progress = new Origo.Core.Blackboard.Blackboard();
        var session = new Origo.Core.Blackboard.Blackboard();
        session.Set("hp", 100);

        var ctx = new SaveContext(progress, session, world);
        var json = ctx.SerializeSession();

        var session2 = new Origo.Core.Blackboard.Blackboard();
        var ctx2 = new SaveContext(new Origo.Core.Blackboard.Blackboard(), session2, world);
        ctx2.DeserializeSession(json);

        Assert.Equal(100, session2.TryGet<int>("hp").value);
    }

    [Fact]
    public void SaveContext_SerializeSndScene_ReturnsJson()
    {
        var world = CreateWorld();
        var ctx = new SaveContext(new Origo.Core.Blackboard.Blackboard(), new Origo.Core.Blackboard.Blackboard(), world);
        var host = new TestSndSceneHost();

        var json = ctx.SerializeSndScene(host);
        Assert.NotNull(json);
    }

    [Fact]
    public void SaveContext_DeserializeSndScene_ClearsAndLoads()
    {
        var world = CreateWorld();
        var ctx = new SaveContext(new Origo.Core.Blackboard.Blackboard(), new Origo.Core.Blackboard.Blackboard(), world);
        var host = new TestSndSceneHost();
        host.Spawn(new SndMetaData { Name = "old" });

        ctx.DeserializeSndScene(host, "[]", true);
        Assert.Equal(1, host.ClearAllCount);
    }

    [Fact]
    public void SaveContext_SaveGame_CreatesSaveGamePayload()
    {
        var world = CreateWorld();
        var progress = new Origo.Core.Blackboard.Blackboard();
        var session = new Origo.Core.Blackboard.Blackboard();
        progress.Set("gold", 500);

        var ctx = new SaveContext(progress, session, world);
        var host = new TestSndSceneHost();

        var payload = ctx.SaveGame(host, "slot1", "level1", null, "{}", "{}");

        Assert.Equal("slot1", payload.SaveId);
        Assert.Equal("level1", payload.ActiveLevelId);
        Assert.NotNull(payload.ProgressJson);
        Assert.Contains("level1", payload.Levels.Keys);
    }

    [Fact]
    public void SaveContext_SaveGame_WithCustomMeta()
    {
        var world = CreateWorld();
        var ctx = new SaveContext(new Origo.Core.Blackboard.Blackboard(), new Origo.Core.Blackboard.Blackboard(), world);
        var host = new TestSndSceneHost();
        var meta = new Dictionary<string, string> { ["display"] = "Save 1" };

        var payload = ctx.SaveGame(host, "slot1", "level1", meta, "{}", "{}");

        Assert.NotNull(payload.CustomMeta);
        Assert.Equal("Save 1", payload.CustomMeta!["display"]);
    }

    [Fact]
    public void SaveContext_Constructor_ThrowsOnNullArgs()
    {
        var world = CreateWorld();
        var bb = new Origo.Core.Blackboard.Blackboard();
        Assert.Throws<ArgumentNullException>(() => new SaveContext(null!, bb, world));
        Assert.Throws<ArgumentNullException>(() => new SaveContext(bb, null!, world));
        Assert.Throws<ArgumentNullException>(() => new SaveContext(bb, bb, null!));
    }

    [Fact]
    public void SaveContext_Properties_ExposeBlackboards()
    {
        var world = CreateWorld();
        var progress = new Origo.Core.Blackboard.Blackboard();
        var session = new Origo.Core.Blackboard.Blackboard();
        var ctx = new SaveContext(progress, session, world);

        Assert.Same(progress, ctx.Progress);
        Assert.Same(session, ctx.Session);
        Assert.Same(world, ctx.SndWorld);
    }
}

// ── NullLogger ─────────────────────────────────────────────────────────

public class NullLoggerTests
{
    [Fact]
    public void NullLogger_Instance_IsSingleton()
    {
        Assert.Same(NullLogger.Instance, NullLogger.Instance);
    }

    [Fact]
    public void NullLogger_Log_DoesNotThrow()
    {
        NullLogger.Instance.Log(LogLevel.Info, "tag", "message");
        NullLogger.Instance.Log(LogLevel.Warning, "tag", "message");
        NullLogger.Instance.Log(LogLevel.Error, "tag", "message");
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
    public void WellKnownKeys_ActiveSaveId_HasExpectedValue()
    {
        Assert.Equal("origo.active_save_id", WellKnownKeys.ActiveSaveId);
    }

    [Fact]
    public void WellKnownKeys_ActiveLevelId_HasExpectedValue()
    {
        Assert.Equal("origo.active_level_id", WellKnownKeys.ActiveLevelId);
    }
}

// ── TypeStringMapping additional tests ─────────────────────────────────

public class TypeStringMappingExtendedTests
{
    [Fact]
    public void TypeStringMapping_GetTypeByName_UnregisteredType_Throws()
    {
        var mapping = new TypeStringMapping();
        Assert.Throws<InvalidOperationException>(() => mapping.GetTypeByName("nonexistent"));
    }

    [Fact]
    public void TypeStringMapping_GetNameByType_UnregisteredType_Throws()
    {
        var mapping = new TypeStringMapping();
        Assert.Throws<InvalidOperationException>(() => mapping.GetNameByType(typeof(DateTime)));
    }

    [Fact]
    public void TypeStringMapping_RegisterType_DuplicateSameType_NoThrow()
    {
        var mapping = new TypeStringMapping();
        // Re-registering same type->name pair should not throw
        mapping.RegisterType<int>(BclTypeNames.Int32);
    }

    [Fact]
    public void TypeStringMapping_RegisterType_ConflictingNameToType_Throws()
    {
        var mapping = new TypeStringMapping();
        // "Int32" is already mapped to int; trying to map it to long should throw
        Assert.Throws<InvalidOperationException>(() => mapping.RegisterType<long>(BclTypeNames.Int32));
    }

    [Fact]
    public void TypeStringMapping_RegisterType_ConflictingTypeToName_Throws()
    {
        var mapping = new TypeStringMapping();
        // int is already mapped to "Int32"; trying to map it to "MyInt" should throw
        Assert.Throws<InvalidOperationException>(() => mapping.RegisterType<int>("MyInt"));
    }

    [Fact]
    public void TypeStringMapping_RegisterType_WhitespaceName_Throws()
    {
        var mapping = new TypeStringMapping();
        Assert.Throws<ArgumentException>(() => mapping.RegisterType<Guid>(""));
        Assert.Throws<ArgumentException>(() => mapping.RegisterType<Guid>("  "));
    }

    [Fact]
    public void TypeStringMapping_BclTypes_AllPreregistered()
    {
        var mapping = new TypeStringMapping();
        Assert.Equal(typeof(int), mapping.GetTypeByName(BclTypeNames.Int32));
        Assert.Equal(typeof(string), mapping.GetTypeByName(BclTypeNames.String));
        Assert.Equal(typeof(bool), mapping.GetTypeByName(BclTypeNames.Boolean));
        Assert.Equal(typeof(float), mapping.GetTypeByName(BclTypeNames.Single));
        Assert.Equal(typeof(double), mapping.GetTypeByName(BclTypeNames.Double));
        Assert.Equal(typeof(long), mapping.GetTypeByName(BclTypeNames.Int64));
        Assert.Equal(typeof(short), mapping.GetTypeByName(BclTypeNames.Int16));
        Assert.Equal(typeof(byte), mapping.GetTypeByName(BclTypeNames.Byte));
        Assert.Equal(typeof(string[]), mapping.GetTypeByName(BclTypeNames.ArrayString));
    }

    [Fact]
    public void TypeStringMapping_RegisterCustomType_RoundTrips()
    {
        var mapping = new TypeStringMapping();
        mapping.RegisterType<Guid>("Guid");
        Assert.Equal(typeof(Guid), mapping.GetTypeByName("Guid"));
        Assert.Equal("Guid", mapping.GetNameByType(typeof(Guid)));
    }
}

// ── RandomNumberGenerator additional tests ─────────────────────────────

public class RandomNumberGeneratorExtendedTests
{
    [Fact]
    public void RandomNumberGenerator_SameSeed_ProducesSameSequence()
    {
        var rng1 = new RandomNumberGenerator();
        rng1.Initialize("test-seed");
        var seq1 = Enumerable.Range(0, 10).Select(_ => rng1.NextUInt64()).ToList();

        var rng2 = new RandomNumberGenerator();
        rng2.Initialize("test-seed");
        var seq2 = Enumerable.Range(0, 10).Select(_ => rng2.NextUInt64()).ToList();

        Assert.Equal(seq1, seq2);
    }

    [Fact]
    public void RandomNumberGenerator_DifferentSeed_ProducesDifferentSequence()
    {
        var rng1 = new RandomNumberGenerator();
        rng1.Initialize("seed-a");
        var val1 = rng1.NextUInt64();

        var rng2 = new RandomNumberGenerator();
        rng2.Initialize("seed-b");
        var val2 = rng2.NextUInt64();

        Assert.NotEqual(val1, val2);
    }

    [Fact]
    public void RandomNumberGenerator_NextInt32_ReturnsValue()
    {
        var rng = new RandomNumberGenerator();
        rng.Initialize("seed");
        var val = rng.NextInt32();
        // Just verifying it returns without throwing; value is deterministic
        Assert.IsType<int>(val);
    }

    [Fact]
    public void RandomNumberGenerator_NextInt64_ReturnsValue()
    {
        var rng = new RandomNumberGenerator();
        rng.Initialize("seed");
        var val = rng.NextInt64();
        Assert.IsType<long>(val);
    }

    [Fact]
    public void RandomNumberGenerator_Sequence_IsNotConstant()
    {
        var rng = new RandomNumberGenerator();
        rng.Initialize("varies");
        var values = Enumerable.Range(0, 100).Select(_ => rng.NextUInt64()).ToHashSet();
        // With 100 values from a good RNG, we expect at least many unique values
        Assert.True(values.Count > 90);
    }

    [Fact]
    public void RandomNumberGenerator_WithoutInitialize_StillProducesValues()
    {
        var rng = new RandomNumberGenerator();
        // Default state should still produce values
        var val = rng.NextUInt64();
        Assert.IsType<ulong>(val);
    }
}

// ── TypedData ──────────────────────────────────────────────────────────

public class TypedDataTests
{
    [Fact]
    public void TypedData_Constructor_StoresTypeAndValue()
    {
        var td = new TypedData(typeof(int), 42);
        Assert.Equal(typeof(int), td.DataType);
        Assert.Equal(42, td.Data);
    }

    [Fact]
    public void TypedData_NullValue_Allowed()
    {
        var td = new TypedData(typeof(string), null);
        Assert.Equal(typeof(string), td.DataType);
        Assert.Null(td.Data);
    }
}

// ── SndMetaData ────────────────────────────────────────────────────────

public class SndMetaDataTests
{
    [Fact]
    public void SndMetaData_DeepClone_CopiesName()
    {
        var meta = new SndMetaData { Name = "test" };
        var clone = meta.DeepClone();
        Assert.Equal("test", clone.Name);
    }

    [Fact]
    public void SndMetaData_DeepClone_CopiesNodeMetaData()
    {
        var meta = new SndMetaData
        {
            Name = "entity",
            NodeMetaData = new NodeMetaData { Pairs = new Dictionary<string, string> { ["res"] = "sprite.png" } }
        };
        var clone = meta.DeepClone();
        Assert.NotSame(meta.NodeMetaData, clone.NodeMetaData);
        Assert.Equal("sprite.png", clone.NodeMetaData!.Pairs["res"]);
    }

    [Fact]
    public void SndMetaData_DeepClone_CopiesStrategyMetaData()
    {
        var meta = new SndMetaData
        {
            Name = "entity",
            StrategyMetaData = new StrategyMetaData { Indices = new List<string> { "strat1", "strat2" } }
        };
        var clone = meta.DeepClone();
        Assert.NotSame(meta.StrategyMetaData!.Indices, clone.StrategyMetaData!.Indices);
        Assert.Equal(2, clone.StrategyMetaData.Indices.Count);
    }

    [Fact]
    public void SndMetaData_DeepClone_NullNodeMetaData_RemainsNull()
    {
        var meta = new SndMetaData { Name = "e", NodeMetaData = null };
        var clone = meta.DeepClone();
        Assert.Null(clone.NodeMetaData);
    }

    [Fact]
    public void SndMetaData_DefaultValues()
    {
        var meta = new SndMetaData();
        Assert.Equal(string.Empty, meta.Name);
        Assert.Null(meta.NodeMetaData);
        Assert.Null(meta.StrategyMetaData);
        Assert.NotNull(meta.DataMetaData);
    }
}

// ── SaveMetaBuildContext ────────────────────────────────────────────────

public class SaveMetaBuildContextTests
{
    [Fact]
    public void SaveMetaBuildContext_StoresAllProperties()
    {
        var progress = new Origo.Core.Blackboard.Blackboard();
        var session = new Origo.Core.Blackboard.Blackboard();
        var host = new TestSndSceneHost();
        var ctx = new SaveMetaBuildContext("s1", "lvl1", progress, session, host);

        Assert.Equal("s1", ctx.SaveId);
        Assert.Equal("lvl1", ctx.CurrentLevelId);
        Assert.Same(progress, ctx.Progress);
        Assert.Same(session, ctx.Session);
        Assert.Same(host, ctx.SceneAccess);
    }

    [Fact]
    public void SaveMetaBuildContext_ThrowsOnNullArgs()
    {
        var bb = new Origo.Core.Blackboard.Blackboard();
        var host = new TestSndSceneHost();

        Assert.Throws<ArgumentNullException>(() => new SaveMetaBuildContext(null!, "l", bb, bb, host));
        Assert.Throws<ArgumentNullException>(() => new SaveMetaBuildContext("s", null!, bb, bb, host));
        Assert.Throws<ArgumentNullException>(() => new SaveMetaBuildContext("s", "l", null!, bb, host));
        Assert.Throws<ArgumentNullException>(() => new SaveMetaBuildContext("s", "l", bb, null!, host));
        Assert.Throws<ArgumentNullException>(() => new SaveMetaBuildContext("s", "l", bb, bb, null!));
    }
}

// ── SaveGamePayload ────────────────────────────────────────────────────

public class SaveGamePayloadTests
{
    [Fact]
    public void SaveGamePayload_CurrentFormatVersion_IsOne()
    {
        Assert.Equal(1, SaveGamePayload.CurrentFormatVersion);
    }

    [Fact]
    public void SaveGamePayload_DefaultValues()
    {
        var payload = new SaveGamePayload();
        Assert.Equal(SaveGamePayload.CurrentFormatVersion, payload.FormatVersion);
        Assert.Equal(string.Empty, payload.SaveId);
        Assert.Equal(string.Empty, payload.ActiveLevelId);
        Assert.Equal(string.Empty, payload.ProgressJson);
        Assert.NotNull(payload.Levels);
    }

    [Fact]
    public void LevelPayload_DefaultValues()
    {
        var lp = new LevelPayload();
        Assert.Equal(string.Empty, lp.LevelId);
        Assert.Equal(string.Empty, lp.SndSceneJson);
        Assert.Equal(string.Empty, lp.SessionJson);
        Assert.Equal(string.Empty, lp.SessionStateMachinesJson);
    }
}

// ── OrigoRuntime integration ───────────────────────────────────────────

public class OrigoRuntimeBasicTests
{
    [Fact]
    public void OrigoRuntime_Constructor_CreatesSndWorld()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = new OrigoRuntime(logger, host, new TypeStringMapping(),
            systemBlackboard: new Origo.Core.Blackboard.Blackboard());

        Assert.NotNull(runtime.SndWorld);
        Assert.Same(logger, runtime.Logger);
    }

    [Fact]
    public void OrigoRuntime_ConsoleInputQueue_NullWithoutInjection()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = new OrigoRuntime(logger, host, new TypeStringMapping(),
            systemBlackboard: new Origo.Core.Blackboard.Blackboard());

        Assert.Null(runtime.ConsoleInput);
        Assert.Null(runtime.ConsoleOutputChannel);
        Assert.Null(runtime.Console);
    }

    [Fact]
    public void OrigoRuntime_WithConsole_CreatesConsole()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var inputQueue = new ConsoleInputQueue();
        var outputChannel = new ConsoleOutputChannel();
        var runtime = new OrigoRuntime(logger, host, new TypeStringMapping(),
            systemBlackboard: new Origo.Core.Blackboard.Blackboard(),
            consoleInput: inputQueue,
            consoleOutputChannel: outputChannel);

        Assert.NotNull(runtime.Console);
        Assert.Same(inputQueue, runtime.ConsoleInput);
        Assert.Same(outputChannel, runtime.ConsoleOutputChannel);
    }

    [Fact]
    public void OrigoRuntime_ResetConsoleState_ClearsInputQueue()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var inputQueue = new ConsoleInputQueue();
        inputQueue.Enqueue("test");
        var outputChannel = new ConsoleOutputChannel();
        var runtime = new OrigoRuntime(logger, host, new TypeStringMapping(),
            systemBlackboard: new Origo.Core.Blackboard.Blackboard(),
            consoleInput: inputQueue,
            consoleOutputChannel: outputChannel);

        runtime.ResetConsoleState();
        Assert.False(inputQueue.TryDequeueCommand(out _));
    }

    [Fact]
    public void OrigoRuntime_FlushEndOfFrameDeferred_ExecutesDeferredActions()
    {
        var logger = new TestLogger();
        var host = new TestSndSceneHost();
        var runtime = new OrigoRuntime(logger, host, new TypeStringMapping(),
            systemBlackboard: new Origo.Core.Blackboard.Blackboard());

        var businessRan = false;
        var systemRan = false;
        runtime.EnqueueBusinessDeferred(() => businessRan = true);
        runtime.EnqueueSystemDeferred(() => systemRan = true);
        runtime.FlushEndOfFrameDeferred();

        Assert.True(businessRan);
        Assert.True(systemRan);
    }
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
        Assert.Throws<System.IO.IOException>(() => fs.WriteAllText("file.txt", "v2", false));
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
}
