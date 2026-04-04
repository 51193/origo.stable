using Origo.Core.Abstractions.Random;

namespace Origo.Core.Random;

/// <summary>
///     基于 XorShift128+ 的可复现随机数实现，不依赖具体存储或引擎。
/// </summary>
public sealed class RandomNumberGenerator : IRandom
{
    private const ulong FnvOffsetBasis = 0xcbf29ce484222325;
    private const ulong FnvPrime = 0x100000001b3;

    private ulong _state0 = 0xBAD5EED;
    private ulong _state1 = 0xC0FFEEUL;

    public void Initialize(string seed)
    {
        var hash1 = GetStableHash64(seed + "K6205");
        var hash2 = GetStableHash64(seed + "AMADEUS");

        _state0 = hash1 == 0 ? 0xBAD5EED : hash1;
        _state1 = hash2 == 0 ? 0xBAD5EED : hash2;
    }

    public ulong NextUInt64()
    {
        var s1 = _state0;
        var s0 = _state1;
        _state0 = s0;

        s1 ^= s1 << 23;
        s1 ^= s1 >> 17;
        s1 ^= s0;
        s1 ^= s0 >> 26;
        _state1 = s1;

        return _state0 + _state1;
    }

    public long NextInt64() => (long)NextUInt64();

    public int NextInt32() => (int)(NextUInt64() & 0xFFFFFFFF);

    private static ulong GetStableHash64(string str)
    {
        var hash = FnvOffsetBasis;

        foreach (var c in str)
        {
            hash ^= c;
            hash *= FnvPrime;
        }

        return hash;
    }
}
