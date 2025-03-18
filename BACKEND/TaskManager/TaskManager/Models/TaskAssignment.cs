namespace TaskManager.Models
{
    public class TaskAssignment
    {
        public string TaskId { get; set; }  
        public string TaskName { get; set; }  
        public int Minutes { get; set; }
        public bool IsDivisible { get; set; }
        public int TaskStartDay { get; set; }
        public int TaskAvailableDays { get; set; }

        // Konstruktor nem darabolható feladatokhoz
        public TaskAssignment(string taskId, string taskName, int minutes)
        {
            TaskId = taskId;
            TaskName = taskName;
            Minutes = minutes;
            IsDivisible = false;
        }

        // Konstruktor darabolható feladatokhoz
        public TaskAssignment(string taskId, string taskName, int minutes, int taskStartDay, int taskAvailableDays)
        {
            TaskId = taskId;
            TaskName = taskName;
            Minutes = minutes;
            IsDivisible = true;
            TaskStartDay = taskStartDay;
            TaskAvailableDays = taskAvailableDays;
        }
    }
}
