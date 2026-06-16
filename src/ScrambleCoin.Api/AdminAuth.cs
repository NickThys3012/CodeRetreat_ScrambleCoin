namespace ScrambleCoin.Api;

/// <summary>
/// Shared admin-key authentication helpers used by all admin-protected endpoints.
/// Centralising the key and helpers here ensures a single place to update on key rotation.
/// </summary>
internal static class AdminAuth
{
    /// <summary>The admin key value expected in the <c>X-Admin-Key</c> header.</summary>
    internal const string Key = "scramblecoin-admin";

    /// <summary>Returns <c>true</c> when the request carries the correct admin key.</summary>
    internal static bool IsValid(HttpRequest request) =>
        request.Headers.TryGetValue("X-Admin-Key", out var key) && key == Key;

    /// <summary>Returns a 401 Unauthorized <see cref="IResult"/> for missing/invalid admin keys.</summary>
    internal static IResult Unauthorized() =>
        Results.Problem(
            detail: "Missing or invalid X-Admin-Key header.",
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Unauthorized");
}
