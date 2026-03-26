using System;
using System.Collections.Generic;
using System.Reflection;
using Origo.Core.Abstractions;
using Origo.Core.Logging;

namespace Origo.Core.Snd.Strategy;

/// <summary>
///     负责按索引管理与复用策略实例的池。
///     不使用反射自动收集，而是通过显式注册提升可控性与可测试性。
/// </summary>
internal sealed class SndStrategyPool
{
    private readonly Dictionary<string, Func<BaseSndStrategy>> _factories = new();
    private readonly ILogger? _logger;
    private readonly Dictionary<string, BaseSndStrategy> _pool = new();
    private readonly Dictionary<string, int> _refCounts = new();

    public SndStrategyPool(ILogger? logger = null)
    {
        _logger = logger;
    }

    public void Register(Type strategyType, Func<BaseSndStrategy> factory)
    {
        ArgumentNullException.ThrowIfNull(strategyType);
        var index = ResolveRequiredIndex(strategyType);
        _factories[index] = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public void Register<TStrategy>(Func<TStrategy> factory) where TStrategy : BaseSndStrategy
    {
        ArgumentNullException.ThrowIfNull(factory);
        Register(typeof(TStrategy), () => factory());
    }

    public BaseSndStrategy? GetStrategy(string index)
    {
        if (_pool.TryGetValue(index, out var strategy))
        {
            _refCounts[index]++;
            return strategy;
        }

        if (_factories.TryGetValue(index, out var factory))
        {
            strategy = factory();
            _pool[index] = strategy;
            _refCounts[index] = 1;
            _logger?.Log(LogLevel.Info, nameof(SndStrategyPool),
                new LogMessageBuilder().AddSuffix("strategyIndex", index).Build("Created new strategy instance."));
            return strategy;
        }

        throw new InvalidOperationException($"Strategy factory for '{index}' not found.");
    }

    public void ReleaseStrategy(string index)
    {
        if (_refCounts.TryGetValue(index, out var count))
        {
            count--;
            if (count <= 0)
            {
                if (count < 0)
                    _logger?.Log(LogLevel.Warning, nameof(SndStrategyPool),
                        new LogMessageBuilder().AddSuffix("strategyIndex", index)
                            .Build("Reference count went below zero."));

                _pool.Remove(index);
                _refCounts.Remove(index);
                _logger?.Log(LogLevel.Info, nameof(SndStrategyPool),
                    new LogMessageBuilder().AddSuffix("strategyIndex", index).Build("Released strategy instance."));
            }
            else
            {
                _refCounts[index] = count;
            }
        }
        else
        {
            _logger?.Log(LogLevel.Warning, nameof(SndStrategyPool),
                new LogMessageBuilder().AddSuffix("strategyIndex", index).Build("Attempted to release unknown strategy."));
        }
    }

    private static string ResolveRequiredIndex(Type strategyType)
    {
        var attr = strategyType.GetCustomAttribute<StrategyIndexAttribute>();
        if (attr == null)
            throw new InvalidOperationException(
                $"Strategy type '{strategyType.FullName}' must declare [StrategyIndex(\"...\")].");
        if (string.IsNullOrWhiteSpace(attr.Index))
            throw new InvalidOperationException(
                $"Strategy type '{strategyType.FullName}' has an empty StrategyIndexAttribute value.");
        return attr.Index;
    }
}