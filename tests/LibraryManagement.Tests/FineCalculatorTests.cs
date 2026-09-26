using LibraryManagement.Api.Services;

namespace LibraryManagement.Tests;

public class FineCalculatorTests
{
    private static readonly DateTime Due = new(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Due_date_is_14_days_after_borrowing()
    {
        var borrowed = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);

        Assert.Equal(new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc), FineCalculator.CalculateDueDate(borrowed));
    }

    [Theory]
    [InlineData("2026-03-10T09:00:00Z", 0)]   // early
    [InlineData("2026-03-15T23:59:00Z", 0)]   // late in the day, but still on the due date
    [InlineData("2026-03-16T00:01:00Z", 5)]   // first minute of the next day: 1 day late
    [InlineData("2026-03-16T09:00:00Z", 5)]   // less than 24h after the due time, but a new calendar day
    [InlineData("2026-03-25T10:00:00Z", 50)]  // 10 days late
    [InlineData("2026-04-14T10:00:00Z", 150)] // 30 days late
    public void Fine_is_5_rupees_per_calendar_day_after_due_date(string returnedAt, int expectedFine)
    {
        // expectedFine is an int because attribute arguments can't be decimal literals.
        var returned = DateTime.Parse(returnedAt, null, System.Globalization.DateTimeStyles.AdjustToUniversal);

        Assert.Equal((decimal)expectedFine, FineCalculator.CalculateFine(Due, returned));
    }

    [Fact]
    public void Days_overdue_counts_calendar_days_and_is_never_negative()
    {
        Assert.Equal(0, FineCalculator.DaysOverdue(Due, Due.AddDays(-5)));
        Assert.Equal(0, FineCalculator.DaysOverdue(Due, Due.AddHours(13))); // 23:00, still the due date
        Assert.Equal(1, FineCalculator.DaysOverdue(Due, Due.AddHours(15))); // 01:00 the next day
        Assert.Equal(16, FineCalculator.DaysOverdue(Due, Due.AddDays(16)));
    }
}
