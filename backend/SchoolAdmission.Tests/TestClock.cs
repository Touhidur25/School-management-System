namespace SchoolAdmission.Tests;

/// <summary>
/// A clock the tests control, so expiry can be tested without sleeping.
/// It only moves when a test calls <see cref="Advance"/>.
/// </summary>
public sealed class TestClock : TimeProvider
{
    private readonly object _gate = new();
    private DateTimeOffset _now = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate) return _now;
    }

    public void Advance(TimeSpan by)
    {
        lock (_gate) _now += by;
    }
}
