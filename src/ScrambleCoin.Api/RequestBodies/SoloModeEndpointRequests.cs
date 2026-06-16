namespace ScrambleCoin.Api.RequestBodies;

public static class SoloModeEndpointRequests
{
    // ── Request bodies ────────────────────────────────────────────────────────
    public sealed record CreateSoloGameRequest(
        Guid BotId,
        string VillainId);
}
