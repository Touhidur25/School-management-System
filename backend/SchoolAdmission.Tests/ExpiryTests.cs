using System.Net;

namespace SchoolAdmission.Tests;

/// <summary>
/// Expiry is tested by moving the test clock, not by sleeping. Nothing calls the API to
/// "trigger" expiry: the seat must come back by itself.
/// </summary>
[Collection(ApiCollection.Name)]
public class ExpiryTests(ApiFactory factory)
{
    private readonly HttpClient _http = factory.CreateClient();
    private ApiClient Api => new(_http);

    private void Wait(int seconds) => factory.Clock.Advance(TimeSpan.FromSeconds(seconds));

    [Fact]
    public async Task Hold_ExpiresOnItsOwn_AndTheSeatIsReleased()
    {
        var classId = await Api.CreateClassAsync(1);
        var holdId = await Api.ApplyOkAsync(classId, ApiClient.NewStudentId());

        Wait(ApiFactory.HoldSeconds - 1);
        Assert.Equal("HELD", (await Api.GetHoldAsync(holdId)).GetProperty("status").GetString());
        Assert.Equal(0, (await Api.GetClassAsync(classId)).GetProperty("availableSeats").GetInt32());

        Wait(2);
        Assert.Equal("EXPIRED", (await Api.GetHoldAsync(holdId)).GetProperty("status").GetString());
        var cls = await Api.GetClassAsync(classId);
        Assert.Equal(0, cls.GetProperty("heldSeats").GetInt32());
        Assert.Equal(1, cls.GetProperty("availableSeats").GetInt32());
    }

    [Fact]
    public async Task ExpiredSeat_CanBeTakenByAnotherStudent()
    {
        var classId = await Api.CreateClassAsync(1);
        await Api.ApplyOkAsync(classId, ApiClient.NewStudentId());
        Assert.Equal(HttpStatusCode.Conflict, (await Api.ApplyAsync(classId, ApiClient.NewStudentId())).StatusCode);

        Wait(ApiFactory.HoldSeconds + 1);

        await Api.ApplyOkAsync(classId, ApiClient.NewStudentId());
    }

    [Fact]
    public async Task Confirm_AfterExpiry_Returns409_AndDoesNotTakeTheSeat()
    {
        var classId = await Api.CreateClassAsync(1);
        var student = ApiClient.NewStudentId();
        var holdId = await Api.ApplyOkAsync(classId, student);
        Wait(ApiFactory.HoldSeconds + 1);

        var response = await Api.ConfirmAsync(holdId, student);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("hold_expired", await ApiClient.ErrorCodeAsync(response));
        Assert.Equal("EXPIRED", (await Api.GetHoldAsync(holdId)).GetProperty("status").GetString());
        var cls = await Api.GetClassAsync(classId);
        Assert.Equal(0, cls.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(1, cls.GetProperty("availableSeats").GetInt32());
    }

    [Fact]
    public async Task Student_CanApplyAgain_AfterTheirOwnHoldExpired()
    {
        var classId = await Api.CreateClassAsync(1);
        var student = ApiClient.NewStudentId();
        var firstHold = await Api.ApplyOkAsync(classId, student);

        Wait(ApiFactory.HoldSeconds + 1);
        var secondHold = await Api.ApplyOkAsync(classId, student);

        Assert.NotEqual(firstHold, secondHold);
        Assert.Equal("EXPIRED", (await Api.GetHoldAsync(firstHold)).GetProperty("status").GetString());
        Assert.Equal("HELD", (await Api.GetHoldAsync(secondHold)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task MyHolds_ShowsExpiredStatus()
    {
        var classId = await Api.CreateClassAsync(5);
        var student = ApiClient.NewStudentId();
        await Api.ApplyOkAsync(classId, student);

        Wait(ApiFactory.HoldSeconds + 1);

        var holds = (await ApiClient.Json(await Api.MyHoldsAsync(student))).EnumerateArray().ToList();
        Assert.Equal("EXPIRED", Assert.Single(holds).GetProperty("status").GetString());
    }

    [Fact]
    public async Task ConfirmedAdmission_NeverExpires()
    {
        var classId = await Api.CreateClassAsync(1);
        var student = ApiClient.NewStudentId();
        var holdId = await Api.ApplyOkAsync(classId, student);
        await Api.ConfirmAsync(holdId, student);

        Wait(24 * 60 * 60);

        Assert.Equal("CONFIRMED", (await Api.GetHoldAsync(holdId)).GetProperty("status").GetString());
        var cls = await Api.GetClassAsync(classId);
        Assert.Equal(1, cls.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(0, cls.GetProperty("availableSeats").GetInt32());
    }
}
