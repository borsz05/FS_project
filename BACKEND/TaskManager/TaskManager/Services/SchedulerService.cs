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
                // Nem darabolható: keressük az első olyan napot, ahol elfér (szünetek figyelembe véve)
                foreach (var day in ScheduleDays)
                {
                    if (day.RemainingMinutes >= task.TotalMinutes + (day.Assignments.Count > 0 ? DaySchedule.BreakTime : 0))
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
                // Darabolható feladat: először próbáljuk meg egyben elhelyezni, ha lehetséges
                for (int i = 0; i < ScheduleDays.Count; i++)
                {
                    if (ScheduleDays[i].RemainingMinutes >= task.TotalMinutes + (ScheduleDays[i].Assignments.Count > 0 ? DaySchedule.BreakTime : 0))
                    {
                        ScheduleDays[i].Assignments.Add(new TaskAssignment(task.Name, task.TotalMinutes, ScheduleDays[i].DayNumber, task.AvailableDays));
                        return;
                    }
                }
            }
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
                for (int x = 0; x <= Math.Min(remaining, free); x++)
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
    }
}
