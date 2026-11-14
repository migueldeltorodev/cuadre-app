using Cuadre.Application.Models;
using Cuadre.Contracts.Enums;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;

namespace Cuadre.Api.Mapping;

/// <summary>RF-DAT-08: the audit log.</summary>
public static partial class ContractMapping
{
    public static GetAuditEventsOptions MapToOptions(this GetAuditEventsRequest request) => new()
    {
        From = request.From,
        To = request.To,
        Kind = request.Kind is { } kind ? (AuditEventKind)kind : null,
        Page = request.Page.GetValueOrDefault(PagedRequest.DefaultPage),
        PageSize = request.PageSize.GetValueOrDefault(PagedRequest.DefaultPageSize)
    };

    public static PagedResponse<AuditEventResponse> MapToResponse(this AuditEventsPage page, int pageNumber,
        int pageSize) => new()
    {
        Items = page.Items.Select(entry => entry.MapToResponse()).ToList(),
        Page = pageNumber,
        PageSize = pageSize,
        Total = page.Total
    };

    public static AuditEventResponse MapToResponse(this AuditEvent entry) => new()
    {
        At = entry.At,
        Kind = (AuditEventKindCode)entry.Kind,
        Subject = (AuditSubjectCode)entry.Subject,
        SubjectId = entry.SubjectId,
        Title = entry.Title,
        Reason = entry.Reason,
        Changes = entry.Changes
            .Select(change => new FieldChangeResponse { Field = change.Field, Before = change.Before, After = change.After })
            .ToList()
    };
}
