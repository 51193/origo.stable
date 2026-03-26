using System;
using Origo.Core.Abstractions;
using Origo.Core.Serialization;

namespace Origo.Core.Runtime.Console.CommandImpl;

/// <summary>
///     <c>spawn</c> 命令：仅支持 template 模式（name + template 别名）。
/// </summary>
public sealed class SpawnTemplateCommandHandler : IConsoleCommandHandler
{
    private readonly OrigoRuntime _runtime;

    public SpawnTemplateCommandHandler(OrigoRuntime runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public string Name => "spawn";

    public bool TryExecute(
        CommandInvocation invocation,
        IConsoleOutputChannel outputChannel,
        out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(outputChannel);

        if (!TryGetSpawnArgs(invocation, out var entityName, out var templateKey, out var err))
        {
            errorMessage = err;
            return false;
        }

        var jsonOptions = _runtime.SndWorld.JsonOptions;
        Origo.Core.Snd.SndMetaData template;
        try
        {
            template = _runtime.SndWorld.Mappings.ResolveTemplate(templateKey);
        }
        catch (Exception ex)
        {
            errorMessage = $"Failed to resolve template '{templateKey}': {ex.Message}";
            return false;
        }

        var clonedJson = OrigoJson.SerializeSndMetaData(template, jsonOptions);
        var cloned = OrigoJson.DeserializeSndMetaData(clonedJson, jsonOptions);
        if (cloned == null)
        {
            errorMessage = $"Template '{templateKey}' failed to deserialize.";
            return false;
        }
        cloned.Name = entityName;

        _runtime.Snd.Spawn(cloned);

        var msg = $"Spawned '{entityName}' from template '{templateKey}'.";
        outputChannel.Publish(msg);
        errorMessage = null;
        return true;
    }

    private static bool TryGetSpawnArgs(
        CommandInvocation invocation,
        out string entityName,
        out string templateKey,
        out string? error)
    {
        entityName = string.Empty;
        templateKey = string.Empty;

        if (invocation.NamedArgs.Count > 0)
        {
            if (invocation.PositionalArgs.Count > 0)
            {
                error = "Cannot mix named and positional arguments for 'spawn'.";
                return false;
            }

            if (!invocation.NamedArgs.TryGetValue("name", out var n) ||
                string.IsNullOrWhiteSpace(n))
            {
                error = "Missing or invalid 'name=' for 'spawn'.";
                return false;
            }

            if (!invocation.NamedArgs.TryGetValue("template", out var t) ||
                string.IsNullOrWhiteSpace(t))
            {
                error = "Missing or invalid 'template=' for 'spawn'.";
                return false;
            }

            entityName = n.Trim();
            templateKey = t.Trim();
            error = null;
            return true;
        }

        if (invocation.PositionalArgs.Count != 2)
        {
            error = "Usage: spawn <name> <template>  OR  spawn name=<name> template=<template>";
            return false;
        }

        entityName = invocation.PositionalArgs[0].Trim();
        templateKey = invocation.PositionalArgs[1].Trim();

        if (string.IsNullOrWhiteSpace(entityName) || string.IsNullOrWhiteSpace(templateKey))
        {
            error = "Name and template must be non-empty.";
            return false;
        }

        error = null;
        return true;
    }
}