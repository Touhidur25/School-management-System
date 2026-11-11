using System.Net;
using System.Text.Json;

namespace SchoolAdmission.Tests;

/// <summary>
/// Many requests fired at the same moment. Whatever order they run in, seats taken
/// (held + confirmed) must never go above totalSeats.
/// </summary>
[Collection(ApiCollection.Name)]
public class ConcurrencyTests(ApiFactory factory)
{
    private readonly HttpClient _http = factory.CreateClient();
    private ApiClient Api => new(_http);

    private static void AssertSeatsAreConsistent(JsonElement cls)
    {
        var total = cls.GetProperty("totalSeats").GetInt32();
        var held = cls.GetProperty("heldSeats").GetInt32();
        var confirmed = cls.GetProperty("confirmedSeats").GetInt32();
        var available = cls.GetProperty("availableSeats").GetInt32();

        Assert.True(held + confirmed <= total, $"Oversold: held {held} + confirmed {confirmed} > total {total}");
        Assert.Equal(total - held - confirmed, available);
    }

    [Fact]
    public async Task ManySimultaneousApplications_NeverTakeMoreSeatsThanExist()
    {
        const int seats = 5;
        const int applicants = 40;
        var classId = await Api.CreateClassAsync(seats);

        var responses = await Task.WhenAll(Enumerable.Range(0, applicants)
            .Select(_ => Api.ApplyAsync(classId, ApiClient.NewStudentId())));

        Assert.Equal(seats, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(applicants - seats, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));

        var cls = await Api.GetClassAsync(classId);
        AssertSeatsAreConsistent(cls);
        Assert.Equal(seats, cls.GetProperty("heldSeats").GetInt32());
        Assert.Equal(0, cls.GetProperty("availableSeats").GetInt32());
    }

    [Fact]
    public async Task TwoBackendInstancesSharingTheDatabase_NeverTakeMoreSeatsThanExist()
    {
        // A second host = a second, separate app instance (own DI container and DB
        // connections) on the same database, like running two backend processes.
        await using var secondInstance = factory.WithWebHostBuilder(_ => { });
        var apis = new[] { Api, new ApiClient(secondInstance.CreateClient()) };

        const int seats = 8;
        const int applicants = 60;
        var classId = await Api.CreateClassAsync(seats);

        var responses = await Task.WhenAll(Enumerable.Range(0, applicants)
            .Select(i => apis[i % 2].ApplyAsync(classId, ApiClient.NewStudentId())));

        Assert.Equal(seats, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(applicants - seats, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        AssertSeatsAreConsistent(await Api.GetClassAsync(classId));
    }

    [Fact]
    public async Task SameStudentApplyingManyTimesAtOnce_GetsExactlyOneSeat()
    {
        var classId = await Api.CreateClassAsync(20);
        var student = ApiClient.NewStudentId();

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Api.ApplyAsync(classId, student)));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(9, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(1, (await Api.GetClassAsync(classId)).GetProperty("heldSeats").GetInt32());
    }

    [Fact]
    public async Task SimultaneousConfirmsOfTheSameHold_CountOnce()
    {
        var classId = await Api.CreateClassAsync(5);
        var student = ApiClient.NewStudentId();
        var holdId = await Api.ApplyOkAsync(classId, student);

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Api.ConfirmAsync(holdId, student)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var cls = await Api.GetClassAsync(classId);
        Assert.Equal(1, cls.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(0, cls.GetProperty("heldSeats").GetInt32());
        AssertSeatsAreConsistent(cls);
    }

    [Fact]
    public async Task ApplicationsRacingWithCancellationsAndConfirmations_StayWithinTheLimit()
    {
        const int seats = 6;
        var classId = await Api.CreateClassAsync(seats);

        // Fill the class: 3 holds to cancel, 3 to confirm.
        var toCancel = new List<(string HoldId, string Student)>();
        var toConfirm = new List<(string HoldId, string Student)>();
        for (var i = 0; i < 3; i++)
        {
            var s = ApiClient.NewStudentId();
            toCancel.Add((await Api.ApplyOkAsync(classId, s), s));
            var t = ApiClient.NewStudentId();
            toConfirm.Add((await Api.ApplyOkAsync(classId, t), t));
        }

        var cancels = toCancel.Select(h => Api.CancelAsync(h.HoldId, h.Student));
        var confirms = toConfirm.Select(h => Api.ConfirmAsync(h.HoldId, h.Student));
        var newcomers = Enumerable.Range(0, 30).Select(_ => Api.ApplyAsync(classId, ApiClient.NewStudentId()));

        var all = await Task.WhenAll(cancels.Concat(confirms).Concat(newcomers));

        var cancelResults = all.Take(3);
        var confirmResults = all.Skip(3).Take(3);
        var newcomerResults = all.Skip(6);
        Assert.All(cancelResults, r => Assert.Equal(HttpStatusCode.NoContent, r.StatusCode));
        Assert.All(confirmResults, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        var cls = await Api.GetClassAsync(classId);
        AssertSeatsAreConsistent(cls);
        // 3 confirmed stay; the 3 cancelled seats can be taken by at most 3 newcomers.
        Assert.Equal(3, cls.GetProperty("confirmedSeats").GetInt32());
        var accepted = newcomerResults.Count(r => r.StatusCode == HttpStatusCode.Created);
        Assert.InRange(accepted, 0, 3);
        Assert.Equal(accepted, cls.GetProperty("heldSeats").GetInt32());
    }
}
