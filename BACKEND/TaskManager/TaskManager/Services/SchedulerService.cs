using System.Diagnostics.Tracing;
using System.Runtime.CompilerServices;
using TaskManager.Models;

namespace TaskManager.Services
{
    public class SchedulerService : ISchedulerService
    {
        // Összes eddig beérkezett task tárolása
        private List<TaskItem> _allTasks = new List<TaskItem>();
        public List<DaySchedule> ScheduleDays { get; set; } = new List<DaySchedule>();

        // Új task hozzáadása, majd teljes ütemterv újraszámolása
        public void AddTask(TaskItem newTask)
        {
            _allTasks.Add(newTask);
            RebuildSchedule();
        }
        // Task frissítése: a létező taskot lecseréljük a frissített adatokra,
        // majd újraszámoljuk az ütemtervet
        public void UpdateTask(TaskItem updatedTask)
        {
            var existingTask = _allTasks.FirstOrDefault(t => t.Id == updatedTask.Id);
            if (existingTask != null)
            {
                existingTask.Name = updatedTask.Name;
                existingTask.TotalHours = updatedTask.TotalHours;
                existingTask.AvailableDays = updatedTask.AvailableDays;

                RebuildSchedule();
            }
        }

        // Task törlése az ütemtervből, majd újraszámoljuk az ütemtervet
        public void DeleteTask(string taskId)
        {
            var taskToRemove = _allTasks.FirstOrDefault(t => t.Id == taskId);
            if (taskToRemove != null)
            {
                _allTasks.Remove(taskToRemove);
                RebuildSchedule();
            }
        }

        // Teljes ütemterv újraszámolása: több lehetséges elosztás szimulációjával
        // és a legkiegyensúlyozottabb eredmény kiválasztásával
        private void RebuildSchedule()
        {
            var sortedTasks = SortTasks(_allTasks);

            int totalMinutes = _allTasks.Sum(t => t.TotalMinutes);
            int estimatedDays = Math.Max(1, (int)Math.Ceiling((double)totalMinutes / DaySchedule.Capacity));
            int candidateExtra = 2;

            List<DaySchedule> bestSchedule = null;
            double bestMetric = double.MaxValue;

            // Szimulálunk különböző napok számával
            for (int candidateDays = estimatedDays; candidateDays <= estimatedDays + candidateExtra; candidateDays++)
            {
                var simulated = SimulateSchedule(candidateDays, sortedTasks);
                double metric = EvaluateSchedule(simulated);
                if (metric < bestMetric)
                {
                    bestMetric = metric;
                    bestSchedule = simulated;
                }
            }

            ScheduleDays = bestSchedule;
        }

        // Szimulálja az ütemtervet adott kezdeti napok számával
        private List<DaySchedule> SimulateSchedule(int initialDays, List<TaskItem> tasks)
        {
            var days = new List<DaySchedule>();
            for (int i = 1; i <= initialDays; i++)
            {
                days.Add(new DaySchedule(i));
            }

            foreach (var task in tasks)
            {
                DistributeTask(task, days);
            }

            return days;
        }

        // Feladatok rendezése: azokat előrébb, amelyek kevésbé oszthatóak
        private List<TaskItem> SortTasks(List<TaskItem> tasks)
        {
            return tasks.OrderBy(t => t.AvailableDays)
                        .ThenBy(t => t.TotalMinutes)
                        .ToList();
        }

