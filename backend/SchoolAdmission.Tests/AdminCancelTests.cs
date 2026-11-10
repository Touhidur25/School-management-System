using System.Net;
using System.Text.Json;

namespace SchoolAdmission.Tests;

/// <summary>The school can cancel an application or a confirmed admission; the student is notified.</summary>
[Collection(ApiCollection.Name)]
public class AdminCancelTests(ApiFactory factory)
{
    private ApiClient Api => new(factory.CreateClient());

    private async Task<string> StatusOfAsync(string holdId) =>
        (await Api.GetHoldAsync(holdId)).GetProperty("status").GetString()!;

    [Fact]
    public async Task AdminCancel_OfConfirmedAdmission_FreesTheSeat_AndNotifiesTheStudent()
    {
        var classId = await Api.CreateClassAsync(1, "Class 10 - Morning Shift");
        var student = ApiClient.NewStudentId();
        var holdId = await Api.ApplyOkAsync(classId, student);
        await Api.ConfirmAsync(holdId, student);

        var response = await Api.AdminCancelAsync(holdId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ApiClient.Json(response);
        Assert.Equal(holdId, body.GetProperty("holdId").GetString());
        Assert.Equal("CANCELLED", body.GetProperty("status").GetString());
        Assert.Equal("CANCELLED", await StatusOfAsync(holdId));

        var cls = await Api.GetClassAsync(classId);
        Assert.Equal(0, cls.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(1, cls.GetProperty("availableSeats").GetInt32());

        var notification = Assert.Single(await Api.NotificationListAsync(student));
        Assert.StartsWith("n_", notification.GetProperty("notificationId").GetString());
        Assert.Equal("ADMISSION_CANCELLED", notification.GetProperty("type").GetString());
        Assert.Equal(holdId, notification.GetProperty("holdId").GetString());
        Assert.Equal(classId, notification.GetProperty("classId").GetString());
        Assert.Equal("Class 10 - Morning Shift", notification.GetProperty("className").GetString());
        Assert.Equal("CONFIRMED", notification.GetProperty("previousStatus").GetString());
        Assert.Equal(JsonValueKind.Null, notification.GetProperty("readAt").ValueKind);

        // The freed seat can go to someone else.
        await Api.ApplyOkAsync(classId, ApiClient.NewStudentId());
    }

    [Fact]
    public async Task AdminCancel_OfHeldApplication_NotifiesWithPreviousStatusHeld()
    {
        var classId = await Api.CreateClassAsync(3);
        var student = ApiClient.NewStudentId();
        var holdId = await Api.ApplyOkAsync(classId, student);

        Assert.Equal(HttpStatusCode.OK, (await Api.AdminCancelAsync(holdId)).StatusCode);

        var notification = Assert.Single(await Api.NotificationListAsync(student));
        Assert.Equal("HELD", notification.GetProperty("previousStatus").GetString());
        Assert.Equal("CANCELLED", await StatusOfAsync(holdId));
    }

    [Fact]
    public async Task AdminCancel_Twice_IsHarmless_AndNotifiesOnlyOnce()
    {
        var classId = await Api.CreateClassAsync(3);
        var student = ApiClient.NewStudentId();
        var holdId = await Api.ApplyOkAsync(classId, student);

        Assert.Equal(HttpStatusCode.OK, (await Api.AdminCancelAsync(holdId)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Api.AdminCancelAsync(holdId)).StatusCode);

        Assert.Single(await Api.NotificationListAsync(student));
    }

    [Fact]
    public async Task AdminCancel_OfExpiredApplication_Returns409_AndSendsNoNotification()
    {
        var classId = await Api.CreateClassAsync(3);
        var student = ApiClient.NewStudentId();
        var holdId = await Api.ApplyOkAsync(classId, student);
        factory.Clock.Advance(TimeSpan.FromSeconds(ApiFactory.HoldSeconds + 1));

        var response = await Api.AdminCancelAsync(holdId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("hold_expired", await ApiClient.ErrorCodeAsync(response));
        Assert.Empty(await Api.NotificationListAsync(student));
        Assert.Equal("EXPIRED", await StatusOfAsync(holdId));
    }

    [Fact]
    public async Task AdminCancel_OfUnknownHold_Returns404()
    {
        var response = await Api.AdminCancelAsync("h_doesnotexist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task StudentCancellingTheirOwnApplication_SendsNoNotification()
    {
        var classId = await Api.CreateClassAsync(3);
        var student = ApiClient.NewStudentId();
        var holdId = await Api.ApplyOkAsync(classId, student);

        await Api.CancelAsync(holdId, student);

        Assert.Empty(await Api.NotificationListAsync(student));
    }

    [Fact]
    public async Task AfterAdminCancel_TheStudentCanApplyToTheClassAgain()
    {
        var classId = await Api.CreateClassAsync(3);
        var student = ApiClient.NewStudentId();
        var holdId = await Api.ApplyOkAsync(classId, student);
        await Api.AdminCancelAsync(holdId);

        var newHold = await Api.ApplyOkAsync(classId, student);

        Assert.NotEqual(holdId, newHold);
    }

    [Fact]
    public async Task AdminList_ShowsTheCancelledAdmission()
    {
        var classId = await Api.CreateClassAsync(3);
        var student = ApiClient.NewStudentId();
        var holdId = await Api.ApplyOkAsync(classId, student);
        await Api.ConfirmAsync(holdId, student);
        await Api.AdminCancelAsync(holdId);

        var row = Assert.Single(await Api.AdminHoldsAsync(classId));

        Assert.Equal("CANCELLED", row.GetProperty("status").GetString());
        Assert.Equal(student, row.GetProperty("studentId").GetString());
    }

    [Fact]
    public async Task AdminCancelRacingWithConfirm_EndsCancelled_WithTheSeatFreeAndOneNotification()
    {
        var classId = await Api.CreateClassAsync(2);
        var student = ApiClient.NewStudentId();
        var holdId = await Api.ApplyOkAsync(classId, student);

        var results = await Task.WhenAll(Api.ConfirmAsync(holdId, student), Api.AdminCancelAsync(holdId));

        // Whichever ran first, the admin's cancel always succeeds; the confirm either
        // went through just before it or was refused just after it.
        Assert.Equal(HttpStatusCode.OK, results[1].StatusCode);
        Assert.Contains(results[0].StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
        Assert.Equal("CANCELLED", await StatusOfAsync(holdId));
        var cls = await Api.GetClassAsync(classId);
        Assert.Equal(0, cls.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(0, cls.GetProperty("heldSeats").GetInt32());
        Assert.Equal(2, cls.GetProperty("availableSeats").GetInt32());
        Assert.Single(await Api.NotificationListAsync(student));
    }
}
