﻿using System;
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
            int interval = 0;

            if (args.Length >= 1)
            {
                process_set_id = args[0];
            }
            if (args.Length >= 2)
            {
                interval = Int32.Parse(args[1]);
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
                            writer.WriteLine(GetThreadDump(p.Id));
                        }
                    }
                }
                sw.Stop();
                Console.WriteLine(DateTime.Now.ToString() + " " + sw.ElapsedMilliseconds);
                // wait at least twice as long as it took to process
                // try to keep up with the defined interval
                Thread.Sleep(Math.Max(interval * 1000 - (int)sw.ElapsedMilliseconds, 2* (int)sw.ElapsedMilliseconds));
            } while (interval > 0);
        }

        

        public static string GetThreadDump(int pid)
        {
            using (StringWriter writer = new StringWriter())
            {

                using (var dataTarget = DataTarget.AttachToProcess(pid, 5000, AttachFlag.Passive))
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

                        foreach (var frame in t.EnumerateStackTrace())
                        {
                            writer.WriteLine("\t" + frame.ToString());
                        }
                        writer.WriteLine();
                    }
                }
                return writer.ToString();
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
