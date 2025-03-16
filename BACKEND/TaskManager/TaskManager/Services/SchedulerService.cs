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
        private void EnsureDayExists(int dayNumber)
        {
            while (ScheduleDays.Count < dayNumber)
                ScheduleDays.Add(new DaySchedule(ScheduleDays.Count + 1));
        }
        public void InsertTask(TaskItem task)
        {
            if (!task.Divisible)
            {
                // Nem darabolható: keressük az első olyan napot, ahol elfér (szünetek figyelembe véve)
                foreach (var day in ScheduleDays)
                {
                    if (day.RemainingMinutes >= task.TotalMinutes + (day.Assignments.Count > 0 ? DaySchedule.BreakTime : 0))
                    {
                        day.Assignments.Add(new TaskAssignment(task.Name, task.TotalMinutes));
                        return;
                    }
                }
                int newDay = ScheduleDays.Count + 1;
                EnsureDayExists(newDay);
                ScheduleDays[newDay - 1].Assignments.Add(new TaskAssignment(task.Name, task.TotalMinutes));
            }
        }

    }
}
