using System.Net;

namespace SchoolAdmission.Tests;

[Collection(ApiCollection.Name)]
public class HoldTests(ApiFactory factory)
{
    private readonly HttpClient _http = factory.CreateClient();
    private ApiClient Api => new(_http);

    [Fact]
    public async Task Apply_ReturnsHeldHoldAndTakesOneSeat()
    {
        var classId = await Api.CreateClassAsync(3);
        var student = ApiClient.NewStudentId();

        var response = await Api.ApplyAsync(classId, student);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var hold = await ApiClient.Json(response);
        Assert.StartsWith("h_", hold.GetProperty("holdId").GetString());
        Assert.Equal(classId, hold.GetProperty("classId").GetString());
        Assert.Equal(student, hold.GetProperty("studentId").GetString());
        Assert.Equal("HELD", hold.GetProperty("status").GetString());

        var expiresAt = hold.GetProperty("expiresAt").GetDateTimeOffset();
        var earliest = factory.Clock.GetUtcNow().AddSeconds(ApiFactory.HoldSeconds);
        Assert.InRange(expiresAt, earliest, earliest.AddSeconds(1));

        var cls = await Api.GetClassAsync(classId);
        Assert.Equal(1, cls.GetProperty("heldSeats").GetInt32());
        Assert.Equal(2, cls.GetProperty("availableSeats").GetInt32());
    }

    [Fact]
    public async Task Apply_WhenNoSeatsLeft_Returns409()
    {
        var classId = await Api.CreateClassAsync(1);
        await Api.ApplyOkAsync(classId, ApiClient.NewStudentId());

        var response = await Api.ApplyAsync(classId, ApiClient.NewStudentId());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("no_seats", await ApiClient.ErrorCodeAsync(response));
    }

    [Fact]
    public async Task Apply_TwiceToTheSameClass_Returns409()
    {
        var classId = await Api.CreateClassAsync(5);
        var student = ApiClient.NewStudentId();
        await Api.ApplyOkAsync(classId, student);

        var response = await Api.ApplyAsync(classId, student);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("already_applied", await ApiClient.ErrorCodeAsync(response));
        Assert.Equal(1, (await Api.GetClassAsync(classId)).GetProperty("heldSeats").GetInt32());
    }

    [Fact]
    public async Task Apply_ToDifferentClasses_IsAllowed()
    {
        var student = ApiClient.NewStudentId();
        var morning = await Api.CreateClassAsync(5, "Class 6 - Morning Shift");
        var day = await Api.CreateClassAsync(5, "Class 6 - Day Shift");

        await Api.ApplyOkAsync(morning, student);
        await Api.ApplyOkAsync(day, student);
    }

