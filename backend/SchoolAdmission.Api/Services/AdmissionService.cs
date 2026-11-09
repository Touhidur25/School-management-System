using Microsoft.EntityFrameworkCore;
using Npgsql;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Domain;
using SchoolAdmission.Api.Infrastructure;

namespace SchoolAdmission.Api.Services;

/// <summary>
/// Admission rules.
///
/// Concurrency: every operation that changes how many seats a class has taken
/// (create hold, confirm, cancel) runs in a transaction that first locks that class's
/// row with SELECT ... FOR UPDATE. Operations on the same class therefore run one at a
/// time, in this process or in another backend process sharing the same database, so
/// held + confirmed can never exceed totalSeats.
///
/// Expiry: a HELD hold whose ExpiresAt has passed no longer occupies a seat. This is
/// evaluated from the clock at read time, so seats free themselves with no API call and
/// no background job. Stale rows are marked EXPIRED the next time the class is touched.
/// </summary>
public class AdmissionService(AppDbContext db, TimeProvider clock, HoldOptions options)
{
    private const int MaxSeatsPerClass = 100_000;

    private DateTime Now() => clock.GetUtcNow().UtcDateTime;

    // ---------------------------------------------------------------- classes

    public async Task<SchoolClass> CreateClassAsync(string? name, int? totalSeats)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new ApiException(400, "invalid_name", "Class name is required.");
        if (trimmed.Length > 200)
            throw new ApiException(400, "invalid_name", "Class name must be 200 characters or fewer.");
        if (totalSeats is null || totalSeats < 1 || totalSeats > MaxSeatsPerClass)
            throw new ApiException(400, "invalid_seats",
                $"Total seats must be a whole number between 1 and {MaxSeatsPerClass}.");

