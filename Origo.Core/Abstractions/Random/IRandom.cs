namespace Origo.Core.Abstractions.Random;

/// <summary>
///     抽象出的随机数服务接口，用于替代具体实现中的单例随机数管理器。
/// </summary>
public interface IRandom
{
    /// <summary>
    ///     使用给定种子初始化随机序列。
    /// </summary>
    /// <param name="seed">任意可序列化的种子字符串。</param>
    void Initialize(string seed);

    ulong NextUInt64();

    long NextInt64();

    int NextInt32();
}
