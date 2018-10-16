using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;

namespace UpdateNewVersion
{
	class Updater
	{
        public Updater(FtpDetails ftp)
        {
            ADDRESS = ftp.Address;
            USER = ftp.User;
            PASS = ftp.Password;
            excludeExtensionsList = ftp.ExcludeExtention;
        }

        /// <summary>
        /// This function will execute all update process.
        /// Get files from ftp, clean up, backup, manage oldversion, download.
        /// If something went worng it will execute rollback.
        /// </summary>
        /// <returns></returns>
		public bool UpdateVersion()
        {
            bool success = true;
            try
            {
                UpdateManager.WriteToConsole("Get Files from FTP",Color.YELLOW);
                List<string> files = GetFTPFiles(excludeExtensionsList);
                if (success = (files != null && files.Count > 0))//get files from ftp
                {
                    UpdateManager.WriteToConsole("Clean Up old version",Color.YELLOW);
                    if (success = CleanUP(files))//clean _OldVersion files from loacl
                    {
                        UpdateManager.WriteToConsole($"Backup and change current files to {oldVeriosn}",Color.YELLOW);
                        if (success = ChangeAndBackUpOldFiles(mBasePathToSave, files))//create backup files and change local files to _OldVersion
                        {
                            UpdateManager.WriteToConsole("Download files from FTP",Color.YELLOW);
                            success = DownloadFtpFiles(files);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine("Error in CopyFilesFromFTP: " + e.Message);
                UpdateManager.WriteToConsole("Error in CopyFilesFromFTP: " + e.Message,Color.RED);
                success = false;
                RollBack();
            }
            if (success == false)
            {
                RollBack();
            }
            return success;
        }
       
        #region FTP Members
        private readonly string ADDRESS;
        private readonly string USER ;
        private readonly string PASS ;
        private  List<string> excludeExtensionsList = new List<string>();
        #endregion

        #region Const File Extention 
        private const string backup = "_backup";
		private const string oldVeriosn = "_OldVersion";
		private const string exeExtention = ".exe";
        #endregion

        #region File Path Members
        private string mExeFilePath;
		private string mExeFileName;
		private string mFilePathToSave;//need to download temporary exe file for getting ftp version
		private string mBasePathToSave;//files folder 
        private string mDirectory;//directory in FTP should be the same as exe file name
		public string ExeFilePath
		{
			 get {	return mExeFilePath; }
			 set
			 {
				mExeFilePath = value;
				int lastIndex = mExeFilePath.LastIndexOf('\\');
				string fileNameWithExtention = mExeFilePath.Substring(lastIndex + 1);
				mDirectory = Path.GetFileNameWithoutExtension(fileNameWithExtention);
				//mDirectory = fileNameWithExtention.Remove(fileNameWithExtention.IndexOf('.'));for testing .vshost.exe
				mExeFileName =  $"{mDirectory}{exeExtention}";
				mBasePathToSave = $"{mExeFilePath.Substring(0, lastIndex)}\\";
				mFilePathToSave = $"{backup}{mExeFileName}";
			}
		}
        #endregion
       
        #region CleanUp & BackUp & OldVersion
        /// <summary>
        /// Copy the files from current location to backup folder.
        /// Change the current files names to _OldVersion so we could import 
        /// the new files from ftp while running .
        /// </summary>
        /// <param name="path"></param>
        /// <param name="files"></param>
        /// <returns></returns>
        private bool ChangeAndBackUpOldFiles(string path, List<string> files)
        {
            mMode = MODE.BACKUP_OLDVERSION;
            bool success = true;
            try
            {
                string toPath = path;
                string fromPath = path;
                string[] backUpDirs = Directory.GetDirectories(toPath, $"{backup}", SearchOption.TopDirectoryOnly);
                for (int i = 0; i < backUpDirs.Length; i++)
                {
                    Directory.Delete(backUpDirs[0], true);
                }
                string backUpFolder = backup;
                toPath += backUpFolder;
                Directory.CreateDirectory(toPath);
                foreach (string file in files)
                {
                    string fromLocal = $"{fromPath}{file}";
                    string toBackUp = $"{toPath}\\{file}";

                    FileStatus fs = mFileStatusList.Where(f => f.Name == file).FirstOrDefault();
                    if (File.Exists(fromLocal))
                    {
                        UpdateFileStatus(ref fs, Status.EXISTS, true);
                        File.Copy(fromLocal, toBackUp);//backup
                        UpdateManager.WriteToConsole($"{file} backup",Color.DEFAULT);
                        UpdateFileStatus(ref fs, Status.BACKUP, true);
                        //Change file name so we could replace it with the new file from ftp
                        string changePath = ChangeFilePath(fromLocal);
                        if (changePath != string.Empty)
                        {
                            File.Move(fromLocal, changePath);//oldversion
                            UpdateManager.WriteToConsole($"{file} {oldVeriosn}",Color.DEFAULT);
                            UpdateFileStatus(ref fs, Status.OLDVERSION, true);
                        }
                    }
                    else
                    {
                        UpdateManager.WriteToConsole($"{file} doesnt exists in local. Continue to the next file",Color.DEFAULT);
                        UpdateFileStatus(ref fs, Status.EXISTS, false);
                    }
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine("Error in BackUpOldFiles: " + e.Message);
                UpdateManager.WriteToConsole("Error in BackUpOldFiles: " + e.Message,Color.RED);
                success = false;
            }
            return success;
        }

        /// <summary>
        /// Change file name to _OldVersion
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static string ChangeFilePath(string filePath)
        {
            string newFilePath = string.Empty;
            try
            {
                string appFolder = Path.GetDirectoryName(filePath);
                string appName = Path.GetFileNameWithoutExtension(filePath);
                string appExtension = Path.GetExtension(filePath);
                newFilePath = Path.Combine(appFolder, appName + oldVeriosn + appExtension);
            }
            catch (Exception e)
            {
                Trace.WriteLine("Error in ChangeFileName: " + e.Message);
                UpdateManager.WriteToConsole("Error in ChangeFileName: " + e.Message,Color.RED);
                newFilePath = string.Empty;
            }
            return newFilePath;
        }
        /// <summary>
        /// Delete all _OldVersion files from local
        /// </summary>
        /// <param name="files"></param>
        /// <returns></returns>
        private bool CleanUP(List<string> files)
        {
            mMode = MODE.CLEANUP;
            bool success = true;
            try
            {
                foreach (string file in files)
                {
                    UpdateManager.WriteToConsole($"{file}",Color.DEFAULT);
                    string checkOldVersionPath = ChangeFilePath(mBasePathToSave + file);
                    if (checkOldVersionPath != string.Empty &&
                        File.Exists(checkOldVersionPath))
                    {
                        File.Delete(checkOldVersionPath);
                    }
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine("Error in CleanUp: " + e.Message);
                UpdateManager.WriteToConsole("Error in CleanUp: " + e.Message,Color.RED);
                success = false;
            }
            return success;
        }

        #endregion

        #region Stauts
        private void UpdateFileStatus(ref FileStatus fs, Status status, bool b)
        {
            switch (status)
            {
                case Status.BACKUP:
                    fs.IsBackUp = b;
                    break;
                case Status.DOWNLOAD:
                    fs.IsDownload = b;
                    break;
                case Status.OLDVERSION:
                    fs.IsOldVersion = b;
                    break;
                case Status.EXISTS:
                    fs.IsExistLocal = b;
                    break;
            }
        }
        #endregion

        #region Get Version
        public string GetFtpAssemblyVersion()
        {
            string version = string.Empty;
            try
            {
                string fromPath = $"{ADDRESS}/{mDirectory}/{mExeFileName}";
                string toPath = $"{mBasePathToSave}{mFilePathToSave}";
                bool success = DownloadExeFileFromFTP(fromPath, toPath);
                if (success)
                {
                    var versionInfo = FileVersionInfo.GetVersionInfo(toPath);
                    version = versionInfo.ProductVersion;
                    if (File.Exists(toPath))
                        File.Delete(toPath);
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine("Error in GetFtpAssemblyVersion: " + e.Message);
                UpdateManager.WriteToConsole("Error in GetFtpAssemblyVersion: " + e.Message,Color.RED);
                version = string.Empty;
            }
            return version;
        }
        #endregion

        #region FTP 

        /// <summary>
        /// Files will be download from ftp server only if:
        /// 1. create backup
        /// 2. manage old veriosn
        /// Otherwise the program will do rollback.
        /// If file doesnt exists in local the program will continue to the next file.
        /// </summary>
        /// <param name="files"></param>
        /// <returns></returns>
        private bool DownloadFtpFiles(List<string> files)
        {
            mMode = MODE.DOWNLOAD;
            bool success = true;
            try
            {
                string fromPath = $"{ADDRESS}{mDirectory}";
                using (WebClient ftpClient = new WebClient())
                {
                    ftpClient.Credentials = new System.Net.NetworkCredential(USER, PASS);
                    for (int i = 0; i < files.Count; i++)
                    {
                        FileStatus fs = mFileStatusList.Where(f => f.Name == files[i]).FirstOrDefault();
                        UpdateManager.WriteToConsole($"{fs.Name}:Backup - {fs.IsBackUp}, OldVer - {fs.IsOldVersion}",Color.DEFAULT);
                        if (fs.IsExistLocal)
                        {
                            if (!(success = (fs.IsBackUp && fs.IsOldVersion)))//file must be backup and has OldVersion before download
                            {
                                UpdateManager.WriteToConsole($"{fs.Name} will not be download.[backup - {fs.IsBackUp}, oldversion - {fs.IsOldVersion}, exists in local - {fs.IsExistLocal}]",Color.DEFAULT);
                                break;
                            }
                            string fileFtpPath = $"{fromPath}/{files[i].ToString()}";
                            string toPath = mBasePathToSave + files[i].ToString();
                            ftpClient.DownloadFile(fileFtpPath, toPath);
                            UpdateManager.WriteToConsole($"download file {files[i]} ended successfully",Color.DEFAULT);
                            UpdateFileStatus(ref fs, Status.DOWNLOAD, true);
                        }
                        else
                        {
                            UpdateManager.WriteToConsole($"{fs.Name} will not be download.[file exists in local - {fs.IsExistLocal}]",Color.DEFAULT);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine("Error in DownloadFtpFiles: " + e.Message);
                UpdateManager.WriteToConsole("Error in DownloadFtpFiles: " + e.Message,Color.RED);
                success = false;
            }
            return success;
        }

        private bool DownloadExeFileFromFTP(string fromPath, string toPath)
        {
            bool success = true;
            try
            {
                WebClient client = new WebClient();
                client.Credentials = new NetworkCredential(USER, PASS);
                client.DownloadFile(fromPath, toPath);
            }
            catch (Exception e)
            {
                Trace.WriteLine("Error in DownloadExeFileFromFTP: " + e.Message);
                UpdateManager.WriteToConsole("Error in DownloadExeFileFromFTP: " + e.Message,Color.RED);
                success = false;
            }
            return success;
        }

        /// <summary>
        /// Get List of ftp files except from excludeExtensionsList
        /// </summary>
        /// <param name="excludeExtensionsList"></param>
        /// <returns></returns>
        private List<string> GetFTPFiles(List<string> excludeExtensionsList = null)
        {
            mMode = MODE.GETFILES;
            List<string> files = null;
            try
            {
                string fromPath = $"{ADDRESS}{mDirectory}";
                FtpWebRequest ftpRequest = (FtpWebRequest)WebRequest.Create(fromPath);
                ftpRequest.Credentials = new NetworkCredential(USER, PASS);
                ftpRequest.Method = WebRequestMethods.Ftp.ListDirectory;
                FtpWebResponse response = (FtpWebResponse)ftpRequest.GetResponse();
                StreamReader streamReader = new StreamReader(response.GetResponseStream());

                files = new List<string>();
                string line = streamReader.ReadLine();
                while (!string.IsNullOrEmpty(line))
                {
                    if (excludeExtensionsList == null || !excludeExtensionsList.Contains(Path.GetExtension(line)))
                    {
                        files.Add(line);
                        mFileStatusList.Add(new FileStatus() { Name = line, IsBackUp = false, IsOldVersion = false, IsDownload = false, IsExistLocal = false, DirectoryPath = mBasePathToSave });
                    }
                    line = streamReader.ReadLine();
                }
                streamReader.Close();
                UpdateManager.WriteToConsole($"{mFileStatusList.Count} files were found",Color.DEFAULT);
            }
            catch (Exception e)
            {
                Trace.WriteLine("Error in GetFTPFiles: " + e.Message);
                UpdateManager.WriteToConsole("Error in GetFTPFiles: " + e.Message,Color.RED);
                return null;
            }
            return files;
        }
        #endregion

        #region RollBack
        public enum MODE
        {
            GETFILES,
            CLEANUP,
            BACKUP_OLDVERSION,
            DOWNLOAD
        }

        private List<FileStatus> mFileStatusList = new List<FileStatus>();//for rollback
        private MODE mMode;//for rollback

        private void RollBack()
		{
			switch(mMode)
			{
				case MODE.GETFILES:
					break;
				case MODE.CLEANUP:
					break;
				case MODE.BACKUP_OLDVERSION:
					OldVersionRollBack();
					break;
				case MODE.DOWNLOAD:
					DownloadRollBack();					
					break;
			}
		}

		private bool OldVersionRollBack()
		{
            Console.WriteLine("RollBack from OldVersion");
			bool success = true;
			try
			{
				foreach(FileStatus fs in mFileStatusList)
				{
					if(fs.IsOldVersion)
					{
						if (File.Exists(fs.NewPath))
						{
							File.Move(fs.NewPath, fs.OriginalPath);
                            UpdateManager.WriteToConsole($"change to original name for {fs.Name} ended successfully",Color.DEFAULT);
                            fs.IsOldVersion = false;
						}
					}
				}
			}
			catch(Exception e)
			{
				Trace.WriteLine("Error in OldVersionRollBack: " + e.Message);
                UpdateManager.WriteToConsole("Error in OldVersionRollBack: " + e.Message,Color.RED);
                success = false;
			}
			return success;
		}

		private bool DownloadRollBack()
		{
            UpdateManager.WriteToConsole("RollBack from Download",Color.YELLOW);
            bool success = true;
			try
			{
				foreach(FileStatus fs in mFileStatusList)
				{
					if(fs.IsDownload)//if it downloaded we have backup & oldversion(see DownloadFtpFiles)
					{
						if(!File.Exists(fs.OriginalPath))
						{
							File.Delete(fs.OriginalPath);
                            UpdateManager.WriteToConsole($"delete {fs.Name} ended successfully",Color.DEFAULT);
                            fs.IsDownload = false;
						}
					}
				}
				success = OldVersionRollBack();
			}
			catch(Exception e)
			{
				Trace.WriteLine("Error in DownloadRollBack: " + e.Message);
                UpdateManager.WriteToConsole("Error in DownloadRollBack: " + e.Message,Color.RED);
                success = false;
			}
			return success;
		}
		#endregion
	}
}
