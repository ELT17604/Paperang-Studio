namespace Paperang.Core;

public static class AppVersion
{
    public const string Current = "0.3.1-winlite-opt1";
}

// Injected only by diagnostics/tests; production uses 2 s polling and 60 s cooldown.
public sealed record RelayTiming(TimeSpan PollInterval, TimeSpan RetryCooldown)
{
    public static RelayTiming Default { get; } = new(TimeSpan.FromSeconds(2), TimeSpan.FromMinutes(1));
}
