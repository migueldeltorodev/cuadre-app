using System.Net;
using System.Net.Http.Json;
using Cuadre.Contracts.Enums;
using Cuadre.Contracts.Requests;
using Cuadre.Contracts.Responses;
using FluentAssertions;

namespace Cuadre.Api.Tests.Integration.Envelopes;

internal static class EnvelopeTestHelpers
{
    public static async Task<EnvelopeOverviewResponse> GetOverviewAsync(this HttpClient client, CancellationToken ct,
        string query = "") =>
        (await client.GetFromJsonAsync<EnvelopeOverviewResponse>(ApiEndpoints.Envelopes.Balances + query, ct))!;

    public static EnvelopeResponse Envelope(this EnvelopeOverviewResponse overview, EnvelopeType role) =>
        overview.Envelopes.Single(x => x.Envelope.Role == role).Envelope;

    /// <summary>The balance of the envelope with that role in that box (0 when absent).</summary>
    public static long Balance(this EnvelopeOverviewResponse overview, EnvelopeType role, Guid cashBoxId) =>
        overview.Envelopes.Single(x => x.Envelope.Role == role).Balances
            .SingleOrDefault(x => x.CashBoxId == cashBoxId)?.AmountCents ?? 0;

    public static long Balance(this EnvelopeOverviewResponse overview, Guid envelopeId, Guid cashBoxId) =>
        overview.Envelopes.Single(x => x.Envelope.Id == envelopeId).Balances
            .SingleOrDefault(x => x.CashBoxId == cashBoxId)?.AmountCents ?? 0;

    public static long BoxTotal(this EnvelopeOverviewResponse overview, Guid cashBoxId) =>
        overview.CashBoxes.Single(x => x.Id == cashBoxId).TotalCents;

    public static OpeningTargetRequest Target(Guid cashBoxId, Guid envelopeId, long amountCents) => new()
    {
        CashBoxId = cashBoxId,
        EnvelopeId = envelopeId,
        AmountCents = amountCents
    };

    public static Task<EnvelopeOperationResponse> OpenAsync(this HttpClient client, DateOnly date, CancellationToken ct,
        params OpeningTargetRequest[] targets) =>
        client.PostEnvelopeOperationAsync(ApiEndpoints.EnvelopeOperations.CreateOpening,
            new CreateEnvelopeOpeningRequest { Date = date, Targets = targets }, ct);

    public static Task<EnvelopeOperationResponse> MoveAsync(this HttpClient client, DateOnly date, Guid cashBoxId,
        Guid fromId, Guid toId, long amountCents, CancellationToken ct) =>
        client.PostEnvelopeOperationAsync(ApiEndpoints.EnvelopeOperations.CreateTransfer,
            new CreateEnvelopeTransferRequest
            {
                Date = date,
                CashBoxId = cashBoxId,
                FromEnvelopeId = fromId,
                ToEnvelopeId = toId,
                AmountCents = amountCents
            }, ct);

    public static Task<EnvelopeOperationResponse> WithdrawAsync(this HttpClient client, DateOnly date,
        Guid cashBoxId, Guid envelopeId, long amountCents, CancellationToken ct) =>
        client.PostEnvelopeOperationAsync(ApiEndpoints.EnvelopeOperations.CreateWithdrawal,
            new CreateEnvelopeWithdrawalRequest
            {
                Date = date,
                CashBoxId = cashBoxId,
                EnvelopeId = envelopeId,
                AmountCents = amountCents
            }, ct);

    public static async Task<EnvelopeOperationResponse> PostEnvelopeOperationAsync<TRequest>(this HttpClient client,
        string route, TRequest request, CancellationToken ct)
    {
        var response = await client.PostAsJsonAsync(route, request, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(ct));
        return (await response.Content.ReadFromJsonAsync<EnvelopeOperationResponse>(ct))!;
    }
}
