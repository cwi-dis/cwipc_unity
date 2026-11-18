#if !VRT_WITHOUT_STATS
using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Cwipc.Tests
{
    /// <summary>
    /// Tests for the stats file path (cwi-dis/cwipc_unity#61, #63).
    /// </summary>
    public class StatisticsTests
    {
        static readonly DateTime When = new DateTime(2026, 11, 18, 14, 30, 0);

        [Test]
        public void RelativeStatsFileIsInPersistentDataPath()
        {
            string path = Statistics.StatsFilename("stats.log", When);
            Assert.AreEqual(Path.Combine(Application.persistentDataPath, "stats.log"), path);
        }

        [Test]
        public void AbsoluteStatsFileIsUsedAsIs()
        {
            string absolute = Path.Combine(Path.GetTempPath(), "cwipc-stats-test", "stats.log");
            Assert.IsTrue(Path.IsPathRooted(absolute));
            Assert.AreEqual(absolute, Statistics.StatsFilename(absolute, When));
        }

        [Test]
        public void HostAndTimestampAreReplaced()
        {
            string path = Statistics.StatsFilename("stats-{host}-{ts}.log", When);
            Assert.AreEqual($"stats-{Environment.MachineName}-20261118-1430.log", Path.GetFileName(path));
        }
    }
}
#endif
