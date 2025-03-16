using Microsoft.AspNetCore.Mvc;
using TaskManager.Models;
using TaskManager.Services;

namespace TaskManager.Controllers
{
    [ApiController]
    [Route("scheduler")]
    public class SchedulerController : ControllerBase
    {
        private readonly ISchedulerService schedulerService;
        public SchedulerController(ISchedulerService schedulerService)
        {
            this.schedulerService = schedulerService;
        }
        [HttpPost]
        public void AddTask([FromBody] TaskItem task)
        {
            schedulerService.InsertTask(task);
        }
    }
}
