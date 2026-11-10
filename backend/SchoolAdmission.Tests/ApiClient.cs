using System.Net.Http.Json;
using System.Text.Json;

namespace SchoolAdmission.Tests;

/// <summary>Small helper that speaks the HTTP contract, so tests read like scenarios.</summary>
public class ApiClient(HttpClient http)
{
    private static long _studentCounter = Random.Shared.NextInt64(1_000_000_000_000);

    /// <summary>A unique, valid 17-digit student id.</summary>
    public static string NewStudentId() =>
        "2015" + Interlocked.Increment(ref _studentCounter).ToString("D13");

    public async Task<string> CreateClassAsync(int totalSeats, string name = "Test Class")
    {
        var response = await http.PostAsJsonAsync("/classes", new { name, totalSeats });
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        return (await Json(response)).GetProperty("id").GetString()!;
    }

    public Task<HttpResponseMessage> PostClassAsync(object body) => http.PostAsJsonAsync("/classes", body);

    public Task<HttpResponseMessage> ApplyAsync(string classId, string? studentId) =>
        SendAsync(HttpMethod.Post, $"/classes/{classId}/holds", studentId);

    public Task<HttpResponseMessage> ConfirmAsync(string holdId, string? studentId) =>
        SendAsync(HttpMethod.Post, $"/holds/{holdId}/confirm", studentId);

    public Task<HttpResponseMessage> CancelAsync(string holdId, string? studentId) =>
        SendAsync(HttpMethod.Delete, $"/holds/{holdId}", studentId);

    public Task<HttpResponseMessage> MyHoldsAsync(string? studentId) =>
        SendAsync(HttpMethod.Get, "/me/holds", studentId);

    public async Task<JsonElement> GetClassAsync(string classId) =>
        await Json(await http.GetAsync($"/classes/{classId}"));

    public async Task<JsonElement> GetHoldAsync(string holdId) =>
        await Json(await http.GetAsync($"/holds/{holdId}"));

    public async Task<List<JsonElement>> AdminHoldsAsync(string? classId = null)
    {
        var url = classId is null ? "/admin/holds" : $"/admin/holds?classId={Uri.EscapeDataString(classId)}";
        var response = await http.GetAsync(url);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        return (await Json(response)).EnumerateArray().ToList();
    }

    public Task<HttpResponseMessage> AdminCancelAsync(string holdId) =>
        SendAsync(HttpMethod.Post, $"/admin/holds/{holdId}/cancel", null);

    public Task<HttpResponseMessage> NotificationsAsync(string? studentId) =>
        SendAsync(HttpMethod.Get, "/me/notifications", studentId);

    public Task<HttpResponseMessage> MarkNotificationReadAsync(string notificationId, string? studentId) =>
        SendAsync(HttpMethod.Post, $"/me/notifications/{notificationId}/read", studentId);

    public Task<HttpResponseMessage> MarkAllNotificationsReadAsync(string? studentId) =>
        SendAsync(HttpMethod.Post, "/me/notifications/read-all", studentId);

    /// <summary>The student's notifications; asserts the call succeeded.</summary>
    public async Task<List<JsonElement>> NotificationListAsync(string studentId)
    {
        var response = await NotificationsAsync(studentId);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        return (await Json(response)).EnumerateArray().ToList();
    }

    /// <summary>Applies and asserts success; returns the hold id.</summary>
    public async Task<string> ApplyOkAsync(string classId, string studentId)
    {
        var response = await ApplyAsync(classId, studentId);
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        return (await Json(response)).GetProperty("holdId").GetString()!;
    }

    public static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    public static async Task<string?> ErrorCodeAsync(HttpResponseMessage response) =>
        (await Json(response)).GetProperty("error").GetProperty("code").GetString();

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string? studentId)
    {
        var request = new HttpRequestMessage(method, url);
        if (studentId is not null) request.Headers.Add("X-Student-Id", studentId);
        return http.SendAsync(request);
    }
}
