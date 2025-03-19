using System.Diagnostics.Tracing;
using System.Runtime.CompilerServices;
using TaskManager.Models;

namespace TaskManager.Services
{
    public class SchedulerService : ISchedulerService
    {
        public List<DaySchedule> ScheduleDays { get; set; } = new List<DaySchedule>();

        public SchedulerService()
        {
            // Kezdjük az első nappal – mindig legalább egy nap legyen.
            ScheduleDays.Add(new DaySchedule(1));
        }

        private void EnsureDayExists(int dayNumber)
        {
            while (ScheduleDays.Count < dayNumber)
                ScheduleDays.Add(new DaySchedule(ScheduleDays.Count + 1));
        }

        public void InsertTask(TaskItem task)
        {
            if (!task.Divisible)
            {
                // Nem darabolható: keressük az első olyan napot, ahol elfér.
                foreach (var day in ScheduleDays)
                {
                    if (day.RemainingMinutes >= task.TotalMinutes + (day.Assignments.Any() ? DaySchedule.BreakTime : 0))
                    {
                        // Ha már szerepel a feladat az adott napon, frissítjük a perceket,
                        // különben új assignment kerül hozzáadásra.
                        var existing = day.Assignments.FirstOrDefault(a => a.TaskId == task.Id);
                        if (existing != null)
                            existing.Minutes += task.TotalMinutes;
                        else
                        {
                            day.Assignments.Add(new TaskAssignment(task.Id, task.Name, task.TotalMinutes));
                            CleanupEmptyDays();
                            GlobalRebalance();
                            EnsureCapacityLimit();
                        }
                            return;
                    }
                }
                int newDay = ScheduleDays.Count + 1;
                EnsureDayExists(newDay);
                ScheduleDays[newDay - 1].Assignments.Add(new TaskAssignment(task.Id, task.Name, task.TotalMinutes));
            }
            else
            {
                // Próbáljuk meg egyben elhelyezni a feladatot, ha lehetséges.
                for (int i = 0; i < ScheduleDays.Count; i++)
                {
                    if (ScheduleDays[i].RemainingMinutes >= task.TotalMinutes + (ScheduleDays[i].Assignments.Any() ? DaySchedule.BreakTime : 0))
                    {
                        var existing = ScheduleDays[i].Assignments.FirstOrDefault(a => a.TaskId == task.Id);
                        if (existing != null)
                            existing.Minutes += task.TotalMinutes;
                        else
                        {
                            ScheduleDays[i].Assignments.Add(new TaskAssignment(task.Id, task.Name, task.TotalMinutes, ScheduleDays[i].DayNumber, task.AvailableDays));
                            CleanupEmptyDays();
                            GlobalRebalance();
                            EnsureCapacityLimit();
                        }
                        return;
                    }
                }
                // Candidate blokk kiválasztása: próbáljunk candidate blokk méreteket 1-től task.AvailableDays-ig.
                int bestCandidateBlockSize = task.AvailableDays;
                int bestCandidateStart = -1;
                int[] bestDistribution = null;
                int bestScore = int.MaxValue;

                for (int candidateBlockSize = 1; candidateBlockSize <= task.AvailableDays; candidateBlockSize++)
                {
                    int candidateStart = -1;
                    // Megkeressük a candidate blokkot a meglévő napok között.
                    for (int i = 0; i <= ScheduleDays.Count - candidateBlockSize; i++)
                    {
                        int totalFree = 0;
                        for (int j = 0; j < candidateBlockSize; j++)
                        {
                            EnsureDayExists(i + j + 1);
                            int freeTime = ScheduleDays[i + j].RemainingMinutes;
                            if (ScheduleDays[i + j].Assignments.Any())
                                freeTime -= DaySchedule.BreakTime;
                            totalFree += freeTime;
                        }
                        if (totalFree >= task.TotalMinutes)
                        {
                            candidateStart = i;
                            break;
                        }
                    }
                    // Ha nem találunk megfelelő blokkot, új napokat adunk hozzá.
                    if (candidateStart == -1)
                    {
                        candidateStart = ScheduleDays.Count;
                        for (int j = 0; j < candidateBlockSize; j++)
                            EnsureDayExists(candidateStart + j);
                    }
                    int[] distribution = FindOptimalDistribution(task.TotalMinutes, candidateBlockSize, candidateStart);
                    if (distribution == null)
                        continue;

                    int maxFinal = int.MinValue, minFinal = int.MaxValue;
                    int fragmentsCount = 0;
                    for (int i = 0; i < candidateBlockSize; i++)
                    {
                        int load = ScheduleDays[candidateStart + i].EffectiveLoad + distribution[i];
                        maxFinal = Math.Max(maxFinal, load);
                        minFinal = Math.Min(minFinal, load);
                        if (distribution[i] > 0)
                            fragmentsCount++;
                    }
                    int cost = maxFinal - minFinal;
                    int score = cost + fragmentsCount * 10; // A fragmentumok súlyozása.
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestCandidateBlockSize = candidateBlockSize;
                        bestCandidateStart = candidateStart;
                        bestDistribution = distribution;
                    }
                }
                // Fallback: ha nem találtunk megfelelő candidate blokkot.
                if (bestDistribution == null)
                {
                    int candidateBlockSize = task.AvailableDays;
                    int candidateStart = ScheduleDays.Count;
                    for (int j = 0; j < candidateBlockSize; j++)
                        EnsureDayExists(candidateStart + j);
                    bestDistribution = FindOptimalDistribution(task.TotalMinutes, candidateBlockSize, candidateStart);
                    bestCandidateBlockSize = candidateBlockSize;
                    bestCandidateStart = candidateStart;
                }
                int taskStartDay = ScheduleDays[bestCandidateStart].DayNumber;
                for (int j = 0; j < bestCandidateBlockSize; j++)
                {
                    int assigned = bestDistribution[j];
                    if (assigned > 0)
                    {
                        EnsureDayExists(bestCandidateStart + bestCandidateBlockSize);
                        var day = ScheduleDays[bestCandidateStart + j];
                        var existing = day.Assignments.FirstOrDefault(a => a.TaskId == task.Id);
                        if (existing != null)
                            existing.Minutes += assigned;
                        else
                        {
                            day.Assignments.Add(new TaskAssignment(task.Id, task.Name, assigned, taskStartDay, task.AvailableDays));
                            GlobalRebalance();
                            EnsureCapacityLimit();
                        }

                    }
                }
            }
            CleanupEmptyDays();
            GlobalRebalance();
            EnsureCapacityLimit();
        }

