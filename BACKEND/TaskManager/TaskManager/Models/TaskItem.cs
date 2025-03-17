namespace TaskManager.Models
{
    public class TaskItem
    {
        public string Name { get; set; }
        public int TotalHours { get; set; }
        public int AvailableDays { get; set; }
        public int TotalMinutes => TotalHours * 60;
        public bool Divisible => AvailableDays > 1;

        public TaskItem(string name, int totalHours, int availableDays)
        {
            Name = name;
            TotalHours = totalHours;
            AvailableDays = availableDays;
        }
    }
}
