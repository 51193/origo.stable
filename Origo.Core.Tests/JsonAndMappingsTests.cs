using System;
using System.Collections.Generic;
using System.Text.Json;
using Origo.Core.Abstractions;
using Origo.Core.Serialization;
using Origo.Core.Snd;
using Xunit;

namespace Origo.Core.Tests;

public class JsonAndMappingsTests
{
    private const string StrategyMove = "test.move";
    private const string StrategyAttack = "test.attack";
    private const string StrategyAi = "test.ai";
    private const string StrategyTalk = "test.talk";

    [Fact]
    public void OrigoJson_SndMetaData_RoundTripPreservesTypedData()
    {
        var typeMapping = new TypeStringMapping();
        var options = OrigoJson.CreateDefaultOptions(typeMapping);
        var meta = new SndMetaData
        {
            Name = "Hero",
            NodeMetaData = new NodeMetaData { Pairs = new Dictionary<string, string> { ["body"] = "hero_prefab" } },
            StrategyMetaData = new StrategyMetaData { Indices = new List<string> { StrategyMove, StrategyAttack } },
            DataMetaData = new DataMetaData
            {
                Pairs = new Dictionary<string, TypedData>
                {
                    ["hp"] = new(typeof(int), 100),
                    ["title"] = new(typeof(string), "Knight")
                }
            }
        };

        var json = OrigoJson.SerializeSndMetaData(meta, options);
        var parsed = OrigoJson.DeserializeSndMetaData(json, options);

        Assert.Equal("Hero", parsed.Name);
        Assert.Equal("hero_prefab", parsed.NodeMetaData!.Pairs["body"]);
        Assert.Equal(new[] { StrategyMove, StrategyAttack }, parsed.StrategyMetaData!.Indices);
        Assert.Equal(100, Assert.IsType<int>(parsed.DataMetaData!.Pairs["hp"].Data));
        Assert.Equal("Knight", Assert.IsType<string>(parsed.DataMetaData.Pairs["title"].Data));
    }

