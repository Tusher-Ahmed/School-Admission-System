namespace SchoolAdmission.Api.Infrastructure;

/// <summary>
/// The current student is identified only by the 17-digit birth registration number in X-Student-Id.
/// </summary>
public static class StudentIdentity
{
    public const string HeaderName = "X-Student-Id";
    private const string ItemKey = "StudentId";

    /// <summary>Trims the value and accepts exactly 17 ASCII digits (Bangla digits are converted by the UI).</summary>
    public static bool TryNormalize(string? raw, out string studentId)
    {
        studentId = raw?.Trim() ?? "";
        return studentId.Length == 17 && studentId.All(char.IsAsciiDigit);
    }

    /// <summary>Adds the X-Student-Id check to an endpoint; invalid or missing header → 400 before the handler runs.</summary>
    public static RouteHandlerBuilder RequireStudentId(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            var values = context.HttpContext.Request.Headers[HeaderName];

            if (values.Count == 0 || string.IsNullOrWhiteSpace(values[0]))
                return ApiErrors.BadRequest("MISSING_STUDENT_ID",
                    "Enter the student's 17-digit birth registration number (X-Student-Id header is missing).");

            if (values.Count > 1 || !TryNormalize(values[0], out var studentId))
                return ApiErrors.BadRequest("INVALID_STUDENT_ID",
                    "The birth registration number must be exactly 17 digits.");

            context.HttpContext.Items[ItemKey] = studentId;
            return await next(context);
        });

    /// <summary>The validated student ID; only valid inside endpoints that use RequireStudentId().</summary>
    public static string GetStudentId(this HttpContext context) =>
        context.Items[ItemKey] as string
        ?? throw new InvalidOperationException("RequireStudentId() is missing on this endpoint.");
}
