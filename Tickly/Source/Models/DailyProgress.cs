namespace Tickly.Models;

public class DailyProgress
{
    public DateTime Date { get; set; }
    public double PercentageCompleted { get; set; }

    public DailyProgress(DateTime date, double percentageCompleted)
    {
        Date = date;
        PercentageCompleted = percentageCompleted;
    }
}