        // Visszalépéses algoritmust használ arra, hogy minden lehetséges módon elossza a H percnyi feladatot K egymást követő nap között,
        // figyelembe véve az adott napok szabad perceit.
        private List<int[]> GetDistributions(int H, int K, int candidateStart)
        {
            // Kiszámoljuk az egyes napokra rendelkezésre álló szabad perceket.
            int[] free = new int[K];
            for (int i = 0; i < K; i++)
            {
                EnsureDayExists(candidateStart + i + 1);
                free[i] = ScheduleDays[candidateStart + i].RemainingMinutes;
                if (ScheduleDays[candidateStart + i].Assignments.Any())
                    free[i] -= DaySchedule.BreakTime;
            }

            // Ha a teljes szabad idő kevesebb, mint H, akkor nem található megoldás.
            int totalFree = free.Sum();
            if (totalFree < H)
                return new List<int[]>();

            // Inicializáljuk az elosztást 0-kkal.
            int[] distribution = new int[K];
            int remaining = H;

            // A "candidates" lista tartalmazza azokat a napok indexeit, amelyek még rendelkeznek szabad kapacitással.
            List<int> candidates = Enumerable.Range(0, K).ToList();

            // Vízszintű feltöltés: addig osztjuk el a maradék perceket,
            // amíg az egyenletes elosztás lehetséges.
            while (remaining > 0 && candidates.Count > 0)
            {
                // Megkeressük a legkisebb, még felvehető mennyiséget az aktuális jelöltek közül.
                int delta = int.MaxValue;
                foreach (int idx in candidates)
                {
                    int available = free[idx] - distribution[idx];
                    if (available < delta)
                        delta = available;
                }

                int count = candidates.Count;
                if (count * delta <= remaining)
                {
                    // Ha elegendő maradék van, akkor minden jelölt naphoz hozzáadjuk a delta értéket.
                    foreach (int idx in candidates)
                    {
                        distribution[idx] += delta;
                    }
                    remaining -= count * delta;

                    // Azok a napok, amelyek most beteltek, eltávolításra kerülnek a jelöltek közül.
                    candidates = candidates.Where(idx => distribution[idx] < free[idx]).ToList();
                }
                else
                {
                    // Ha már nem osztható fel egyenlően az összes nap között, akkor
                    // elosztjuk a maradékot az egyenlő részekre, majd a maradékot egyenként.
                    int equalExtra = remaining / count;
                    int extraRemainder = remaining % count;
                    foreach (int idx in candidates)
                    {
                        distribution[idx] += equalExtra;
                    }
                    for (int i = 0; i < extraRemainder; i++)
                    {
                        distribution[candidates[i]] += 1;
                    }
                    remaining = 0;
                }
            }

            return new List<int[]> { distribution };
        }