        // Egy feladat elosztása a kapott napok listáján belül.
        // A logika úgy működik, hogy amíg marad kiosztandó perc (minutesRemaining), addig:
        // - Ha a feladat osztható és még nem értük el a maximális megengedett szétosztások számát (splitsAllowed > 1),
        //   akkor egy részletet osztunk szét.
        // - Ha az utolsó megengedett splitnél járunk (splitsAllowed == 1) vagy a feladat nem osztható,
        //   akkor addig nyitunk új napot, amíg az aktuális napban elfér a maradék.
        // Minden esetben, mielőtt új bejegyzést hoznánk létre, ellenőrizzük, hogy az adott napon már van-e
        // ugyanabból a taskból bejegyzés. Ha igen, akkor azt frissítjük.
        private void DistributeTask(TaskItem task, List<DaySchedule> days)
        {
            int minutesRemaining = task.TotalMinutes;
            int splitsAllowed = task.AvailableDays; // maximális szétosztási lehetőség

            while (minutesRemaining > 0)
            {
                // Ha még nem vagyunk az utolsó splitnél, akkor a legkevésbé terhelt napból próbálunk darabolni.
                if (task.Divisible && splitsAllowed > 1)
                {
                    var candidateDay = days.Where(d => CalculateAvailableMinutes(d) > 0)
                                           .OrderBy(d => d.EffectiveLoad)
                                           .FirstOrDefault();
                    if (candidateDay == null)
                    {
                        candidateDay = CreateNewDay(days);
                    }

                    int availableMinutes = CalculateAvailableMinutes(candidateDay);

                    // Nem osztható feladat esetén, ha az aktuális napban nem fér el a maradék, új napot hozunk létre.
                    if (!task.Divisible && availableMinutes < minutesRemaining)
                    {
                        candidateDay = CreateNewDay(days);
                        availableMinutes = CalculateAvailableMinutes(candidateDay);
                    }
                    if (availableMinutes <= 0)
                    {
                        candidateDay = CreateNewDay(days);
                        availableMinutes = CalculateAvailableMinutes(candidateDay);
                    }

                    // Itt úgy döntünk, hogy ne "fogyasszuk el" teljesen a candidateDay kapacitását,
                    // hanem egy részletet osztunk ki, lehetőleg egyenletesen.
                    int minutesToAssign = Math.Min(minutesRemaining, availableMinutes);

                    // Ellenőrizzük, hogy az adott napon már szerepel-e ez a task
                    var existingAssignment = candidateDay.Assignments.FirstOrDefault(a => a.TaskId == task.Id);
                    if (existingAssignment != null)
                    {
                        existingAssignment.Minutes += minutesToAssign;
                    }
                    else
                    {
                        if (task.Divisible)
                        {
                            candidateDay.Assignments.Add(
                                new TaskAssignment(task.Id, task.Name, minutesToAssign, candidateDay.DayNumber, task.AvailableDays)
                            );
                        }
                        else
                        {
                            candidateDay.Assignments.Add(
                                new TaskAssignment(task.Id, task.Name, minutesToAssign, task.AvailableDays)
                            );
                        }
                    }

                    minutesRemaining -= minutesToAssign;
                    splitsAllowed--;
                }
                else
                {
                    // Ez az eset vagy nem osztható feladat, vagy az utolsó megengedett splitnél vagyunk.
                    // Itt arra törekszünk, hogy minden maradék perc fel legyen használva.
                    // Keressük azt a napot, ahol elegendő kapacitás van; ha nincs, nyissunk újat.
                    var candidateDay = days.Where(d => CalculateAvailableMinutes(d) >= minutesRemaining)
                                           .OrderBy(d => d.EffectiveLoad)
                                           .FirstOrDefault();
                    if (candidateDay == null)
                    {
                        candidateDay = CreateNewDay(days);
                    }

                    int availableMinutes = CalculateAvailableMinutes(candidateDay);
                    // Ha mégsem férne el az összes maradék, akkor az aktuális napba annyit osztunk, amennyi belefér,
                    // és a maradékot a következő iterációban próbáljuk majd elosztani.
                    int minutesToAssign = Math.Min(minutesRemaining, availableMinutes);

                    var existingAssignment = candidateDay.Assignments.FirstOrDefault(a => a.TaskId == task.Id);
                    if (existingAssignment != null)
                    {
                        existingAssignment.Minutes += minutesToAssign;
                    }
                    else
                    {
                        if (task.Divisible)
                        {
                            candidateDay.Assignments.Add(
                                new TaskAssignment(task.Id, task.Name, minutesToAssign, candidateDay.DayNumber, task.AvailableDays)
                            );
                        }
                        else
                        {
                            candidateDay.Assignments.Add(
                                new TaskAssignment(task.Id, task.Name, minutesToAssign, task.AvailableDays)
                            );
                        }
                    }

                    minutesRemaining -= minutesToAssign;

                    // Ha még mindig maradt, de a candidateDay kapacitása elfogyott, akkor új napot hozunk létre.
                    if (minutesRemaining > 0 && CalculateAvailableMinutes(candidateDay) <= 0)
                    {
                        CreateNewDay(days);
                    }
                }
            }
        }

        // Kiszámolja, mennyi perc fér még bele az adott napba (figyelembe véve a szünetet)
        private int CalculateAvailableMinutes(DaySchedule day)
        {
            int available = day.RemainingMinutes;
            if (day.Assignments.Any())
            {
                available -= DaySchedule.BreakTime;
            }
            return available;
        }

        // Új nap létrehozása a megadott napok listájában
        private DaySchedule CreateNewDay(List<DaySchedule> days)
        {
            int newDayNumber = days.Count + 1;
            var newDay = new DaySchedule(newDayNumber);
            days.Add(newDay);
            return newDay;
        }

        // Kiértékeli az ütemterv kiegyensúlyozottságát:
        // A cél a legnagyobb és legkisebb EffectiveLoad közti különbség minimalizálása.
        private double EvaluateSchedule(List<DaySchedule> days)
        {
            if (days == null || days.Count == 0)
                return double.MaxValue;

            int min = days.Min(d => d.EffectiveLoad);
            int max = days.Max(d => d.EffectiveLoad);
            return max - min;
        }
    }
}
