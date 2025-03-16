using TaskManager.Models;

namespace TaskManager.Services
{
    public interface ISchedulerService
    {
        void InsertTask(TaskItem task);
    }
}