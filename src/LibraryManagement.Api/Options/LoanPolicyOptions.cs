using System.ComponentModel.DataAnnotations;

namespace LibraryManagement.Api.Options;

public class LoanPolicyOptions
{
    public const string SectionName = "LoanPolicy";

    [Range(1, 365)] public int LoanPeriodDays { get; set; } = 14;

    /// <summary>Fine in rupees for each full day a book is returned after its due date.</summary>
    [Range(0, 10_000)] public decimal FinePerDay { get; set; } = 5m;
}
