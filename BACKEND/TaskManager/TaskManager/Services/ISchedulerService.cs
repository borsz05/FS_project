using TaskManager.Models;

namespace TaskManager.Services
{
    public interface ISchedulerService
    {
        List<DaySchedule> ScheduleDays { get; set; }
        void AddTask(TaskItem task);
    }
}