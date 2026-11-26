using UnityEngine;
using System;
using System.Globalization;

namespace Cwipc
{
#if !VRT_WITHOUT_STATS
    /// <summary>
    /// Gathering statistics during a run to allow later analysis.
    /// All statistics records consist of lines starting with "stats:" followed by name=value pairs.
    /// Each statistics record has some common fields: a field seq= which gives the sequence number and a field ts= which
    /// gives the number of seconds since midnight, on this machines clock. Most statistics records have
    /// a component= field that gives the name of the object responsible for creating this statistics record.
    ///
    /// An aggregating component subclasses Statistics: it adds up values in its own statsUpdate() and
    /// calls MaybeOutput(), and builds its record in OutputAggregate(). When the stats interval is over
    /// it writes a periodic record; when it stops it calls Final(), which writes its final record with
    /// what it added up since its last record (#89). The stats interval (see Initialize()) is
    /// negative for final-only (the default: only final records), 0 for a record on every update, or
    /// a number of seconds.
    ///
    /// One-shot records (mappings between things, events) use the static Output(name, s).
    ///
    /// Statistics records are sent to the Unity Log by default, or written to a separate statistics file.
    ///
    /// Repository <https://github.com/cwi-dis/VRTstatistics> has various tools to collect and agregate the statistics, plot
    /// them, etc.
    /// </summary>
    public abstract class Statistics
    {
        protected string name;
        private System.DateTime statsLastTime;
        private static bool initialized = false;
        // Negative: final-only (#89).
        private static double defaultStatsInterval = -1;
        private static System.IO.StreamWriter statsStream;
        private static bool muted = false;
        private static bool explicitlyInitialized = false;

        /// <summary>
        /// Lock for the aggregation counters of a subclass. statsUpdate() bodies take it, and so do
        /// MaybeOutput() and Final(), because statsUpdate() may be called from other threads than the
        /// one that calls Final() (#89).
        /// </summary>
        protected readonly object statsLock = new object();
        private volatile bool finalDone = false;
        private double recordInterval;

        /// <summary>
        /// Call this static method early during initialization to configure Statistics logger.
        /// Without it, Statistics initializes itself with the defaults (records to the Unity log)
        /// on first use. Calling it with arguments after records have been output is an error:
        /// those records went to the Unity log, not to the stats file (#59).
        /// </summary>
        /// <param name="_defaultStatsInterval">Stats interval in seconds: negative for final-only (only final
        /// records, the default), 0 for a record on every update. null keeps the current value.</param>
        /// <param name="statsOutputFile">Output file name</param>
        /// <param name="append">If true append to the output file, otherwise overwrite</param>
        public static void Initialize(double? _defaultStatsInterval = null, string statsOutputFile = null, bool append = false)
        {
            bool configuring = _defaultStatsInterval.HasValue || statsOutputFile != null;
            if (initialized && configuring)
            {
                if (seq > 0)
                {
                    Debug.LogError($"Statistics: Statistics.Initialize() should be called before outputting statistics: {seq} records were already output to the Unity log, not to the stats file");
                }
                else if (explicitlyInitialized)
                {
                    Debug.LogWarning("Statistics: Initialize() called more than once");
                }
            }
            if (configuring) explicitlyInitialized = true;
            if (_defaultStatsInterval.HasValue)
            {
                defaultStatsInterval = _defaultStatsInterval.Value;
            }
            DateTime now = DateTime.Now;
            if (statsOutputFile == "-")
            {
                muted = true;
                Debug.Log("stats: muted=1");
                return;
            }
            if (statsOutputFile != null && statsOutputFile != "")
            {
                string ts = now.ToString("yyyyMMdd-HHmm");
                string statsFilename = StatsFilename(statsOutputFile, now);
                statsStream = new System.IO.StreamWriter(statsFilename, append);
                //
                // Write an identifying line to both the statsfile (so we can split runs) and the console (so we can find the stats file)
                //
                string statsLine = $"stats: ts={Num(System.DateTime.Now.TimeOfDay.TotalSeconds, 3)}, component=stats, starting=1, wallClock={ts}, statsFilename={statsFilename}";
                if (seq > 0)
                {
                    // Records output before this Initialize() went to the Unity log, not to this file (#59).
                    statsLine += $", records_before_initialize={seq}";
                }
                statsStream.WriteLine(statsLine);
                statsStream.Flush();
                Debug.Log(statsLine);
            }
            initialized = true;
        }

        /// <summary>
        /// The full path of the stats file for a configured statsOutputFile: {host} and {ts} are
        /// replaced by the machine name and the time, and a relative name is relative to
        /// Application.persistentDataPath. An absolute name is used as is (#61).
        /// </summary>
        public static string StatsFilename(string statsOutputFile, DateTime now)
        {
            string sfn = statsOutputFile;
            sfn = sfn.Replace("{host}", Environment.MachineName);
            sfn = sfn.Replace("{ts}", now.ToString("yyyyMMdd-HHmm"));
            return System.IO.Path.Combine(Application.persistentDataPath, sfn);
        }

        static void Flush()
        {
            // xxxjack: need to check performance penalty of this.
            // If it turns out to be too much we call at most every second or something like that.
            if (statsStream != null) statsStream.Flush();

        }

