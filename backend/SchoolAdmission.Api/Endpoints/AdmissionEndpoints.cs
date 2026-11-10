using System.Text.RegularExpressions;
using SchoolAdmission.Api.Infrastructure;
using SchoolAdmission.Api.Services;

namespace SchoolAdmission.Api.Endpoints;

public static partial class AdmissionEndpoints
{
    private const string StudentHeader = "X-Student-Id";

    public static void MapAdmissionEndpoints(this IEndpointRouteBuilder app)
    {
        // 1. Create class
        app.MapPost("/classes", async (CreateClassRequest? body, AdmissionService svc) =>
        {
            var cls = await svc.CreateClassAsync(body?.Name, body?.TotalSeats);
            return Results.Created($"/classes/{cls.Id}",
                new ClassCreatedResponse(cls.Id, cls.Name, cls.TotalSeats));
        });

        // 2. List classes
        app.MapGet("/classes", async (AdmissionService svc) =>
            Results.Ok(await svc.ListClassesAsync()));

        // 3. One class
        app.MapGet("/classes/{classId}", async (string classId, AdmissionService svc) =>
            Results.Ok(await svc.GetClassAsync(classId)));

        // 4. Apply (hold a seat)
        app.MapPost("/classes/{classId}/holds", async (string classId, HttpRequest request, AdmissionService svc) =>
        {
            var studentId = RequireStudentId(request);
            var hold = await svc.CreateHoldAsync(classId, studentId);
            return Results.Created($"/holds/{hold.Id}",
                new HoldCreatedResponse(hold.Id, hold.ClassId, hold.StudentId, hold.Status.ToApi(), hold.ExpiresAt));
        });

        // 5. Hold status
        app.MapGet("/holds/{holdId}", async (string holdId, AdmissionService svc) =>
            Results.Ok(await svc.GetHoldAsync(holdId)));

        // 6. Pay admission fee (confirm)
        app.MapPost("/holds/{holdId}/confirm", async (string holdId, HttpRequest request, AdmissionService svc) =>
        {
            var studentId = RequireStudentId(request);
            var hold = await svc.ConfirmAsync(holdId, studentId);
            return Results.Ok(new HoldConfirmedResponse(hold.Id, hold.Status.ToApi()));
        });

        // 7. Cancel application
        app.MapDelete("/holds/{holdId}", async (string holdId, HttpRequest request, AdmissionService svc) =>
        {
            var studentId = RequireStudentId(request);
            await svc.CancelAsync(holdId, studentId);
            return Results.NoContent();
        });

        // 8. My applications
        app.MapGet("/me/holds", async (HttpRequest request, AdmissionService svc) =>
        {
            var studentId = RequireStudentId(request);
            return Results.Ok(await svc.ListMyHoldsAsync(studentId));
        });

        // Extra (not in the brief's list): all applications for the admin screen.
        // Optional filter: GET /admin/holds?classId=c_123
        app.MapGet("/admin/holds", async (string? classId, AdmissionService svc) =>
            Results.Ok(await svc.ListAllHoldsAsync(classId)));

        // Extra: the school cancels an application or confirmed admission; the student is notified.
        app.MapPost("/admin/holds/{holdId}/cancel", async (string holdId, AdmissionService svc) =>
        {
            await svc.CancelByAdminAsync(holdId);
            return Results.Ok(new HoldCancelledResponse(holdId, "CANCELLED"));
        });

        // Extra: a student's notifications (newest first), and marking them as read.
        app.MapGet("/me/notifications", async (HttpRequest request, AdmissionService svc) =>
            Results.Ok(await svc.ListNotificationsAsync(RequireStudentId(request))));

        app.MapPost("/me/notifications/read-all", async (HttpRequest request, AdmissionService svc) =>
        {
            await svc.MarkAllNotificationsReadAsync(RequireStudentId(request));
            return Results.NoContent();
        });

        app.MapPost("/me/notifications/{notificationId}/read",
            async (string notificationId, HttpRequest request, AdmissionService svc) =>
            {
                await svc.MarkNotificationReadAsync(notificationId, RequireStudentId(request));
                return Results.NoContent();
            });
    }

    private static string RequireStudentId(HttpRequest request)
    {
        var value = request.Headers[StudentHeader].ToString().Trim();
        if (value.Length == 0)
            throw new ApiException(400, "missing_student_id", $"The {StudentHeader} header is required.");
        if (!SeventeenDigits().IsMatch(value))
            throw new ApiException(400, "invalid_student_id", "Student ID must be exactly 17 digits.");
        return value;
    }

    [GeneratedRegex("^[0-9]{17}$")]
    private static partial Regex SeventeenDigits();
}
