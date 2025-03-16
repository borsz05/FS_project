namespace TaskManager.Models
{
    public class Scheduler
    {
        public List<DaySchedule> ScheduleDays { get; set; } = new List<DaySchedule>();

        public Scheduler()
        {
            ScheduleDays.Add(new DaySchedule(1));
        }
        public void InsertTask(TaskItem task)
        {
            if (!task.Divisible)
            {
                // Egy nap alatt teljesíthető feladat keresése
                foreach (var day in ScheduleDays)
                {
                    if (day.RemainingMinutes >= task.TotalMinutes + (day.Assignments.Count > 0 ? DaySchedule.BreakTime : 0))
                    {
                        day.Assignments.Add(new TaskAssignment(task.Name, task.TotalMinutes));
                        return;
                    }
                }

                // Ha nincs elég hely, új nap létrehozása
                int newDay = ScheduleDays.Count + 1;
                ScheduleDays.Add(new DaySchedule(newDay));
                ScheduleDays[newDay - 1].Assignments.Add(new TaskAssignment(task.Name, task.TotalMinutes));
            }
            else
            {
                // Darabolható feladat ütemezése
                int assigned = task.TotalMinutes / task.AvailableDays;
                for (int i = 0; i < task.AvailableDays; i++)
                {
                    if (ScheduleDays.Count < i + 1)
                    {
                        ScheduleDays.Add(new DaySchedule(i + 1));
                    }

                    ScheduleDays[i].Assignments.Add(new TaskAssignment(task.Name, assigned, i + 1, task.AvailableDays));
                }
            }
        }
    }
}
