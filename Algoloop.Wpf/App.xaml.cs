﻿/*
 * Copyright 2018 Capnode AB
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); 
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Algoloop.Model;
using Algoloop.Support;
using Algoloop.Wpf.ViewModel;
using Microsoft.Win32;
using QuantConnect.Configuration;
using QuantConnect.Logging;
using QuantConnect.Util;
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;

namespace Algoloop
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private const uint _esContinous = 0x80000000;
        private const uint _esSystemRequired = 0x00000001;
//        private const uint _esDisplayRequired = 0x00000002;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint SetThreadExecutionState([In] uint esFlags);

        public App()
        {
            // Set working directory to exe directory
            string unc = Assembly.GetExecutingAssembly().Location;
            string folder = Path.GetDirectoryName(unc);
            Directory.SetCurrentDirectory(folder);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            string folder = MainService.GetProgramDataFolder();
            string path = Path.Combine(folder, "init.log");
            File.WriteAllText(path, $"{DateTime.Now}:>OnStartup\n");
            File.AppendAllText(path, $"{DateTime.Now}:ProgramFolder={MainService.GetProgramFolder()}\n");
            File.AppendAllText(path, $"{DateTime.Now}:AppDataFolder={MainService.GetAppDataFolder()}\n");
            File.AppendAllText(path, $"{DateTime.Now}:ProgramDataFolder={MainService.GetProgramDataFolder()}\n");
            File.AppendAllText(path, $"{DateTime.Now}:UserDataFolder={MainService.GetUserDataFolder()}\n");

            // Set Log handler
            Log.DebuggingEnabled = Config.GetBool("debug-mode", false);
            Log.DebuggingLevel = Config.GetInt("debug-level", 1);
            Log.LogHandler = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("Algoloop.Wpf.Lean.LogItemHandler");
            string message = $"Startup \"{AboutModel.AssemblyProduct}\"";
            Log.Trace(message);

            // Exception Handling Wiring
            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
            TaskScheduler.UnobservedTaskException += UnobservedTaskExceptionHandler;

            Algoloop.Wpf.Properties.Settings.Default.Reload();
            EnsureBrowserEmulationEnabled("Algoloop.exe");

            // Prevent going to sleep mode
            _ = SetThreadExecutionState(_esContinous | _esSystemRequired);
            File.AppendAllText(path, $"{DateTime.Now}:<OnStartup\n");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Enable sleep mode
            _ = SetThreadExecutionState(_esContinous);

            ViewModelLocator.ResearchViewModel.StopJupyter();
            ViewModelLocator.MainViewModel.SaveConfig();
            Algoloop.Wpf.Properties.Settings.Default.Save();

            Log.Trace($"Exit \"{AboutModel.AssemblyProduct}\"");
            base.OnExit(e);
        }

        private void UnobservedTaskExceptionHandler(object sender, UnobservedTaskExceptionEventArgs e)
        {
            var message = nameof(UnobservedTaskExceptionHandler);
            e?.SetObserved(); // Prevents the Program from terminating.

            if (e.Exception != null && e.Exception is Exception tuex)
            {
                message = string.Format(CultureInfo.InvariantCulture, "{0} Exception: {1}", message, tuex.Message);
                Log.Error(tuex, message);
            }
            else if (sender is Exception ex)
            {
                message = string.Format(CultureInfo.InvariantCulture, "{0} Exception: {1}", message, ex.Message);
                Log.Error(ex, message);
            }
        }

        private void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            var message = nameof(UnhandledExceptionHandler);
            if (e.ExceptionObject != null && e.ExceptionObject is Exception uex)
            {
                message = string.Format(CultureInfo.InvariantCulture, "{0} Exception: {1}", message, uex.Message);
                Log.Error(uex, message);
            }
            else if (sender is Exception ex)
            {
                message = string.Format(CultureInfo.InvariantCulture, "{0} Exception: {1}", message, ex.Message);
                Log.Error(ex, message);
            }
        }

        private void DispatcherUnhandledExceptionHandler(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            var message = nameof(DispatcherUnhandledExceptionHandler);
            if (e.Exception != null && e.Exception is Exception uex)
            {
                message = string.Format(CultureInfo.InvariantCulture, "{0} Exception: {1}", message, uex.Message);
                Log.Error(uex, message);
            }
            else if (sender is Exception ex)
            {
                message = string.Format(CultureInfo.InvariantCulture, "{0} Exception: {1}", message, ex.Message);
                Log.Error(ex, message);
            }
        }

        /// <summary>
        /// WebBrowser Internet Explorer 11 emulation
        /// </summary>
        public static void EnsureBrowserEmulationEnabled(string exename, bool uninstall = false)
        {
            RegistryKey rk = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION", true);
            if (rk == null) return;
            if (uninstall)
            {
                rk.DeleteValue(exename);
            }
            else
            {
                dynamic value = rk.GetValue(exename);
                if (value == null)
                    rk.SetValue(exename, (uint)11001, RegistryValueKind.DWord);
            }
        }
    }
}
