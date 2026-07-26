using System;
using System.Collections.Generic;
using System.Linq;
using GameSaveCenter.Core.Models;

namespace GameSaveCenter.Core.Services
{
    /// <summary>
    /// Implements layered retention: all recent versions, then representative daily,
    /// weekly and monthly versions. Locked and PreRestore snapshots are never removed.
    /// </summary>
    public sealed class RetentionPlanner
    {
        public RetentionPlan CreatePlan(IEnumerable<BackupSnapshot> snapshots, RetentionPolicy policy, DateTime nowUtc)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            var ordered = (snapshots ?? Enumerable.Empty<BackupSnapshot>())
                .OrderByDescending(x => x.CreatedUtc)
                .ToList();
            var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var snapshot in ordered.Where(x => x.IsLocked || x.IsPreRestore))
            {
                keep.Add(snapshot.BackupId);
            }

            foreach (var snapshot in ordered.Where(x => nowUtc - x.CreatedUtc <= policy.KeepAllFor))
            {
                keep.Add(snapshot.BackupId);
            }

            KeepFirstPerBucket(ordered, keep,
                x => x.CreatedUtc.Date,
                x => nowUtc - x.CreatedUtc <= TimeSpan.FromDays(policy.KeepDailyDays));

            KeepFirstPerBucket(ordered, keep,
                x => GetIsoWeekKey(x.CreatedUtc),
                x => nowUtc - x.CreatedUtc <= TimeSpan.FromDays(policy.KeepWeeklyWeeks * 7));

            KeepFirstPerBucket(ordered, keep,
                x => x.CreatedUtc.Year * 100 + x.CreatedUtc.Month,
                x => nowUtc - x.CreatedUtc <= TimeSpan.FromDays(policy.KeepMonthlyMonths * 31));

            return new RetentionPlan
            {
                Keep = ordered.Where(x => keep.Contains(x.BackupId)).ToList(),
                DeleteCandidates = ordered.Where(x => !keep.Contains(x.BackupId)).ToList()
            };
        }

        private static void KeepFirstPerBucket<TKey>(
            IEnumerable<BackupSnapshot> snapshots,
            ISet<string> keep,
            Func<BackupSnapshot, TKey> bucket,
            Func<BackupSnapshot, bool> inRange)
        {
            foreach (var group in snapshots.Where(inRange).GroupBy(bucket))
            {
                var representative = group.OrderByDescending(x => x.CreatedUtc).First();
                keep.Add(representative.BackupId);
            }
        }

        private static int GetIsoWeekKey(DateTime value)
        {
            var day = System.Globalization.CultureInfo.InvariantCulture.Calendar.GetDayOfWeek(value);
            if (day >= DayOfWeek.Monday && day <= DayOfWeek.Wednesday) value = value.AddDays(3);
            var week = System.Globalization.CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                value,
                System.Globalization.CalendarWeekRule.FirstFourDayWeek,
                DayOfWeek.Monday);
            return value.Year * 100 + week;
        }
    }

    /// <summary>Retention calculation result. Deletion remains an explicit operation.</summary>
    public sealed class RetentionPlan
    {
        public List<BackupSnapshot> Keep { get; set; } = new List<BackupSnapshot>();
        public List<BackupSnapshot> DeleteCandidates { get; set; } = new List<BackupSnapshot>();
    }
}
