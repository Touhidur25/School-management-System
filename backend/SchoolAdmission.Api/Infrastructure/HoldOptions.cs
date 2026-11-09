namespace SchoolAdmission.Api.Infrastructure;

/// <summary>Hold lifetime, configured via HOLD_DURATION_SECONDS (default 600).</summary>
public record HoldOptions(int DurationSeconds)
{
    public const int DefaultDurationSeconds = 600;

    public static HoldOptions FromConfiguration(IConfiguration config)
    {
        var raw = config["HOLD_DURATION_SECONDS"];
        if (string.IsNullOrWhiteSpace(raw)) return new HoldOptions(DefaultDurationSeconds);

        if (!int.TryParse(raw, out var seconds) || seconds <= 0)
            throw new InvalidOperationException(
                $"HOLD_DURATION_SECONDS must be a positive integer, got '{raw}'.");

        return new HoldOptions(seconds);
    }
}
