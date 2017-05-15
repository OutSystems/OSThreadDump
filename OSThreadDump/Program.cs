using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Diagnostics.Runtime;
using System.Diagnostics;
using System.Threading;

namespace OSThreadDump
{
    class Program
    {
        static Dictionary<string, List<string>> process_sets = new Dictionary<string, List<string>>()
        {
            { "all",  new List<string> { "LogServer.exe", "DeployService.exe", "CompilerService.exe", "Scheduler.exe", "SMSConnector.exe", "w3wp.exe" } },
            { "iis",  new List<string> { "w3wp.exe" } },
            // currently disabled because of arch differences (32bit vs 64)
            // { "devtools",  new List<string> { "ServiceStudio.exe", "IntegrationStudio.exe" } },
            { "services",  new List<string> { "LogServer.exe", "DeployService.exe", "CompilerService.exe", "Scheduler.exe", "SMSConnector.exe" } }
        };
        static void Main(string[] args)
        {
            string process_set_id = "all";
            AttachFlag mode = AttachFlag.Passive;
            int interval = 0;

            if (args.Length >= 1)
            {
                process_set_id = args[0];
            }
            if (args.Length >= 2)
            {
                interval = Int32.Parse(args[1]);
            }

            if (args.Length >= 3)
            {
                switch (args[2])
                {
                    case "-Passive":
                        mode = AttachFlag.Passive;
                        break;
                    case "-NonInvasive":
                        mode = AttachFlag.NonInvasive;
                        break;
                    case "-Invasive":
                        mode = AttachFlag.Invasive;
                        break;
                    default:
                        Console.WriteLine("Invalid attach mode. Valid modes: -Passive -NonInvasive -Invasive");
                        return;
                }
            }

            if (!process_sets.ContainsKey(process_set_id))
            {
                Console.WriteLine("Must specify valid process set: all iis services");
                return;
            }

            List<string> process_set = process_sets[process_set_id];
            do
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();
                using (TextWriter writer = new StreamWriter(File.Create("threads_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log")))
                {
                    writer.WriteLine(DateTime.Now.ToString());

                    foreach (var p in Process.GetProcesses())
                    {
                        string filename = GetProcessFilename(p);
                        if (process_set.Any(f => filename.EndsWith(f)))
                        {
                            writer.WriteLine(p.Id + " " + p.ProcessName + " " + filename);
                            writer.WriteLine(GetThreadDump(p.Id, mode));
                        }
                    }
                }
                sw.Stop();
                // wait at least twice as long as it took to process
                // try to keep up with the defined interval
                Thread.Sleep(Math.Max(interval * 1000 - (int)sw.ElapsedMilliseconds, 2*(int)sw.ElapsedMilliseconds));
            } while (interval > 0);
        }



        public static string GetThreadDump(int pid, AttachFlag mode)
        {
            using (StringWriter writer = new StringWriter())
            {
                try {
                    using (var dataTarget = DataTarget.AttachToProcess(pid, 5000, mode))
                    {
                        writer.WriteLine(dataTarget.ClrVersions.First().Version);
                        var runtime = dataTarget.ClrVersions.First().CreateRuntime();

                        foreach (var domain in runtime.AppDomains)
                        {
                            writer.WriteLine("Domain " + domain.Name);
                        }
                        writer.WriteLine();
                        foreach (var t in runtime.Threads)
                        {
                            if (!t.IsAlive)
                                continue;

                            if (t.StackTrace.Count == 0)
                                continue;

                            writer.WriteLine("Thread " + t.ManagedThreadId + ": ");
                            int loop_count = 0;
                            foreach (var frame in t.EnumerateStackTrace())
                            {
                                writer.WriteLine("\t" + frame.StackPointer.ToString("x16") + " " + frame.ToString());
                                loop_count++;
                                if (loop_count > 200)
                                {
                                    writer.WriteLine("\t[CORRUPTED]");
                                    break;
                                }
                            }
                            writer.WriteLine();
                        }
                    }
                    return writer.ToString();
                } catch
                {
                    // This is mostly to catch the "invalid architecture" error.
                    // Any error that happens we want to ignore and return what we have.
                    return writer.ToString();
                }
            }
        }


        public static string GetProcessFilename(Process p)
        {
            try
            {
                return p.Modules[0].FileName;
            }
            catch
            {
                return "";
            }
        }
    }
}
