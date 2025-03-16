namespace TaskManager.Models
{
    public class TaskAssignment
    {
        public string TaskName { get; set; }
        public int Minutes { get; set; }
        public bool IsDivisible { get; set; }
        public int TaskStartDay { get; set; }
        public int TaskAvailableDays { get; set; }

        public TaskAssignment(string taskName, int minutes)
        {
            TaskName = taskName;
            Minutes = minutes;
            IsDivisible = false;
        }

        public TaskAssignment(string taskName, int minutes, int taskStartDay, int taskAvailableDays)
        {
            TaskName = taskName;
            Minutes = minutes;
            IsDivisible = true;
            TaskStartDay = taskStartDay;
            TaskAvailableDays = taskAvailableDays;
        }
    }
}
