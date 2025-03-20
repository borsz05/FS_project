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
        [HttpGet]
        public IEnumerable<DaySchedule> GetSchedule()
        {
            return schedulerService.ScheduleDays;
        }
        [HttpPost]
        public IActionResult AddTask([FromBody] TaskItem task)
        {
            // Ha a feladat nem darabolható és az összóraszám meghaladja a napi maximumot,
            // akkor hibával térünk vissza a frontenden.
            if (!task.Divisible && task.TotalMinutes > DaySchedule.Capacity)
            {
                return BadRequest(new { message = $"The task is too long, with a maximum of {DaySchedule.Capacity / 60} hours allowed per day." });
            }
            else if (task.Divisible&&task.TotalMinutes>DaySchedule.Capacity*task.AvailableDays)
            {
                return BadRequest(new { message = $"The task does not fit on the number of days given." });
            }

            schedulerService.AddTask(task);
            return Ok();
        }
    }
}
