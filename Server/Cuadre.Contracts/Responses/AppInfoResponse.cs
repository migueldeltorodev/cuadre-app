namespace Cuadre.Contracts.Responses;

/// <summary>Smoke endpoint: proves the frontend reaches the API end to end.</summary>
public class AppInfoResponse
{
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required string Environment { get; init; }
}
