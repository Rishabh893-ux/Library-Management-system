using LibraryManagement.Api.Options;
using Microsoft.Extensions.Options;

namespace LibraryManagement.Api.Services;

public interface IFineCalculator
{
    DateTime CalculateDueDate(DateTime borrowDate);

    /// <summary>Whole calendar days past the due date as of <paramref name="asOf"/>. Never negative.</summary>
    int DaysOverdue(DateTime dueDate, DateTime asOf);

    /// <summary>Fine for returning (or still holding) a book on <paramref name="asOf"/>.</summary>
    decimal CalculateFine(DateTime dueDate, DateTime asOf);
}

/// <summary>
/// Loan period and late fine rules: a 14-day loan and ₹5 per day late by default (configurable).
/// Days are counted by calendar date (UTC), so a book returned any time on its due date is not late,
/// and one returned the next day is 1 day late.
/// </summary>
public class FineCalculator(IOptions<LoanPolicyOptions> options) : IFineCalculator
{
    private readonly LoanPolicyOptions _policy = options.Value;

    public DateTime CalculateDueDate(DateTime borrowDate) => borrowDate.AddDays(_policy.LoanPeriodDays);

    public int DaysOverdue(DateTime dueDate, DateTime asOf) => Math.Max(0, (asOf.Date - dueDate.Date).Days);

    public decimal CalculateFine(DateTime dueDate, DateTime asOf) => DaysOverdue(dueDate, asOf) * _policy.FinePerDay;
}
