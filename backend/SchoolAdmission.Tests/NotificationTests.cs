using System.Net;
using System.Text.Json;

namespace SchoolAdmission.Tests;

[Collection(ApiCollection.Name)]
public class NotificationTests(ApiFactory factory)
{
    private ApiClient Api => new(factory.CreateClient());

    /// <summary>Makes a student with one admin-cancelled application; returns the student id.</summary>
    private async Task<string> StudentWithCancelledApplicationAsync()
    {
        var classId = await Api.CreateClassAsync(5);
        var student = ApiClient.NewStudentId();
        var holdId = await Api.ApplyOkAsync(classId, student);
        await Api.AdminCancelAsync(holdId);
        return student;
    }

    [Fact]
    public async Task Notifications_AreOnlyVisibleToTheStudentTheyAreFor()
    {
        var student = await StudentWithCancelledApplicationAsync();

        Assert.Single(await Api.NotificationListAsync(student));
        Assert.Empty(await Api.NotificationListAsync(ApiClient.NewStudentId()));
    }

    [Fact]
    public async Task Notifications_WithoutStudentId_Returns400()
    {
        var response = await Api.NotificationsAsync(null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MarkRead_SetsReadAt_AndRepeatIsHarmless()
    {
        var student = await StudentWithCancelledApplicationAsync();
        var id = (await Api.NotificationListAsync(student))[0].GetProperty("notificationId").GetString()!;

        Assert.Equal(HttpStatusCode.NoContent, (await Api.MarkNotificationReadAsync(id, student)).StatusCode);
        var firstReadAt = (await Api.NotificationListAsync(student))[0].GetProperty("readAt").GetDateTimeOffset();
        Assert.Equal(HttpStatusCode.NoContent, (await Api.MarkNotificationReadAsync(id, student)).StatusCode);

        var after = (await Api.NotificationListAsync(student))[0];
        Assert.Equal(firstReadAt, after.GetProperty("readAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task MarkRead_ByAnotherStudent_Returns403_AndLeavesItUnread()
    {
        var student = await StudentWithCancelledApplicationAsync();
        var id = (await Api.NotificationListAsync(student))[0].GetProperty("notificationId").GetString()!;

        var response = await Api.MarkNotificationReadAsync(id, ApiClient.NewStudentId());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await Api.NotificationListAsync(student))[0].GetProperty("readAt").ValueKind);
    }

    [Fact]
    public async Task MarkRead_OfUnknownNotification_Returns404()
    {
        var response = await Api.MarkNotificationReadAsync("n_doesnotexist", ApiClient.NewStudentId());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MarkAllRead_MarksEveryNotificationOfThatStudentOnly()
    {
        var classId = await Api.CreateClassAsync(5);
        var student = ApiClient.NewStudentId();
        for (var i = 0; i < 2; i++)
        {
            var holdId = await Api.ApplyOkAsync(classId, student);
            await Api.AdminCancelAsync(holdId);
        }
        var other = await StudentWithCancelledApplicationAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await Api.MarkAllNotificationsReadAsync(student)).StatusCode);

        var mine = await Api.NotificationListAsync(student);
        Assert.Equal(2, mine.Count);
        Assert.All(mine, n => Assert.NotEqual(JsonValueKind.Null, n.GetProperty("readAt").ValueKind));
        Assert.All(await Api.NotificationListAsync(other),
            n => Assert.Equal(JsonValueKind.Null, n.GetProperty("readAt").ValueKind));
    }
}
