using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UpdateNewVersion
{
	public enum Status
	{
		BACKUP,
		OLDVERSION,
		DOWNLOAD,
        EXISTS
	}
	class FileStatus
	{
		public string Name { get; set; }
		public bool IsBackUp { get; set; }//under _backup folder
		public bool IsOldVersion { get; set; }//_OldVersion
		public bool IsDownload { get; set; }//from ftp
        public bool IsExistLocal { get; set; }//file exists in local
        public string OriginalPath { get; set; }
		public string NewPath { get; set; }
		public string DirectoryPath
		{
			set
			{
				OriginalPath = value + Name;
				NewPath = Updater.ChangeFilePath(value + Name); 
			}
		}
	}
}
