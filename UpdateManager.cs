using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

/// <summary>
///UpdateNewVersion.dll  is responsible for updating app version from FTP.
///You need to get the server information and app name(optional).
///There are two options:
///1. Use from the Loader.exe file
///2. Use as an automatic update from the app itself
/// </summary>
namespace UpdateNewVersion
{
    public enum Color
    {
        GREEN,
        RED,
        YELLOW,
        DEFAULT
    }
    public sealed class UpdateManager
    {
        private string mLocalVersion;
        private string mUpdaterlVersion;//ftp version
        private Updater mUpdater;
        private string mApplicationPath;

        private static readonly UpdateManager instance = new UpdateManager();

        private string mAppName;

        private UpdateManager() { }

        public static UpdateManager Instance
        {
            get { return instance; }
        }

        #region Get Version
        /// <summary>
        /// Get app version while running this dll from the app itself(automatic update)
        /// </summary>
        /// <returns></returns>
        private string GetCurrentAssemblyVersion()
        {
            string ver = string.Empty;
            try
            {
                ver = Assembly.GetEntryAssembly().GetName().Version.ToString();
                return ver;
            }
            catch (Exception e)
            {
                Trace.WriteLine("Error in GetCurrentAssemblyVersion: " + e.Message);
                UpdateManager.WriteToConsole("Error in GetCurrentAssemblyVersion: " + e.Message, Color.RED);
                ver = string.Empty;
            }
            return ver;
        }
        /// <summary>
        /// Get app version while running from Loader.exe
        /// </summary>
        /// <returns></returns>
        private string GetAssemblyVersion()
        {
            string version = string.Empty;
            try
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(mApplicationPath);
                version = versionInfo.ProductVersion;
            }
            catch (Exception e)
            {
                Trace.WriteLine("Error in GetCurrentAssemblyVersion: " + e.Message);
                UpdateManager.WriteToConsole("Error in GetCurrentAssemblyVersion: " + e.Message,Color.RED);
                version = string.Empty;
            }
            return version;
        }
        /// <summary>
        /// Get app version from ftp server
        /// </summary>
        /// <returns></returns>
        private string GetFtpAssemblyVersion()
        {
            string ver = mUpdater.GetFtpAssemblyVersion();
            return ver;
        }
        #endregion

        #region Get Path
        /// <summary>
        /// Get app path while running this dll from the app itself(automatic update)
        /// </summary>
        /// <returns></returns>
        private string GetCurrentApplicationPath()
        {
            string result;
            try
            {
                result = Process.GetCurrentProcess().MainModule.FileName;
            }
            catch (Exception e)
            {
                UpdateManager.WriteToConsole("Error in GetCurrentApplicationPath: " + e.Message,Color.RED);
                result = string.Empty;
            }
            return result;
        }
        /// <summary>
        /// Get app path while running from Loader.exe
        /// </summary>
        /// <returns></returns>
        private string GetApplicationPath()
        {
            string result;
            try
            {
                string dir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
                result = dir + "\\" + mAppName;
            }
            catch (Exception e)
            {
                result = string.Empty;
            }
            return result;
        }
        #endregion

