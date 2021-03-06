﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Collections;
using System.Management;
using log4net;
using Microsoft.VisualBasic.Devices;
using BoxedIce.ServerDensity.Agent.PluginSupport;

namespace BoxedIce.ServerDensity.Agent.Checks
{
    public class PerformanceCounterBasedProcessCheck : ProcessCheck
    {
        public PerformanceCounterBasedProcessCheck() : base()
        {
            // collection for supported languages
            _names = new Dictionary<string, IList<string>>();

            // english
            _names.Add("Process", new List<string>());
            _names["Process"].Add("ID Process");
            _names["Process"].Add("% Processor Time");
            _names["Process"].Add("Working Set");

            // italian
            _names.Add("Processo", new List<string>());
            _names["Processo"].Add("ID processo");
            _names["Processo"].Add("% Tempo processore");
            _names["Processo"].Add("Working set");

        }

        public override object DoCheck()
        {

            // performance category vars
            PerformanceCounterCategory category = null;
            string effectiveKey = null;

            // get list of all categories to compare to I18N
            PerformanceCounterCategory[] availableCategories = PerformanceCounterCategory.GetCategories();

            // loop over all available performance categories
            foreach (PerformanceCounterCategory perfcat in availableCategories)
            {
                // loop over all I18N's
                foreach (string key in _names.Keys)
                {
                    if (key == perfcat.CategoryName)
                    {
                        category = new PerformanceCounterCategory(key);
                        effectiveKey = key;
                        break;
                    }
                }
            }
            
            string[] names = category.GetInstanceNames();
            var results = new ArrayList();
            ArrayList sysProcesses = new ArrayList(3) { "System", "Idle", "_Total", "logon.scr" };
            
            foreach (string name in names)
            {
                string processName = (sysProcesses.Contains(name)) ? name : name + ".exe";
                float pid = new PerformanceCounter(effectiveKey, _names[effectiveKey][0], name).NextValue();
                float cpuPercentage = new PerformanceCounter(effectiveKey, _names[effectiveKey][1], name).NextValue();
                float workingSet = new PerformanceCounter(effectiveKey, _names[effectiveKey][2], name).NextValue();
                decimal memoryPercentage = Decimal.Round(((decimal)workingSet / (decimal)_totalMemory * 100), 2);
                string fullUserName = GetProcessOwner(pid);

                results.Add(new object[] { pid, processName, fullUserName, cpuPercentage, memoryPercentage, workingSet });

            }

            // flag check
            if (Agent.Flags.ContainsKey("ProcessCheck"))
            {
                bool perf = false;
                bool wmi = false;
                foreach (string name in names)
                {
                    string processName = (sysProcesses.Contains(name)) ? name : name + ".exe";
                    if (processName == Agent.Flags["ProcessCheck"].ToString())
                        perf = true;
                }
                wmi = ProcessCheck.IsProcessRunning(Agent.Flags["ProcessCheck"].ToString());
                if (perf != wmi)
                {
                    Log.Error("Process Check: '" + Agent.Flags["ProcessCheck"] + "' process is running as Perf Counter (" + perf.ToString() + ") and WMI (" + wmi.ToString() + ").");
                }
            }

            return results;
        }

        private string GetProcessOwner(float processId)
        {
            try
            {
                using (ManagementObjectSearcher query = new ManagementObjectSearcher(string.Format("SELECT * FROM Win32_Process WHERE ProcessID = {0}", processId)))
                {
                    foreach (ManagementObject process in query.Get())
                    {
                        var outParameters = process.InvokeMethod("GetOwner", null, null);
                        if (outParameters["User"] != null)
                        {
                            return string.Format(@"{0}\{1}", outParameters["Domain"], outParameters["User"]);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            return string.Empty;
        }

        protected override ulong TotalMemory()
        {
            try
            {
                return new ComputerInfo().TotalPhysicalMemory;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            return 0;
        }

        private static readonly ILog Log = LogManager.GetLogger(typeof(PerformanceCounterBasedProcessCheck));
        private IDictionary<string, IList<string>> _names;

        public static bool IsProcessRunning(string processName)
        {
            bool found = false;

            // assumption of english
            PerformanceCounterCategory category = new PerformanceCounterCategory("Process");
            PerformanceCounterCategory[] availableCategories = PerformanceCounterCategory.GetCategories();
            string[] names = category.GetInstanceNames();
            foreach (string name in names)
            {
                if(processName == name + ".exe")
                    found = true;
            }

            return found;
        }
    }
}