    [Theory]
    [InlineData(null, "missing_student_id")]
    [InlineData("123", "invalid_student_id")]
    [InlineData("2015269123456789", "invalid_student_id")]   // 16 digits
    [InlineData("201526912345678901", "invalid_student_id")] // 18 digits
    [InlineData("abcdefghijklmnopq", "invalid_student_id")]  // 17 letters
    public async Task Apply_WithMissingOrInvalidStudentId_Returns400(string? studentId, string expectedCode)
    {
        var classId = await Api.CreateClassAsync(5);

        var response = await Api.ApplyAsync(classId, studentId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(expectedCode, await ApiClient.ErrorCodeAsync(response));
    }

    [Fact]
    public async Task Apply_ToUnknownClass_Returns404()
    {
        var response = await Api.ApplyAsync("c_doesnotexist", ApiClient.NewStudentId());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetHold_ReturnsCurrentStatus()
    {
        var classId = await Api.CreateClassAsync(5);
        var holdId = await Api.ApplyOkAsync(classId, ApiClient.NewStudentId());

        var hold = await Api.GetHoldAsync(holdId);

        Assert.Equal(holdId, hold.GetProperty("holdId").GetString());
        Assert.Equal(classId, hold.GetProperty("classId").GetString());
        Assert.Equal("HELD", hold.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Confirm_ConfirmsSeat_AndRepeatedConfirmCountsOnce()
    {
        var classId = await Api.CreateClassAsync(5);
        var student = ApiClient.NewStudentId();
        var holdId = await Api.ApplyOkAsync(classId, student);

        for (var i = 0; i < 3; i++)
        {
            var response = await Api.ConfirmAsync(holdId, student);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await ApiClient.Json(response);
            Assert.Equal(holdId, body.GetProperty("holdId").GetString());
            Assert.Equal("CONFIRMED", body.GetProperty("status").GetString());
        }

        var cls = await Api.GetClassAsync(classId);
        Assert.Equal(1, cls.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(0, cls.GetProperty("heldSeats").GetInt32());
        Assert.Equal(4, cls.GetProperty("availableSeats").GetInt32());
        Assert.Equal("CONFIRMED", (await Api.GetHoldAsync(holdId)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Confirm_ByAnotherStudent_Returns403_AndChangesNothing()
    {
        var classId = await Api.CreateClassAsync(5);
        var holdId = await Api.ApplyOkAsync(classId, ApiClient.NewStudentId());

        var response = await Api.ConfirmAsync(holdId, ApiClient.NewStudentId());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("HELD", (await Api.GetHoldAsync(holdId)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Confirm_UnknownHold_Returns404()
    {
        var response = await Api.ConfirmAsync("h_doesnotexist", ApiClient.NewStudentId());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_FreesTheSeat_AndStudentCanApplyAgain()
    {
        var classId = await Api.CreateClassAsync(1);
        var student = ApiClient.NewStudentId();
        var holdId = await Api.ApplyOkAsync(classId, student);

        Assert.Equal(HttpStatusCode.NoContent, (await Api.CancelAsync(holdId, student)).StatusCode);
        // Cancelling again is harmless.
        Assert.Equal(HttpStatusCode.NoContent, (await Api.CancelAsync(holdId, student)).StatusCode);

        Assert.Equal("CANCELLED", (await Api.GetHoldAsync(holdId)).GetProperty("status").GetString());
        Assert.Equal(1, (await Api.GetClassAsync(classId)).GetProperty("availableSeats").GetInt32());

        await Api.ApplyOkAsync(classId, student);
    }

    [Fact]
    public async Task Cancel_ByAnotherStudent_Returns403()
    {
        var classId = await Api.CreateClassAsync(5);
        var holdId = await Api.ApplyOkAsync(classId, ApiClient.NewStudentId());

        var response = await Api.CancelAsync(holdId, ApiClient.NewStudentId());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_ConfirmedAdmission_Returns409()
    {
        var classId = await Api.CreateClassAsync(5);
        var student = ApiClient.NewStudentId();
        var holdId = await Api.ApplyOkAsync(classId, student);
        await Api.ConfirmAsync(holdId, student);

        var response = await Api.CancelAsync(holdId, student);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("already_confirmed", await ApiClient.ErrorCodeAsync(response));
        Assert.Equal("CONFIRMED", (await Api.GetHoldAsync(holdId)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Confirm_AfterCancel_Returns409()
    {
        var classId = await Api.CreateClassAsync(5);
        var student = ApiClient.NewStudentId();
        var holdId = await Api.ApplyOkAsync(classId, student);
        await Api.CancelAsync(holdId, student);

        var response = await Api.ConfirmAsync(holdId, student);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("hold_cancelled", await ApiClient.ErrorCodeAsync(response));
    }

    [Fact]
    public async Task MyHolds_ReturnsOnlyThisStudentsApplications()
    {
        var morning = await Api.CreateClassAsync(5, "Class 7 - Morning Shift");
        var day = await Api.CreateClassAsync(5, "Class 7 - Day Shift");
        var me = ApiClient.NewStudentId();
        var someoneElse = ApiClient.NewStudentId();
        var myMorning = await Api.ApplyOkAsync(morning, me);
        var myDay = await Api.ApplyOkAsync(day, me);
        await Api.ApplyOkAsync(morning, someoneElse);

        var response = await Api.MyHoldsAsync(me);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var holds = (await ApiClient.Json(response)).EnumerateArray().ToList();
        Assert.Equal(2, holds.Count);
        Assert.Equal(new[] { myMorning, myDay }.Order(),
            holds.Select(h => h.GetProperty("holdId").GetString()!).Order());
        var morningEntry = holds.Single(h => h.GetProperty("holdId").GetString() == myMorning);
        Assert.Equal("Class 7 - Morning Shift", morningEntry.GetProperty("className").GetString());
        Assert.Equal("HELD", morningEntry.GetProperty("status").GetString());
    }

    [Fact]
    public async Task MyHolds_WithoutStudentId_Returns400()
    {
        var response = await Api.MyHoldsAsync(null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
