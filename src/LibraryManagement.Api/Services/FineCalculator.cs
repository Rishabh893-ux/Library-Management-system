namespace LibraryManagement.Api.Services;

/// <summary>
/// Loan period and late fine rules: a 14-day loan and ₹5 per day late.
/// Days are counted by calendar date (UTC), so a book returned any time on its due date is not late,
/// and one returned the next day is 1 day late.
/// </summary>
public static class FineCalculator
{
    public const int LoanPeriodDays = 14;
    public const decimal FinePerDay = 5m;

    public static DateTime CalculateDueDate(DateTime borrowDate) => borrowDate.AddDays(LoanPeriodDays);

    /// <summary>Whole calendar days past the due date as of <paramref name="asOf"/>. Never negative.</summary>
    public static int DaysOverdue(DateTime dueDate, DateTime asOf) => Math.Max(0, (asOf.Date - dueDate.Date).Days);

    /// <summary>Fine for returning (or still holding) a book on <paramref name="asOf"/>.</summary>
    public static decimal CalculateFine(DateTime dueDate, DateTime asOf) => DaysOverdue(dueDate, asOf) * FinePerDay;
}
