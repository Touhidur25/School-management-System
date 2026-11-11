namespace SchoolAdmission.Api.Domain;

public enum HoldStatus
{
    Held,
    Confirmed,
    Expired,
    Cancelled
}

/// <summary>
/// A student's application for a seat. Occupies a seat while HELD (and not past
/// ExpiresAt) or CONFIRMED.
/// </summary>
public class Hold
{
    public string Id { get; set; } = default!;
    public string ClassId { get; set; } = default!;
    public string StudentId { get; set; } = default!;
    public HoldStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }

    public SchoolClass? Class { get; set; }
}
