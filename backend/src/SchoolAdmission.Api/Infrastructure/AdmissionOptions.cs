namespace SchoolAdmission.Api.Infrastructure;

public class AdmissionOptions
{
    public const int DefaultHoldDurationSeconds = 600;

    public TimeSpan HoldDuration { get; init; } = TimeSpan.FromSeconds(DefaultHoldDurationSeconds);

    /// <summary>Reads HOLD_DURATION_SECONDS; fails fast on a value that is not a positive whole number.</summary>
    public static AdmissionOptions FromConfiguration(IConfiguration config)
    {
        var raw = config["HOLD_DURATION_SECONDS"];
        if (string.IsNullOrWhiteSpace(raw))
            return new AdmissionOptions();

        if (!int.TryParse(raw.Trim(), out var seconds) || seconds <= 0)
            throw new InvalidOperationException(
                $"HOLD_DURATION_SECONDS must be a positive whole number of seconds, but was '{raw}'.");

        return new AdmissionOptions { HoldDuration = TimeSpan.FromSeconds(seconds) };
    }
}
