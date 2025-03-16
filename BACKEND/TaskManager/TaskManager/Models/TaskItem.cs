namespace TaskManager.Models
{
    public class TaskItem
    {
        public string Name { get; set; }
        public int TotalMinutes { get; set; }
        public int AvailableDays { get; set; }
        public bool Divisible => AvailableDays > 1;

        public TaskItem(string name, int totalHours, int availableDays)
        {
            Name = name;
            TotalMinutes = totalHours * 60;
            AvailableDays = availableDays;
        }
    }
}
