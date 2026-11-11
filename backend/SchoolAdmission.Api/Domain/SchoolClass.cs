namespace SchoolAdmission.Api.Domain;

/// <summary>
/// One class-shift (e.g. "Class 6 - Morning Shift") with its own seat limit.
/// </summary>
public class SchoolClass
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public int TotalSeats { get; set; }
    public DateTime CreatedAt { get; set; }
}
