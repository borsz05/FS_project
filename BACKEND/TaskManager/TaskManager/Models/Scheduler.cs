namespace TaskManager.Models
{
    public class Scheduler
    {
        public List<DaySchedule> ScheduleDays { get; set; } = new List<DaySchedule>();

        public Scheduler()
        {
            ScheduleDays.Add(new DaySchedule(1));
        }
    }
}
