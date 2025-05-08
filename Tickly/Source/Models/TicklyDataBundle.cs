using System.Collections.Generic;
// Removed: using Tickly.Services; as AppSettings class is static and its properties will be directly included.

namespace Tickly.Models
{
    public class TicklyDataBundle
    {
        public List<TaskItem>? Tasks { get; set; }
        public CalendarSystemType SelectedCalendarSystem { get; set; }
        public ThemeType SelectedTheme { get; set; }
        public List<DailyProgress>? Progress { get; set; }
    }
}
