namespace SchoolAdmission.Api.Domain;

public static class NotificationTypes
{
    /// <summary>The school cancelled a student's application or confirmed admission.</summary>
    public const string AdmissionCancelled = "ADMISSION_CANCELLED";
}

/// <summary>
/// A message waiting for a student. There is no login, so it is addressed to the
/// student's birth registration number and shown the next time (or while) the student
/// has the app open.
/// </summary>
public class Notification
{
    public string Id { get; set; } = default!;
    public string StudentId { get; set; } = default!;
    public string HoldId { get; set; } = default!;
    public string Type { get; set; } = default!;

    /// <summary>HELD or CONFIRMED: what the application was before it was cancelled.</summary>
    public string PreviousStatus { get; set; } = default!;

    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }

    public Hold? Hold { get; set; }
}
