using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MeshFlowViewer
{
    public class Timer
    {
        private DateTime starttime;

        public Timer()
        {
            Reset();
        }
        public void Reset() { starttime = DateTime.Now; }

        public String Elapsed()
        {
            return (DateTime.Now - starttime).ToString();
        }

        public String ElapsedNoSubseconds()
        {
            String sElapsed = (DateTime.Now - starttime).ToString();
            int tickat = sElapsed.IndexOf('.');
            if (tickat == -1) return sElapsed;
            return sElapsed.Substring(0, tickat); // remove tick count
        }

        public String EstimateDone(float percentDone)
        {
            if (percentDone < float.Epsilon) return "??:??:??";
            TimeSpan tsElapsed = DateTime.Now - starttime;
            double dEstTotalSeconds = tsElapsed.TotalSeconds / (double)percentDone;
            DateTime dtEstDone = starttime.AddSeconds(dEstTotalSeconds);
            String sEstDone = (dtEstDone - starttime).ToString();
            int tickat = sEstDone.IndexOf('.');
            if (tickat == -1) return sEstDone;
            return sEstDone.Substring(0, tickat); // remove tick count
        }

        public double ElapsedMilliseconds()
        {
            return (DateTime.Now - starttime).TotalMilliseconds;
        }

        public double ElapsedSeconds()
        {
            return (DateTime.Now - starttime).TotalSeconds;
        }

        public double ElapsedMinutes()
        {
            return (DateTime.Now - starttime).TotalMinutes;
        }

        public static long GetExecutionTime_ms(Action func)
        {
            DateTime starttime = DateTime.Now;
            func();
            DateTime endtime = DateTime.Now;
            TimeSpan ts = endtime - starttime;
            return (long)ts.TotalMilliseconds;
        }

        private static int nestedtimings = 0;
        private static string printpad = "    ";
        public static void PrintTimeToExecute(string label, Action func)
        {
            nestedtimings++;
            DateTime starttime = DateTime.Now;
            func();
            DateTime endtime = DateTime.Now;
            TimeSpan ts = endtime - starttime;
            nestedtimings = Math.Max(0, nestedtimings - 1);

            for (int i = 0; i < nestedtimings; i++) System.Console.Write(printpad);
            System.Console.WriteLine("{0}:\t{1}", label, ts.ToString());
        }
        public static T PrintTimeToExecute<T>(string label, Func<T> func)
        {
            nestedtimings++;
            DateTime starttime = DateTime.Now;
            T val = func();
            DateTime endtime = DateTime.Now;
            TimeSpan ts = endtime - starttime;
            nestedtimings = Math.Max(0, nestedtimings - 1);

            for (int i = 0; i < nestedtimings; i++) System.Console.Write(printpad);
            System.Console.WriteLine("{0}:\t{1}", label, ts.ToString());

            return val;
        }
    }
}