        /// <summary>
        /// Base class constructor, called by subclasses. The interval of the first record starts now.
        /// </summary>
        /// <param name="_name">Name of the component to which this instance pertains</param>
        protected Statistics(string _name)
        {
            if (!initialized) Initialize();
            name = _name;
            statsLastTime = System.DateTime.Now;
        }

        ~Statistics()
        {
            // Only a check, at whatever moment the garbage collector gets here: no record, and
            // nothing with the stats stream (#89).
            try
            {
                if (!finalDone && !muted)
                {
                    Debug.LogWarning($"Statistics: {GetType().FullName} ({name}): Final() was never called");
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Build the record for what was added up since the last record, with OutputFields(), and
        /// reset the counters. Called with statsLock held, for periodic records and for the final
        /// record. A class with nothing to aggregate overrides this with an empty body.
        /// </summary>
        protected abstract void OutputAggregate();

        /// <summary>
        /// Call at the end of statsUpdate(): writes a periodic record when the stats interval is over.
        /// </summary>
        protected void MaybeOutput()
        {
            lock (statsLock)
            {
                if (finalDone) return;
                if (ShouldOutput()) OutputRecord();
            }
        }

        /// <summary>
        /// Write the final record: what was added up since the last record. Call once, when the
        /// component stops; later calls do nothing, and later statsUpdate() calls write no records.
        /// </summary>
        public void Final()
        {
            lock (statsLock)
            {
                if (finalDone) return;
                finalDone = true;
                OutputRecord();
            }
        }

        private void OutputRecord()
        {
            recordInterval = Interval();
            OutputAggregate();
            statsLastTime = System.DateTime.Now;
        }

        /// <summary>
        /// Duration in seconds of the current interval, so far.
        /// </summary>
        protected double Interval()
        {
            return (System.DateTime.Now - statsLastTime).TotalSeconds;
        }

        /// <summary>
        /// Inside OutputAggregate(): the duration in seconds of the interval of the record being built
        /// (the same value as its interval_s).
        /// </summary>
        protected double RecordInterval => recordInterval;

        /// <summary>
        /// Return true if our interval is over and a periodic record should be written.
        /// </summary>
        private bool ShouldOutput()
        {
            double interval = defaultStatsInterval;
            if (interval < 0) return false;
            // Elapsed seconds rather than DateTime + TimeSpan: doesn't throw for huge intervals (#89).
            return Interval() > interval;
        }

        /// <summary>
        /// Write an aggregating record: the given fields (null ones are left out), plus interval_s.
        /// Build the fields with Rate(), Average() and Field().
        /// </summary>
        protected void OutputFields(params string[] fields)
        {
            var parts = new System.Collections.Generic.List<string>();
            foreach (var f in fields)
            {
                if (f != null) parts.Add(f);
            }
            parts.Add(Field("interval_s", Num(recordInterval, 3)));
            Output(string.Join(", ", parts));
        }

        /// <summary>
        /// A rate field (count per second over the interval of this record), or null when that interval
        /// is 0, so the rate would be NaN or infinite (#89).
        /// </summary>
        protected string Rate(string field, double count, string format = "F2")
        {
            if (recordInterval <= 0) return null;
            return $"{field}={(count / recordInterval).ToString(format, CultureInfo.InvariantCulture)}";
        }

        /// <summary>
        /// An average field (sum / divisor), or null when the divisor is 0 (#89).
        /// </summary>
        protected static string Average(string field, double sum, double divisor, string format = "F2")
        {
            if (divisor == 0) return null;
            return $"{field}={(sum / divisor).ToString(format, CultureInfo.InvariantCulture)}";
        }

        /// <summary>
        /// A plain field. Numbers are formatted independent of the locale.
        /// </summary>
        protected static string Field(string field, object value)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}={1}", field, value);
        }

        /// <summary>
        /// A number with a fixed number of decimals, independent of the locale ("12.34", never "12,34",
        /// which would break the name=value, name=value format). For one-shot records:
        /// Statistics.Output(name, $"latency_ms={Statistics.Num(latency, 1)}").
        /// </summary>
        public static string Num(double value, int decimals = 2)
        {
            return value.ToString("F" + decimals, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Output a single record for the Statistics object.
        /// The parameter should only contain comma-separated name=value pairs, the common fields are automatically added.
        /// </summary>
        /// <param name="s">The statistics to output</param>
        protected void Output(string s)
        {
            Output(name, s);
        }

        static object lockObj = new object();
        static int seq = 0;

        /// <summary>
        /// Output a one-shot statistics line.
        /// This is used most often to show mappings between things, for example how ts= timestamps map to NTP clocks,
        /// or which renderer component uses which preparer component. The standard fields are automatically included.
        /// </summary>
        /// <param name="name">Name of the component outputting the record</param>
        /// <param name="s">The name=value string</param>
        public static void Output(string name, string s)
        {
            if (muted) return;
            if (!initialized) Initialize();
            lock (lockObj)
            {
                seq++;
                string statsLine = string.Format(System.Globalization.CultureInfo.InvariantCulture, "stats: seq={0}, ts={1:F3}, component={2}, {3}", seq, System.DateTime.Now.TimeOfDay.TotalSeconds, name, s);
                if (statsStream == null)
                {
                    Debug.Log(statsLine);
                }
                else
                {
                    statsStream.WriteLine(statsLine);
                    Flush();
                }
            }
        }
    }
#endif
}
