#if !VRT_WITHOUT_STATS
using System;
using System.IO;
using NUnit.Framework;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;

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

        [Test]
        public void InitializeAfterOutputIsAnError()
        {
            // Statistics state is static and can't be reset: this leaves Statistics initialized and
            // writing to a temporary file for the rest of the test run (#59).
            string statsFile = Path.Combine(Path.GetTempPath(), $"cwipc-stats-test-{Guid.NewGuid()}.log");
            Statistics.Output("StatisticsTests", "test=1");
            LogAssert.Expect(LogType.Error, new Regex("Statistics.Initialize\\(\\) should be called before outputting statistics"));
            Statistics.Initialize(10, statsFile);
            // The start line of the stats file says that records went to the Unity log.
            // FileShare.ReadWrite: Statistics keeps the file open.
            using (var stream = new FileStream(statsFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                string firstLine = reader.ReadLine();
                StringAssert.Contains("component=stats, starting=1", firstLine);
                StringAssert.Contains("records_before_initialize=", firstLine);
            }
        }
    }
}
#endif