        // Kiválasztja azt az elosztást a candidate blokkban, amely minimalizálja a napok EffectiveLoad közti különbséget.
        private int[] FindOptimalDistribution(int H, int K, int candidateStart)
        {
            var distributions = GetDistributions(H, K, candidateStart);
            int[] best = null;
            int bestCost = int.MaxValue;
            foreach (var dist in distributions)
            {
                int maxFinal = int.MinValue;
                int minFinal = int.MaxValue;
                for (int i = 0; i < K; i++)
                {
                    int final = ScheduleDays[candidateStart + i].EffectiveLoad + dist[i];
                    maxFinal = Math.Max(maxFinal, final);
                    minFinal = Math.Min(minFinal, final);
                }
                int cost = maxFinal - minFinal;
                if (cost < bestCost)
                {
                    bestCost = cost;
                    best = dist;
                }
            }
            return best;
        }

        private void CleanupEmptyDays()
        {
            // Eltávolítjuk az üres napokat (kivéve az első napot, ha szükséges)
            for (int i = ScheduleDays.Count - 1; i >= 1; i--)
            {
                if (ScheduleDays[i].Assignments.Count == 0)
                    ScheduleDays.RemoveAt(i);
            }
            // Újranumeráljuk a napokat 1-től kezdődően
            for (int i = 0; i < ScheduleDays.Count; i++)
            {
                int oldNumber = ScheduleDays[i].DayNumber;
                int newNumber = i + 1;
                if (oldNumber != newNumber)
                {
                    ScheduleDays[i].DayNumber = newNumber;
                    foreach (var assignment in ScheduleDays[i].Assignments)
                    {
                        if (assignment.TaskStartDay == oldNumber)
                            assignment.TaskStartDay = newNumber;
                    }
                }
            }
        }


        public void GlobalRebalance()
        {
            int iteration = 0;
            int maxIterations = 25; // Maximális iterációk, hogy elkerüljük a végtelen ciklust
            while (iteration < maxIterations)
            {
                iteration++;
                bool moved = false;
                var maxDay = ScheduleDays.OrderByDescending(day => day.EffectiveLoad).First();
                var minDay = ScheduleDays.OrderBy(day => day.EffectiveLoad).First();
                int diff = maxDay.EffectiveLoad - minDay.EffectiveLoad;
                if (diff <= 1)
                    break;
                foreach (var assignment in maxDay.Assignments.Where(a => a.IsDivisible).ToList())
                {
                    // Csak azokat a napokat vesszük figyelembe, amelyek az eredeti ablakban vannak.
                    var allowedDays = ScheduleDays
                        .Where(d => d.DayNumber >= assignment.TaskStartDay &&
                                    d.DayNumber < assignment.TaskStartDay + assignment.TaskAvailableDays)
                        .OrderBy(d => d.EffectiveLoad)
                        .ToList();

                    if (!allowedDays.Any())
                        continue;

                    var targetDay = allowedDays.First();

                    int transferable = Math.Min(assignment.Minutes, targetDay.RemainingMinutes);
                    if (transferable > 0)
                    {
                        assignment.Minutes -= transferable;
                        var existing = targetDay.Assignments.FirstOrDefault(a => a.TaskId == assignment.TaskId && a.IsDivisible);
                        if (existing != null)
                            existing.Minutes += transferable;
                        else
                            targetDay.Assignments.Add(new TaskAssignment(assignment.TaskId, assignment.TaskName, transferable, targetDay.DayNumber, assignment.TaskAvailableDays));
                        
                    }
                }
                if (!moved)
                    break;
            }
            CleanupEmptyDays();
            EnsureCapacityLimit();
        }

        public void EnsureCapacityLimit()
        {
            foreach (var day in ScheduleDays)
            {
                if (day.EffectiveLoad > DaySchedule.Capacity)
                {
                    int excess = day.EffectiveLoad - DaySchedule.Capacity;

                    // Először próbáljuk áthelyezni a darabolható feladatokat
                    var movableTasks = day.Assignments
                        .Where(a => a.IsDivisible)
                        .OrderByDescending(a => a.Minutes) // Nagyobb feladatokkal kezdünk
                        .ToList();

                    foreach (var task in movableTasks)
                    {
                        if (excess <= 0) break;

                        var otherDay = ScheduleDays
                            .Where(d => d != day && d.RemainingMinutes >= 15) // Olyan napot keresünk, ahol van legalább egy kis szabad hely
                            .OrderBy(d => d.EffectiveLoad) // A legkevésbé terhelt napra helyezünk át
                            .FirstOrDefault();

                        if (otherDay != null)
                        {
                            int transferable = Math.Min(task.Minutes, excess);
                            task.Minutes -= transferable;
                            excess -= transferable;

                            // Ha az adott nap már tartalmazza ezt a feladatot, növeljük annak az idejét
                            var existingTask = otherDay.Assignments
                                .FirstOrDefault(a => a.TaskName == task.TaskName && a.IsDivisible);

                            if (existingTask != null)
                            {
                                existingTask.Minutes += transferable;
                            }
                            else
                            {
                                otherDay.Assignments.Add(new TaskAssignment(task.TaskId,task.TaskName, transferable, otherDay.DayNumber, task.TaskAvailableDays));
                            }

                        }
                    }
                }
            }
        }
    }
}
