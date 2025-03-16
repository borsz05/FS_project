using TaskManager.Models;

namespace TaskManager.Services
{
    public class SchedulerService:ISchedulerService
    {
        public List<DaySchedule> ScheduleDays { get; set; } = new List<DaySchedule>();

        public SchedulerService()
        {
            // Kezdjük az első nappal
            ScheduleDays.Add(new DaySchedule(1));
        }

    }
}
