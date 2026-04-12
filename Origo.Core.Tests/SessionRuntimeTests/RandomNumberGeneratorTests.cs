using System.Linq;
using Origo.Core.Random;
using Xunit;

namespace Origo.Core.Tests;

public class RandomNumberGeneratorTests
{
    [Fact]
    public void SameSeed_ProducesSameSequence()
    {
        var left = new RandomNumberGenerator();
        var right = new RandomNumberGenerator();
        left.Initialize("same-seed");
        right.Initialize("same-seed");

        var a = Enumerable.Range(0, 8).Select(_ => left.NextUInt64()).ToArray();
        var b = Enumerable.Range(0, 8).Select(_ => right.NextUInt64()).ToArray();

        Assert.Equal(a, b);
    }

    [Fact]
    public void DifferentSeed_ProducesDifferentSequence()
    {
        var left = new RandomNumberGenerator();
        var right = new RandomNumberGenerator();
        left.Initialize("seed-a");
        right.Initialize("seed-b");

        var a = Enumerable.Range(0, 8).Select(_ => left.NextUInt64()).ToArray();
        var b = Enumerable.Range(0, 8).Select(_ => right.NextUInt64()).ToArray();

        Assert.NotEqual(a, b);
    }
}
