using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Checkered.Services.Interfaces;
using Checkered.Services;
using Checkered.Models.Interfaces;
using System.Diagnostics;

namespace ConsoleChecker
{
    class Program
    {
        static void Main(string[] args)
        {
            List<IApplication> apps = new List<IApplication>();
            List<IConcentrator> concs = new List<IConcentrator>();
            string facilityName = String.Empty;
            string tech = String.Empty;
            string backupPath = String.Empty;
            float totalCPUUsage = 0;
            float availableMemory = 0;
            int processCount = 0;
            List<string> backupFiles = new List<string>();
            List<IDriveData> drives = new List<IDriveData>();
            IConfigurationService configService = new XmlConfigService("config.xml");
            IFileService fileService = new FileService();
            Console.Write("Getting configuration file from local folder...");
            if (File.Exists("config.xml"))
            {
                apps = configService.GetApplications().ToList();
                concs = configService.GetConcentrators().ToList();
                facilityName = configService.GetFacilityName();
                tech = configService.Tech;
                backupPath = configService.GetBackupLocation();
                backupFiles = apps.SelectMany(f => f.Files.Select(a => f.Folder + a)).ToList();
                Console.WriteLine("Loaded Config File!");
            }
            else
            {
                Console.WriteLine("No config file found!");
                Console.Write("Generating template now...");
                
                configService.CreateNewConfiguration();
                Console.WriteLine("Successfully created new config file.");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }
            Console.Write("Gathering System Data...");
            totalCPUUsage = ProcessService.TotalCPU();
            availableMemory = ProcessService.AvailableMemory();
            processCount = ProcessService.ProcessCount();
            drives = DriveService.GetDrives().ToList();
            Console.WriteLine("Done!");
            if (apps.Count > 0)
            {
                Console.WriteLine("Gathering Application Data...");
                foreach (IApplication app in apps)
                {
                    string processName = new string(app.Executable.Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries).Last().TakeWhile(c => c != '.').ToArray());
                    if (Process.GetProcessesByName(processName).Length == 1)
                    {
                        Console.Write(processName + "...");
                        app.ProcessUsage = ProcessService.ProcessCPU(processName);
                        app.MemoryUsage = ProcessService.ProcessPrivateMemory(processName);
                        app.Version = fileService.GetFileVersion(app.Folder + app.Executable);
                        Console.WriteLine("Done!");
                    }
                }
            }
            if (concs.Count > 0)
            {
                Console.WriteLine("Pinging Concentrators...");
                foreach (IConcentrator conc in concs)
                {
                    Console.Write("Pinging {0}...", conc.Ip);
                    conc.UpdatePing(3);
                    Console.WriteLine("Done!");
                }
            }
            if (backupFiles.Count > 0 && backupPath != String.Empty)
            {
                Console.WriteLine("Backing up files...");
                foreach (IApplication app in apps)
                {
                    if (app.Files.Length > 0)
                    {
                        Console.Write("{0}...", app.DisplayName);
                        try
                        {
                            fileService.BackupFiles(app, backupPath);
                            Console.WriteLine("Done!");
                        }
                        catch(Exception ex)
                        {
                            Console.Write(ex.Message);
                            if (ex.InnerException != null)
                                Console.WriteLine(ex.InnerException.Message);
                            else
                                Console.WriteLine();
                        }
                    }
                }
            }
            string savePath = "CheckData.csv";
            bool saved = false;
            int saveAttempts = 0;
            ISaveService saveService = new CsvService();
            while (!saved && saveAttempts < 5)
            {
                saveAttempts++;
                saved = saveService.SaveData(savePath, facilityName, tech, DateTime.Today, drives, totalCPUUsage, processCount, availableMemory, apps, concs);
                if (!saved)
                    savePath = String.Format("CheckData({0}).csv", saveAttempts);
            }
            if (saved)
                Console.WriteLine("Saved data to {0}", savePath);
            Console.WriteLine("System Check Completed Successfully!");
            Console.WriteLine("Press any key to quit...");
            Console.ReadKey();
        }
    }
}
