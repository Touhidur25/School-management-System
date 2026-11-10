using SchoolAdmission.Api.Domain;

namespace SchoolAdmission.Api.Services;

public record CreateClassRequest(string? Name, int? TotalSeats);

public record ClassCreatedResponse(string Id, string Name, int TotalSeats);

public record ClassSummary(
    string Id, string Name, int TotalSeats, int HeldSeats, int ConfirmedSeats, int AvailableSeats);

public record HoldCreatedResponse(
    string HoldId, string ClassId, string StudentId, string Status, DateTime ExpiresAt);

public record HoldStatusResponse(string HoldId, string ClassId, string Status, DateTime ExpiresAt);

public record HoldConfirmedResponse(string HoldId, string Status);

public record MyHoldResponse(
    string HoldId, string ClassId, string ClassName, string Status, DateTime ExpiresAt);

/// <summary>One row of the admin's "all applications" list.</summary>
public record AdminHoldResponse(
    string HoldId, string ClassId, string ClassName, string StudentId, string Status,
    DateTime AppliedAt, DateTime ExpiresAt, DateTime? ConfirmedAt);

public record HoldCancelledResponse(string HoldId, string Status);

public record NotificationResponse(
    string NotificationId, string Type, string HoldId, string ClassId, string ClassName,
    string PreviousStatus, DateTime CreatedAt, DateTime? ReadAt);

public static class HoldStatusExtensions
{
    /// <summary>API representation: "HELD", "CONFIRMED", "EXPIRED", "CANCELLED".</summary>
    public static string ToApi(this HoldStatus status) => status.ToString().ToUpperInvariant();
}
