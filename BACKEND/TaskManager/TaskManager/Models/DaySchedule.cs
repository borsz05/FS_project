namespace TaskManager.Models
{
    public class DaySchedule
    {
        public int DayNumber { get; set; }
        public List<TaskAssignment> Assignments { get; set; } = new List<TaskAssignment>();

        public const int Capacity = 600;
        public const int BreakTime = 15;

        public int TotalMinutes => Assignments.Where(a => a.Minutes > 0).Sum(a => a.Minutes);

        public int EffectiveLoad => TotalMinutes + ((Assignments.Count > 0 ? Assignments.Count - 1 : 0) * BreakTime);

        public int RemainingMinutes => Capacity - EffectiveLoad;

        public DaySchedule(int dayNumber)
        {
            DayNumber = dayNumber;
        }
    }
}
