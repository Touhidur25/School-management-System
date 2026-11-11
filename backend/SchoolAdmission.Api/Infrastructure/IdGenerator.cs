namespace SchoolAdmission.Api.Infrastructure;

public static class IdGenerator
{
    /// <summary>Short prefixed id such as "c_3f9a1b2c4d5e6f70" or "h_...".</summary>
    public static string New(string prefix)
    {
        var random = Guid.NewGuid().ToString("N").Substring(0, 16);
        return $"{prefix}_{random}";
    }
}
