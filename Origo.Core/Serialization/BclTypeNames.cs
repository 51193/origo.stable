namespace Origo.Core.Serialization;

/// <summary>
///     Stable JSON type discriminator strings for built-in CLR types in <see cref="TypeStringMapping" />.
/// </summary>
internal static class BclTypeNames
{
    internal const string Byte = "Byte";
    internal const string Int16 = "Int16";
    internal const string Int32 = "Int32";
    internal const string Int64 = "Int64";
    internal const string Boolean = "Boolean";
    internal const string Single = "Single";
    internal const string Double = "Double";
    internal const string String = "String";
    internal const string ArrayString = "ArrayString";
}
