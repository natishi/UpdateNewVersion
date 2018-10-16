# UpdateNewVersion
Update new app version from ftp(Source code)

Update New Version
We have 2 approach:
## 	Use UpdateNewVersion.dll for automatic update.
Inside our application code(where the application is starting).

How to use?

FtpDetails ftp = newFtpDetails(){

      Address = ConfigurationManager.AppSettings["FtpAddress"],
      User = ConfigurationManager.AppSettings["FtpUser"],
      Password = ConfigurationManager.AppSettings["FtpPass"],
      ExcludeExtention = newList<string>(ConfigurationManager.AppSettings["excludeExtention"].Split(','))

};

UpdateManager mnger = UpdateManager.Instance;

bool success = mnger.UpdateAssemblyVersion(ftp);

if (success)
UpdateManager.WriteToConsole("Version updated successfully!", Color.GREEN);

else
UpdateManager.WriteToConsole("Version update failed!", Color.RED);


## 	Use Loader.exe.(see Loader repository)
This exe file will update the app after we will activate it from app exe file location.
How to use?
Need to put 3 files under app exe  file location.
1.	Loader.exe file
2.	UpdateNewVersion.dll
3.	Loader.exe.config
This file will contain:
a.	App name
b.	Ftp details(Address, User, Password)
c.	Exclude extension list


