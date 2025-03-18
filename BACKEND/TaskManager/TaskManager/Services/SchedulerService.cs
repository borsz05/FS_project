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
                // Új napok hozzáadása a minimum szükséges mennyiségben
                int minRequiredDays = Math.Min(
                    (int)Math.Ceiling((double)task.TotalMinutes / DaySchedule.Capacity),
                    task.AvailableDays
                );
                int counter = 0;
                // Ellenőrizzük, hogy van-e elég nap a blokkhoz
                while (counter < minRequiredDays)
                {
                    EnsureDayExists(ScheduleDays.Count + 1);
                    counter++;
                }
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
                    if (task.TotalMinutes >= DaySchedule.Capacity)
                    {
                        candidateStart = ScheduleDays.Count - minRequiredDays;
                    }
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
                        var day = ScheduleDays[bestCandidateStart + j];
                        var existing = day.Assignments.FirstOrDefault(a => a.TaskId == task.Id);
                        if (existing != null)
                            existing.Minutes += assigned;
                        else
                        {
                            day.Assignments.Add(new TaskAssignment(task.Id, task.Name, assigned, taskStartDay, task.AvailableDays));
                            GlobalRebalance();
                        }

                    }
                }
            }
            CleanupEmptyDays();
            GlobalRebalance();
        }

        // Visszalépéses algoritmust használ arra, hogy minden lehetséges módon elossza a H percnyi feladatot K egymást követő nap között,
        // figyelembe véve az adott napok szabad perceit.
        private List<int[]> GetDistributions(int H, int K, int candidateStart)
        {
            var results = new List<int[]>();
            int[] current = new int[K];

            void Recurse(int idx, int remaining)
            {
                if (idx == K)
                {
                    if (remaining == 0)
                        results.Add((int[])current.Clone());
                    return;
                }
                EnsureDayExists(candidateStart + idx + 1);
                int free = ScheduleDays[candidateStart + idx].RemainingMinutes;
                if (ScheduleDays[candidateStart + idx].Assignments.Any())
                    free -= DaySchedule.BreakTime;

                for (int x = Math.Max(0, remaining - (K - idx - 1) * free); x <= Math.Min(remaining, free); x++)
                {
                    current[idx] = x;
                    Recurse(idx + 1, remaining - x);
                }
            }
            Recurse(0, H);
            return results;
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

        // Az OptimizeSchedule metódus végigmegy a napokon, és ha két szomszédos nap között jelentős eltérés van,
        // megpróbálja áthelyezni az osztható feladatok egy részét a kevésbé terhelt napra, az id‑alapú logika szerint.
        //private void OptimizeSchedule()
        //{
        //    bool improvement;
        //    int iterations = 0;
        //    int maxIterations = 1000;
        //    do
        //    {
        //        improvement = false;
        //        for (int i = 0; i < ScheduleDays.Count - 1; i++)
        //        {
        //            DaySchedule dayA = ScheduleDays[i];
        //            DaySchedule dayB = ScheduleDays[i + 1];
        //            DaySchedule dayHigh = dayA.EffectiveLoad > dayB.EffectiveLoad ? dayA : dayB;
        //            DaySchedule dayLow = dayA.EffectiveLoad > dayB.EffectiveLoad ? dayB : dayA;
        //            int diff = dayHigh.EffectiveLoad - dayLow.EffectiveLoad;
        //            if (diff <= 0)
        //                continue;
        //            foreach (var assignment in dayHigh.Assignments.Where(a => a.IsDivisible).ToList())
        //            {
        //                if (dayLow.DayNumber >= assignment.TaskStartDay &&
        //                    dayLow.DayNumber < assignment.TaskStartDay + assignment.TaskAvailableDays)
        //                {
        //                    int availableForLow = dayLow.RemainingMinutes;
        //                    bool alreadyPresent = dayLow.Assignments.Any(a => a.TaskId == assignment.TaskId);
        //                    int extraBreakCost = alreadyPresent ? 0 : (dayLow.Assignments.Any() ? DaySchedule.BreakTime : 0);
        //                    int movable = Math.Min(assignment.Minutes, diff / 2);
        //                    int canMove = Math.Min(movable, availableForLow - extraBreakCost);
        //                    if (canMove > 0)
        //                    {
        //                        assignment.Minutes -= canMove;
        //                        if (assignment.Minutes == 0)
        //                            dayHigh.Assignments.Remove(assignment);
        //                        var target = dayLow.Assignments.FirstOrDefault(a => a.TaskId == assignment.TaskId);
        //                        if (target != null)
        //                            target.Minutes += canMove;
        //                        else
        //                            dayLow.Assignments.Add(new TaskAssignment(assignment.TaskId, assignment.TaskName, canMove, assignment.TaskStartDay, assignment.TaskAvailableDays));
        //                        improvement = true;
        //                    }
        //                }
        //            }
        //        }
        //        iterations++;
        //    } while (improvement && iterations < maxIterations);
        //}

        public void GlobalRebalance()
        {
            int iteration = 0;
            int maxIterations = 13; // Maximális iterációk, hogy elkerüljük a végtelen ciklust
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
                    int transferable = Math.Min(assignment.Minutes, minDay.RemainingMinutes);
                    if (transferable > 0)
                    {
                        assignment.Minutes -= transferable;
                        var existing = minDay.Assignments.FirstOrDefault(a => a.TaskId == assignment.TaskId && a.IsDivisible);
                        if (existing != null)
                            existing.Minutes += transferable;
                        else
                            minDay.Assignments.Add(new TaskAssignment(assignment.TaskId, assignment.TaskName, transferable, minDay.DayNumber, 1));
                        moved = true;
                        break;
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
