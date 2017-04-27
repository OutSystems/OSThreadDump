using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Diagnostics.Runtime;
using System.Diagnostics;

namespace OSThreadDump
{
    class Program
    {
        static void Main(string[] args)
        {
            foreach (var p in Process.GetProcesses())
            {
                string filename = GetProcessFilename(p);
                if (filename.EndsWith("w3wp.exe"))
                {
                    Console.WriteLine(p.Id + " " + p.ProcessName + " " + filename);
                    Console.WriteLine(GetThreadDump(p.Id));
                }
            }
        }

        public static string GetThreadDump(int pid)
        {
            StringWriter writer = new StringWriter();

            using (var dataTarget = DataTarget.AttachToProcess(pid, 5000, AttachFlag.Passive))
            {
                // Console.WriteLine(dataTarget.ClrVersions.First().DacInfo.FileName);
                // Console.WriteLine(dataTarget.ClrVersions.First().Version);
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
