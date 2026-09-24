using LibraryManagement.Api.Options;
using LibraryManagement.Api.Services;
using Microsoft.Extensions.Options;

namespace LibraryManagement.Tests;

public class FineCalculatorTests
{
    private readonly FineCalculator _calculator = new(Options.Create(new LoanPolicyOptions()));
    private static readonly DateTime Due = new(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Due_date_is_14_days_after_borrowing()
    {
        var borrowed = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);

        Assert.Equal(new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc), _calculator.CalculateDueDate(borrowed));
    }

    [Theory]
    [InlineData("2026-03-10T09:00:00Z", 0)]   // early
    [InlineData("2026-03-15T23:59:00Z", 0)]   // late in the day, but still on the due date
    [InlineData("2026-03-16T00:01:00Z", 5)]   // first minute of the next day: 1 day late
    [InlineData("2026-03-16T09:00:00Z", 5)]   // less than 24h after the due time, but a new calendar day
    [InlineData("2026-03-25T10:00:00Z", 50)]  // 10 days late
    [InlineData("2026-04-14T10:00:00Z", 150)] // 30 days late
    public void Fine_is_5_rupees_per_calendar_day_after_due_date(string returnedAt, decimal expectedFine)
    {
        var returned = DateTime.Parse(returnedAt, null, System.Globalization.DateTimeStyles.AdjustToUniversal);

        Assert.Equal(expectedFine, _calculator.CalculateFine(Due, returned));
    }

    [Fact]
    public void Fine_rate_and_period_come_from_configuration()
    {
        var custom = new FineCalculator(Options.Create(new LoanPolicyOptions { LoanPeriodDays = 7, FinePerDay = 10m }));
        var borrowed = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var due = custom.CalculateDueDate(borrowed);

        Assert.Equal(borrowed.AddDays(7), due);
        Assert.Equal(30m, custom.CalculateFine(due, due.AddDays(3)));
    }
}
