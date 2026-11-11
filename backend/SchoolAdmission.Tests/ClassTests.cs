using System.Net;

namespace SchoolAdmission.Tests;

[Collection(ApiCollection.Name)]
public class ClassTests(ApiFactory factory)
{
    private readonly HttpClient _http = factory.CreateClient();
    private ApiClient Api => new(_http);

    [Fact]
    public async Task CreateClass_ReturnsContractFields()
    {
        var response = await Api.PostClassAsync(new { name = "Class 6 - Morning Shift", totalSeats = 120 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ApiClient.Json(response);
        Assert.StartsWith("c_", body.GetProperty("id").GetString());
        Assert.Equal("Class 6 - Morning Shift", body.GetProperty("name").GetString());
        Assert.Equal(120, body.GetProperty("totalSeats").GetInt32());
    }

    [Theory]
    [InlineData("", 10)]
    [InlineData("   ", 10)]
    [InlineData("Class 6", 0)]
    [InlineData("Class 6", -5)]
    public async Task CreateClass_WithInvalidInput_Returns400(string name, int totalSeats)
    {
        var response = await Api.PostClassAsync(new { name, totalSeats });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateClass_WithoutTotalSeats_Returns400()
    {
        var response = await Api.PostClassAsync(new { name = "Class 6" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_seats", await ApiClient.ErrorCodeAsync(response));
    }

    [Fact]
    public async Task ListClasses_ShowsHeldConfirmedAndAvailableSeats()
    {
        var classId = await Api.CreateClassAsync(10);
        await Api.ApplyOkAsync(classId, ApiClient.NewStudentId()); // stays HELD
        await Api.ApplyOkAsync(classId, ApiClient.NewStudentId()); // stays HELD

        var payingStudent = ApiClient.NewStudentId();
        var holdId = await Api.ApplyOkAsync(classId, payingStudent);
        Assert.Equal(HttpStatusCode.OK, (await Api.ConfirmAsync(holdId, payingStudent)).StatusCode);

        var list = await ApiClient.Json(await _http.GetAsync("/classes"));
        var entry = list.EnumerateArray().Single(e => e.GetProperty("id").GetString() == classId);

        Assert.Equal(10, entry.GetProperty("totalSeats").GetInt32());
        Assert.Equal(2, entry.GetProperty("heldSeats").GetInt32());
        Assert.Equal(1, entry.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(7, entry.GetProperty("availableSeats").GetInt32());
    }

    [Fact]
    public async Task GetClass_Unknown_Returns404()
    {
        var response = await _http.GetAsync("/classes/c_doesnotexist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("class_not_found", await ApiClient.ErrorCodeAsync(response));
    }
}
