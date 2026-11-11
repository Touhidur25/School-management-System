namespace SchoolAdmission.Tests;

[Collection(ApiCollection.Name)]
public class AdminTests(ApiFactory factory)
{
    private ApiClient Api => new(factory.CreateClient());

    [Fact]
    public async Task AdminList_ShowsEveryApplicationWithStudentStatusAndTime_IncludingExpiredAndCancelled()
    {
        var classId = await Api.CreateClassAsync(10, "Class 8 - Day Shift");
        var expiredStudent = ApiClient.NewStudentId();
        await Api.ApplyOkAsync(classId, expiredStudent);
        factory.Clock.Advance(TimeSpan.FromSeconds(ApiFactory.HoldSeconds + 1)); // first one runs out of time

        var confirmedStudent = ApiClient.NewStudentId();
        var cancelledStudent = ApiClient.NewStudentId();
        var heldStudent = ApiClient.NewStudentId();
        var confirmedHold = await Api.ApplyOkAsync(classId, confirmedStudent);
        var cancelledHold = await Api.ApplyOkAsync(classId, cancelledStudent);
        await Api.ApplyOkAsync(classId, heldStudent);
        await Api.ConfirmAsync(confirmedHold, confirmedStudent);
        await Api.CancelAsync(cancelledHold, cancelledStudent);

        var rows = await Api.AdminHoldsAsync(classId);

        Assert.Equal(4, rows.Count);
        string StatusOf(string student) =>
            rows.Single(r => r.GetProperty("studentId").GetString() == student).GetProperty("status").GetString()!;
        Assert.Equal("EXPIRED", StatusOf(expiredStudent));
        Assert.Equal("CONFIRMED", StatusOf(confirmedStudent));
        Assert.Equal("CANCELLED", StatusOf(cancelledStudent));
        Assert.Equal("HELD", StatusOf(heldStudent));

        Assert.All(rows, r =>
        {
            Assert.Equal(classId, r.GetProperty("classId").GetString());
            Assert.Equal("Class 8 - Day Shift", r.GetProperty("className").GetString());
            Assert.StartsWith("h_", r.GetProperty("holdId").GetString());
            Assert.True(r.GetProperty("appliedAt").GetDateTimeOffset().Year >= 2024);
        });

        var confirmedRow = rows.Single(r => r.GetProperty("studentId").GetString() == confirmedStudent);
        Assert.NotEqual(System.Text.Json.JsonValueKind.Null, confirmedRow.GetProperty("confirmedAt").ValueKind);
    }

    [Fact]
    public async Task AdminList_CanBeFilteredByClass_AndNeedsNoStudentHeader()
    {
        var first = await Api.CreateClassAsync(5, "Class 9 - Morning Shift");
        var second = await Api.CreateClassAsync(5, "Class 9 - Day Shift");
        var inFirst = ApiClient.NewStudentId();
        var inSecond = ApiClient.NewStudentId();
        await Api.ApplyOkAsync(first, inFirst);
        await Api.ApplyOkAsync(second, inSecond);

        var onlyFirst = await Api.AdminHoldsAsync(first);
        var everything = await Api.AdminHoldsAsync();

        Assert.Equal(inFirst, Assert.Single(onlyFirst).GetProperty("studentId").GetString());
        var students = everything.Select(r => r.GetProperty("studentId").GetString()).ToList();
        Assert.Contains(inFirst, students);
        Assert.Contains(inSecond, students);
    }
}