        #region Update
        /// <summary>
        /// Compare between local and ftp application version
        /// </summary>
        /// <returns></returns>
        private bool ShouldBeUpdate()
        {
            bool shouldUpdate = !string.IsNullOrEmpty(mLocalVersion)
                && !string.IsNullOrEmpty(mUpdaterlVersion)
                && mLocalVersion != mUpdaterlVersion;
            UpdateManager.WriteToConsole($"Current Version: {mLocalVersion}",Color.YELLOW);
            UpdateManager.WriteToConsole($"Ftp Version: {mUpdaterlVersion}",Color.YELLOW);
            return shouldUpdate;
        }
        /// <summary>
        /// If necessary, update application version from ftp.
        /// </summary>
        /// <param name="ftp">FTP server details - mandatory</param>
        /// <param name="appName">app name - optional</param>
        /// <returns></returns>
        public bool UpdateAssemblyVersion(FtpDetails ftp, string appName = "")
        {
            FillDetails(ftp, appName);
            bool updated = false;
            if (updated = ShouldBeUpdate())//ftp has different verion
            {
                UpdateManager.WriteToConsole("Start to update...",Color.YELLOW);
                if (updated = UpdateVersion())
                {
                    UpdateManager.WriteToConsole("Restart...",Color.YELLOW);
                    updated = RestartApplication();
                }
            }
            return updated;
        }
        private bool UpdateVersion()
        {
            return mUpdater.UpdateVersion();
        }
        private void FillDetails(FtpDetails ftp, string appName)
        {
            if (appName == string.Empty)//UpdateNewVersion automatically(run the dll from the app itself)
            {
                mApplicationPath = GetCurrentApplicationPath();
                mUpdater = new Updater(ftp);
                mUpdater.ExeFilePath = mApplicationPath;
                mLocalVersion = GetCurrentAssemblyVersion();
            }
            else//UpdateNewVersion from loader(external Loader.exe file)
            {
                mAppName = appName;
                if (Path.GetExtension(appName) != ".exe")
                    mAppName += ".exe";
                mApplicationPath = GetApplicationPath();
                mUpdater = new Updater(ftp);
                mUpdater.ExeFilePath = mApplicationPath;
                mLocalVersion = GetAssemblyVersion();
            }
            mUpdaterlVersion = GetFtpAssemblyVersion();
        }
        #endregion

        #region Start\Restart
        /// <summary>
        /// In case the updae success we start\restart the applicarion.
        /// Automatic mode - will run the new version and then will close the current app.
        /// Loader mode - will run the new version.
        /// </summary>
        /// <returns></returns>
        private bool RestartApplication()
        {
            bool success = true;
            try
            {
                string processName = Path.GetFileNameWithoutExtension(mApplicationPath);
                string processLocation = Path.GetDirectoryName(mApplicationPath);
                bool isRunning = Process.GetProcessesByName(processName).FirstOrDefault(p => p.MainModule.FileName.StartsWith(processLocation + "\\" + processName)) != default(Process);
                if (isRunning)
                {
                    Process[] appsProcess = Process.GetProcessesByName(processName);
                    Process desiredProcess = appsProcess.Where(p => p.MainModule.FileName.StartsWith(Path.GetDirectoryName(processLocation))).FirstOrDefault();
                    int currentProcessId = Process.GetCurrentProcess().Id;
                    if (desiredProcess.Id == currentProcessId)
                    {
                        Process.Start(mApplicationPath);//run new exe file(for debug mode .Replace(".vshost", ""))
                        Thread.Sleep(2000);
                        Process proc = Process.GetProcessById(currentProcessId);
                        Environment.Exit(0);
                        proc.Kill();
                    }
                    else//loader.exe
                    {
                        UpdateManager.WriteToConsole($"Kill process: {processName}",Color.YELLOW);
                        desiredProcess.Kill();
                        Process.Start(mApplicationPath);//run new exe file(for debug mode .Replace(".vshost", ""))
                    }
                }
                else
                {
                    Process.Start(mApplicationPath);//run new exe file(for debug mode .Replace(".vshost", ""))
                }
            }
            catch (Exception e)
            {
                Trace.Write("Error in RestartApplication: " + e.Message);
                UpdateManager.WriteToConsole("Error in RestartApplication: " + e.Message,Color.RED);
                success = false;
            }
            return success;
        }
        #endregion

        public static void WriteToConsole(string message, Color color)
        {
            var col = Console.ForegroundColor;

            switch(color)
            {
                case Color.GREEN:
                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                    break;
                case Color.RED:
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    break;
                case Color.YELLOW:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                case Color.DEFAULT:
                    break;
                default:
                    break;
            }
            Console.WriteLine(message);
            Console.ForegroundColor = col;
            
        }
	}
}