        var cls = new SchoolClass
        {
            Id = IdGenerator.New("c"),
            Name = trimmed,
            TotalSeats = totalSeats.Value,
            CreatedAt = Now()
        };
        db.Classes.Add(cls);
        await db.SaveChangesAsync();
        return cls;
    }

    public async Task<List<ClassSummary>> ListClassesAsync()
    {
        var now = Now();
        var classes = await db.Classes.AsNoTracking()
            .OrderBy(c => c.CreatedAt).ThenBy(c => c.Id)
            .ToListAsync();
        var counts = await CountSeatsAsync(null, now);

        return classes.Select(c => Summarize(c, counts)).ToList();
    }

    public async Task<ClassSummary> GetClassAsync(string classId)
    {
        var cls = await db.Classes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == classId)
            ?? throw ClassNotFound();
        var counts = await CountSeatsAsync(classId, Now());
        return Summarize(cls, counts);
    }

    private static ClassSummary Summarize(SchoolClass c, Dictionary<(string ClassId, HoldStatus Status), int> counts)
    {
        counts.TryGetValue((c.Id, HoldStatus.Held), out var held);
        counts.TryGetValue((c.Id, HoldStatus.Confirmed), out var confirmed);
        return new ClassSummary(c.Id, c.Name, c.TotalSeats, held, confirmed,
            Math.Max(0, c.TotalSeats - held - confirmed));
    }

    /// <summary>Seats currently taken: CONFIRMED plus HELD that has not expired.</summary>
    private async Task<Dictionary<(string ClassId, HoldStatus Status), int>> CountSeatsAsync(
        string? classId, DateTime now)
    {
        var rows = await db.Holds.AsNoTracking()
            .Where(h => (classId == null || h.ClassId == classId)
                        && (h.Status == HoldStatus.Confirmed
                            || (h.Status == HoldStatus.Held && h.ExpiresAt > now)))
            .GroupBy(h => new { h.ClassId, h.Status })
            .Select(g => new { g.Key.ClassId, g.Key.Status, Count = g.Count() })
            .ToListAsync();

        return rows.ToDictionary(r => (r.ClassId, r.Status), r => r.Count);
    }

    // ------------------------------------------------------------------ holds

    public async Task<Hold> CreateHoldAsync(string classId, string studentId)
    {
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync();

            var cls = await LockClassAsync(classId);
            var now = Now();
            await ExpireStaleHoldsAsync(classId, now);

            // After the sweep above, any HELD row is still within its time limit.
            var alreadyApplied = await db.Holds.AnyAsync(h =>
                h.ClassId == classId && h.StudentId == studentId
                && (h.Status == HoldStatus.Held || h.Status == HoldStatus.Confirmed));
            if (alreadyApplied)
                throw new ApiException(409, "already_applied",
                    "You already have an active application or confirmed admission in this class.");

            var taken = await db.Holds.CountAsync(h =>
                h.ClassId == classId
                && (h.Status == HoldStatus.Held || h.Status == HoldStatus.Confirmed));
            if (taken >= cls.TotalSeats)
                throw new ApiException(409, "no_seats", "No seats are available in this class.");

            var hold = new Hold
            {
                Id = IdGenerator.New("h"),
                ClassId = classId,
                StudentId = studentId,
                Status = HoldStatus.Held,
                CreatedAt = now,
                ExpiresAt = CeilToSecond(now.AddSeconds(options.DurationSeconds))
            };
            db.Holds.Add(hold);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return hold;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // The partial unique index caught a duplicate that the checks above missed.
            throw new ApiException(409, "already_applied",
                "You already have an active application or confirmed admission in this class.");
        }
    }

    public async Task<HoldStatusResponse> GetHoldAsync(string holdId)
    {
        var hold = await db.Holds.AsNoTracking().FirstOrDefaultAsync(h => h.Id == holdId)
            ?? throw HoldNotFound();
        return new HoldStatusResponse(hold.Id, hold.ClassId, EffectiveStatus(hold, Now()).ToApi(), hold.ExpiresAt);
    }

    /// <summary>Confirms the hold. Confirming an already confirmed hold is a no-op success.</summary>
    public async Task<Hold> ConfirmAsync(string holdId, string studentId)
    {
        var classId = await FindClassIdOfHoldAsync(holdId);
        ApiException? failure = null;
        Hold hold;

        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            await LockClassAsync(classId);
            hold = await db.Holds.FirstOrDefaultAsync(h => h.Id == holdId) ?? throw HoldNotFound();
            EnsureOwner(hold, studentId);

            var now = Now();
            switch (hold.Status)
            {
                case HoldStatus.Confirmed:
                    break; // repeat confirm: nothing changes, so confirmedSeats counts it once
                case HoldStatus.Held when hold.ExpiresAt > now:
                    hold.Status = HoldStatus.Confirmed;
                    hold.ConfirmedAt = now;
                    break;
                case HoldStatus.Held:
                    hold.Status = HoldStatus.Expired;
                    failure = HoldExpired();
                    break;
                case HoldStatus.Expired:
                    failure = HoldExpired();
                    break;
                case HoldStatus.Cancelled:
                    failure = new ApiException(409, "hold_cancelled", "This application was cancelled.");
                    break;
            }

            await db.SaveChangesAsync(); // also persists HELD -> EXPIRED when detected here
            await tx.CommitAsync();
        }

        if (failure is not null) throw failure;
        return hold;
    }

    /// <summary>Cancels a HELD hold. Repeat cancel is a no-op success.</summary>
    public async Task CancelAsync(string holdId, string studentId)
    {
        var classId = await FindClassIdOfHoldAsync(holdId);
        ApiException? failure = null;

        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            await LockClassAsync(classId);
            var hold = await db.Holds.FirstOrDefaultAsync(h => h.Id == holdId) ?? throw HoldNotFound();
            EnsureOwner(hold, studentId);

            var now = Now();
            switch (hold.Status)
            {
                case HoldStatus.Cancelled:
                    break;
                case HoldStatus.Held when hold.ExpiresAt > now:
                    hold.Status = HoldStatus.Cancelled;
                    break;
                case HoldStatus.Held:
                    hold.Status = HoldStatus.Expired;
                    failure = HoldExpired();
                    break;
                case HoldStatus.Expired:
                    failure = HoldExpired();
                    break;
                case HoldStatus.Confirmed:
                    failure = new ApiException(409, "already_confirmed",
                        "This admission is already confirmed and cannot be cancelled here.");
                    break;
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }

        if (failure is not null) throw failure;
    }

    public async Task<List<MyHoldResponse>> ListMyHoldsAsync(string studentId)
    {
        var now = Now();
        var rows = await (
                from h in db.Holds.AsNoTracking()
                join c in db.Classes.AsNoTracking() on h.ClassId equals c.Id
                where h.StudentId == studentId
                orderby h.CreatedAt descending, h.Id
                select new { h.Id, h.ClassId, ClassName = c.Name, h.Status, h.ExpiresAt, h.CreatedAt })
            .ToListAsync();

        return rows.Select(r => new MyHoldResponse(
            r.Id, r.ClassId, r.ClassName,
            (r.Status == HoldStatus.Held && r.ExpiresAt <= now ? HoldStatus.Expired : r.Status).ToApi(),
            r.ExpiresAt)).ToList();
    }

    // --------------------------------------------------------------- helpers

    /// <summary>
    /// Takes a row lock on the class (released at commit/rollback) and returns it.
    /// Must be called inside a transaction.
    /// </summary>
    private async Task<SchoolClass> LockClassAsync(string classId)
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM classes WHERE id = {classId} FOR UPDATE");

        return await db.Classes.FirstOrDefaultAsync(c => c.Id == classId) ?? throw ClassNotFound();
    }

    private async Task<string> FindClassIdOfHoldAsync(string holdId) =>
        await db.Holds.AsNoTracking().Where(h => h.Id == holdId).Select(h => h.ClassId).FirstOrDefaultAsync()
        ?? throw HoldNotFound();

    private Task<int> ExpireStaleHoldsAsync(string classId, DateTime now) =>
        db.Holds
            .Where(h => h.ClassId == classId && h.Status == HoldStatus.Held && h.ExpiresAt <= now)
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.Status, HoldStatus.Expired));

    private static HoldStatus EffectiveStatus(Hold hold, DateTime now) =>
        hold.Status == HoldStatus.Held && hold.ExpiresAt <= now ? HoldStatus.Expired : hold.Status;

    private static void EnsureOwner(Hold hold, string studentId)
    {
        if (hold.StudentId != studentId)
            throw new ApiException(403, "not_your_hold", "This application belongs to another student.");
    }

    /// <summary>Whole seconds, rounded up, so a hold lasts at least the configured duration.</summary>
    private static DateTime CeilToSecond(DateTime value)
    {
        var ticks = value.Ticks;
        var remainder = ticks % TimeSpan.TicksPerSecond;
        if (remainder != 0) ticks += TimeSpan.TicksPerSecond - remainder;
        return new DateTime(ticks, DateTimeKind.Utc);
    }

    private static ApiException ClassNotFound() =>
        new(404, "class_not_found", "Class not found.");

    private static ApiException HoldNotFound() =>
        new(404, "hold_not_found", "Application not found.");

    private static ApiException HoldExpired() =>
        new(409, "hold_expired", "This application has expired and the seat was released.");
}
