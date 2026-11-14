using Cuadre.Contracts.Enums;

namespace Cuadre.Contracts.Requests;

/// <summary>From/To are the shop's days, both included. No dates = everything up to today.</summary>
public class GetAuditEventsRequest : PagedRequest
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public AuditEventKindCode? Kind { get; init; }
}