    [Fact]
    public void SndMappings_LoadSceneAliases_DuplicateKey_LogsWarningAndLastWins()
    {
        var fs = new TestFileSystem();
        fs.SeedFile("maps/dup_scenes.map", "hero: res://first.tscn\nhero: res://second.tscn\n");
        var mappings = new SndMappings();
        var logger = new TestLogger();
        mappings.LoadSceneAliases(fs, "maps/dup_scenes.map", logger);

        Assert.Equal("res://second.tscn", mappings.ResolveSceneAlias("hero"));
        Assert.Contains(logger.Warnings, w => w.Contains("Duplicate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SndMappings_LoadSceneAliasesAndTemplates_ResolveExpectedValues()
    {
        var fs = new TestFileSystem();
        fs.SeedFile("maps/scenes.map", "# comment\nhero: res://hero.tscn\nui: res://ui/menu.tscn");
        fs.SeedFile("maps/templates.map", "hero_template: templates/hero.json");
        fs.SeedFile("templates/hero.json",
            """
            {
              "name": "TemplateHero",
              "node": { "pairs": { "root": "hero" } },
              "strategy": { "indices": [ "test.move" ] },
              "data": { "pairs": { "hp": { "type": "Int32", "data": 150 } } }
            }
            """);

        var mappings = new SndMappings();
        var logger = new TestLogger();
        var options = OrigoJson.CreateDefaultOptions(new TypeStringMapping());

        mappings.LoadSceneAliases(fs, "maps/scenes.map", logger);
        mappings.LoadTemplates(fs, "maps/templates.map", options, logger);

        Assert.Equal("res://hero.tscn", mappings.ResolveSceneAlias("hero"));
        Assert.Throws<KeyNotFoundException>(() => mappings.ResolveSceneAlias("missing_alias"));

        var template = mappings.ResolveTemplate("hero_template");
        Assert.Equal("TemplateHero", template.Name);
        Assert.Equal(150, Assert.IsType<int>(template.DataMetaData!.Pairs["hp"].Data));

        var readsAfterFirstResolve = fs.ReadAllTextCallCount;
        _ = mappings.ResolveTemplate("hero_template");
        Assert.Equal(readsAfterFirstResolve, fs.ReadAllTextCallCount);
    }

    [Fact]
    public void SndMappings_ResolveMetaListFromJsonArray_SupportsTemplateAndInlineMix()
    {
        var fs = new TestFileSystem();
        fs.SeedFile("maps/templates.map", "enemy_template: templates/enemy.json");
        fs.SeedFile("templates/enemy.json",
            """
            {
              "name": "TemplateEnemy",
              "node": { "pairs": { "root": "enemy" } },
              "strategy": { "indices": [ "test.ai" ] },
              "data": { "pairs": { "damage": { "type": "Int32", "data": 8 } } }
            }
            """);
        var options = OrigoJson.CreateDefaultOptions(new TypeStringMapping());
        var mappings = new SndMappings();
        mappings.LoadTemplates(fs, "maps/templates.map", options, NullLogger.Instance);

        using var doc = JsonDocument.Parse(
            """
            [
              { "sndName": "EnemyA", "templateKey": "enemy_template" },
              {
                "name": "Npc",
                "node": { "pairs": { "root": "npc" } },
                "strategy": { "indices": [ "test.talk" ] },
                "data": { "pairs": { "mood": { "type": "String", "data": "Calm" } } }
              }
            ]
            """);

        var metas = mappings.ResolveMetaListFromJsonArray(doc.RootElement, options);

        Assert.Equal(2, metas.Count);
        Assert.Equal("EnemyA", metas[0].Name);
        Assert.Equal(8, Assert.IsType<int>(metas[0].DataMetaData!.Pairs["damage"].Data));
        Assert.Equal("Npc", metas[1].Name);
        Assert.Equal("Calm", Assert.IsType<string>(metas[1].DataMetaData!.Pairs["mood"].Data));
    }

    [Fact]
    public void TypedDataJson_DataPropertyBeforeType_DeserializesCorrectly()
    {
        var typeMapping = new TypeStringMapping();
        var options = OrigoJson.CreateDefaultOptions(typeMapping);
        const string json = """{"data":42,"type":"Int32"}""";

        var td = JsonSerializer.Deserialize<TypedData>(json, options);
        Assert.NotNull(td);
        Assert.Equal(typeof(int), td!.DataType);
        Assert.Equal(42, Assert.IsType<int>(td.Data));
    }

    [Fact]
    public void Blackboard_SerializeAll_ReturnsDetachedCopy()
    {
        var bb = new Origo.Core.Blackboard.Blackboard();
        bb.Set("k", 1);

        var exported = bb.SerializeAll();
        Assert.Single(exported);
        Assert.Equal(1, Assert.IsType<int>(exported["k"].Data));

        ((Dictionary<string, TypedData>)exported).Clear();
        Assert.Single(bb.GetKeys());
        var (foundK, kVal) = bb.TryGet<int>("k");
        Assert.True(foundK);
        Assert.Equal(1, kVal);
    }

    [Fact]
    public void SndMappings_ResolveTemplate_BeforeLoadTemplates_Throws()
    {
        var mappings = new SndMappings();
        Assert.Throws<InvalidOperationException>(() => mappings.ResolveTemplate("any"));
    }

    [Fact]
    public void SndMappings_ResolveTemplate_AfterLoadTemplatesWithEmptyMap_Throws()
    {
        var fs = new TestFileSystem();
        fs.SeedFile("maps/empty_templates.map", "# no entries\n");
        var options = OrigoJson.CreateDefaultOptions(new TypeStringMapping());
        var mappings = new SndMappings();
        mappings.LoadTemplates(fs, "maps/empty_templates.map", options, NullLogger.Instance);

        var ex = Assert.Throws<InvalidOperationException>(() => mappings.ResolveTemplate("any_alias"));
        Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SndMappings_ResolveTemplate_InvalidJson_Throws()
    {
        var fs = new TestFileSystem();
        fs.SeedFile("maps/templates.map", "bad_template: templates/bad.json");
        fs.SeedFile("templates/bad.json", "{ invalid-json");
        var options = OrigoJson.CreateDefaultOptions(new TypeStringMapping());
        var mappings = new SndMappings();
        var logger = new TestLogger();
        mappings.LoadTemplates(fs, "maps/templates.map", options, logger);

        Assert.Throws<JsonException>(() => mappings.ResolveTemplate("bad_template"));
    }
}
