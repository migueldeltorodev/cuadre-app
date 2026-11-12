namespace Cuadre.Application.Models;

/// <summary>
/// Stored as TEXT. The distinction drives the Profit First split (RN-10, D-11):
/// bank money is "stuck" and goes to Reposición; cash is prioritized for the other envelopes.
/// </summary>
public enum CashBoxKind
{
    Cash,
    Bank
}
