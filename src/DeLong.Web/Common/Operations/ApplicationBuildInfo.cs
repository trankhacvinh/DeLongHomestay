using System.Reflection;

namespace DeLong.Web.Common.Operations;

public sealed record ApplicationBuildInfo(
    string Version,
    string BuildId,
    string? Commit,
    DateTimeOffset StartedAtUtc)
{
    public static ApplicationBuildInfo Current { get; } = Create();

    private static ApplicationBuildInfo Create()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(ApplicationBuildInfo).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        var versionParts = informationalVersion?.Split('+', 2, StringSplitOptions.TrimEntries);
        var version = versionParts is { Length: > 0 } && !string.IsNullOrWhiteSpace(versionParts[0])
            ? versionParts[0]
            : assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        var commit = versionParts is { Length: 2 } && !string.IsNullOrWhiteSpace(versionParts[1])
            ? versionParts[1]
            : null;
        var buildId = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(x => string.Equals(x.Key, "BuildId", StringComparison.Ordinal))?
            .Value;

        return new ApplicationBuildInfo(
            version,
            string.IsNullOrWhiteSpace(buildId) ? "local" : buildId,
            commit,
            DateTimeOffset.UtcNow);
    }
}
