using TaskManager.Models;

namespace TaskManager.Services
{
    public class SchedulerService:ISchedulerService
    {
        public List<DaySchedule> ScheduleDays { get; set; } = new List<DaySchedule>();

        public SchedulerService()
        {
            // Kezdjük az első nappal
            // biztosítja hogy mindig legalább egy napot tartalmazzon a beosztás így el lehet kezdeni a feladatok elosztását
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
                        day.Assignments.Add(new TaskAssignment(task.Name, task.TotalMinutes));
                        return;
                    }
                }
                int newDay = ScheduleDays.Count + 1;
                EnsureDayExists(newDay);
                ScheduleDays[newDay - 1].Assignments.Add(new TaskAssignment(task.Name, task.TotalMinutes));
            }
            else
            {
                // Próbáljuk meg egyben elhelyezni a feladatot, ha lehetséges.
                for (int i = 0; i < ScheduleDays.Count; i++)
                {
                    if (ScheduleDays[i].RemainingMinutes >= task.TotalMinutes + (ScheduleDays[i].Assignments.Any() ? DaySchedule.BreakTime : 0))
                    {
                        ScheduleDays[i].Assignments.Add(new TaskAssignment(task.Name, task.TotalMinutes, ScheduleDays[i].DayNumber, task.AvailableDays));
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
                            // Ha már van másik feladat, akkor a szünetet levonjuk!
                            if (ScheduleDays[i + j].Assignments.Any())
                            {
                                freeTime -= DaySchedule.BreakTime;
                            }
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
                    // Ha nem sikerült érvényes elosztást találni, lépjünk tovább.
                    if (distribution == null)
                        continue;

                    // Számoljuk az elosztás "score"-át: a kisebb terheléskülönbség és kevesebb fragmentum jobb.
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
                    int score = cost + fragmentsCount * 10; // A fragmentumok száma súlyozva van.
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestCandidateBlockSize = candidateBlockSize;
                        bestCandidateStart = candidateStart;
                        bestDistribution = distribution;
                    }
                }
                // Fallback: ha egy candidate blokk esetén sem találtuk meg az érvényes elosztást,
                // használjuk a maximális candidate blokkot.
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
                        ScheduleDays[bestCandidateStart + j].Assignments.Add(
                            new TaskAssignment(task.Name, assigned, taskStartDay, task.AvailableDays)
                        );
                    }
                }
            }
            CleanupEmptyDays();
        }

        //visszalépéses algoritmust használ arra,
        //hogy minden lehetséges módon elossza a H percnyi feladatot K egymást követő nap között,
        //figyelembe véve az adott napok szabad perceit
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
                {
                    free -= DaySchedule.BreakTime;
                }

                // limitáljuk x -et, hogy elkeruljuk a felesleges rekurziokat
                for (int x = Math.Max(0, remaining - (K - idx - 1) * free); x <= Math.Min(remaining, free); x++)
                {
                    current[idx] = x;
                    Recurse(idx + 1, remaining - x);
                }
            }
            Recurse(0, H);
            return results;
        }

        // Kiválasztja a candidate blokk (candidateStart, hossz K) esetén a legjobb elosztást,
        // minimalizálja a napok EffectiveLoad közti maximum és minimum különbséget.
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
            for (int i = ScheduleDays.Count - 1; i >= 1; i--)
            {
                if (ScheduleDays[i].Assignments.Count == 0)
                {
                    ScheduleDays.RemoveAt(i);
                }
                else
                {
                    break; // az első nem üres napnál megállunk
                }
            }
        }


    }
}
