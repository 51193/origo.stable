namespace Origo.Core.Save;

public static class SaveFormat
{
    // Reserved for future evolution. Early stage strict mode: only current version is supported.
    public const int CurrentVersion = 1;

    // Recommended place to store this is progress blackboard (TypedData) under this key.
    public const string VersionKey = "origo.save_format_version";
}