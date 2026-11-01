using System.Globalization;

namespace Qlcheck.Tests;

// Today's date as a suppression comment writes it (checked-on:YYYY-MM-DD).
internal static class TestDates
{
    public static string Today => DateOnly.FromDateTime(DateTime.UtcNow).ToString("O", CultureInfo.InvariantCulture);
}
