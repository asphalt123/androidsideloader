﻿using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using System.Net;
using System.Windows.Forms;
using JR.Utils.GUI.Forms;

namespace AndroidSideloader
{
    class Sideloader
    {
        public static string TempFolder = Path.Combine(Environment.CurrentDirectory, "temp");
        public static string CrashLogPath = "crashlog.txt";

        public static string SpooferWarning = @"Please make sure you have installed:
- APKTool
- Java JDK
- aapt
And all of them added to PATH, without ANY of them, the spoofer won't work!";

        //push user.json to device (required for some devices like oculus quest)
        public static void PushUserJsons()
        {
            ADB.WakeDevice();
            foreach (var userJson in UsernameForm.userJsons)
            {
                UsernameForm.createUserJsonByName(Utilities.GeneralUtilities.randomString(16), userJson);
                ADB.RunAdbCommandToString("push \"" + Environment.CurrentDirectory + $"\\{userJson}\" " + " /sdcard/");
                File.Delete(userJson);
            }
        }

        //List of all installed package names from connected device
        //public static List<string> InstalledPackageNames = new List<string>();        //Remove folder from device
        public static ProcessOutput RemoveFolder(string path)
        {
            ADB.WakeDevice();
            return ADB.RunAdbCommandToString($"shell rm -r {path}");
        }
        public static ProcessOutput RemoveFile(string path)
        {
            ADB.WakeDevice();
            return ADB.RunAdbCommandToString($"shell rm -f {path}");
        }

        //For games that require manual install, like having another folder that isnt an obb
        public static ProcessOutput RunADBCommandsFromFile(string path)
        {
            ADB.WakeDevice();
            ProcessOutput output = new ProcessOutput();
            var commands = File.ReadAllLines(path);
            string currfolder = Path.GetDirectoryName(path);
            string[] zipz = Directory.GetFiles(currfolder, "*.7z", SearchOption.AllDirectories);
            foreach (string zip in zipz)
            {
                Utilities.Zip.ExtractFile($"{zip}", currfolder);
            }
            foreach (string cmd in commands)
            {
                if (cmd.StartsWith("adb"))
                {
                    string replacement = "";
                    string pattern = "adb";
                    if (ADB.DeviceID.Length > 1)
                        replacement = $"{Properties.Settings.Default.ADBPath} -s {ADB.DeviceID}";
                    else
                        replacement = $"{Properties.Settings.Default.ADBPath}";
                    Regex rgx = new Regex(pattern);
                    string result = rgx.Replace(cmd, replacement);
                    Program.form.ChangeTitle($"Running {result}");
                    Logger.Log($"Logging command: {result} from file: {path}");
                    output += ADB.RunAdbCommandToStringWOADB(result, path);
                    if (output.Error.Contains("mkdir"))
                        output.Error = "";
                    if (output.Output.Contains("reserved"))
                        output.Output = "";
                }
            }
            output.Output += "Custom install successful!";
            return output;
        }





        //Recursive sideload any apk fileD
        public static ProcessOutput RecursiveOutput = new ProcessOutput();
        public static void RecursiveSideload(string FolderPath)
        {
            try
            {
                foreach (string f in Directory.GetFiles(FolderPath))
                {
                    if (Path.GetExtension(f) == ".apk")

                        RecursiveOutput += ADB.Sideload(f);
                }

                foreach (string d in Directory.GetDirectories(FolderPath))
                {
                    RecursiveSideload(d);
                }
            }
            catch (Exception ex) { Logger.Log(ex.Message); }
        }

        //Recursive copy any obb folder
        public static void RecursiveCopyOBB(string FolderPath)
        {
            try
            {
                foreach (string f in Directory.GetFiles(FolderPath))
                {
                    RecursiveOutput += ADB.CopyOBB(f);
                }

                foreach (string d in Directory.GetDirectories(FolderPath))
                {
                    RecursiveCopyOBB(d);
                }
            }
            catch (Exception ex) { Logger.Log(ex.Message); }
        }
        public static string BackupFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), $"Rookie Backups");
        //uninstalls an app
        public static ProcessOutput UninstallGame(string packagename)
        {
            ADB.WakeDevice();
            Program.form.ChangeTitle("Attempting to backup any savedata to Documents\\Rookie Backups...");
            ProcessOutput output = new ProcessOutput("", "");
            string date_str = DateTime.Today.ToString("yyyy.MM.dd");
            string CurrBackups = Path.Combine(BackupFolder, date_str);
            if (!Directory.Exists(CurrBackups))
            Directory.CreateDirectory(CurrBackups);
            ADB.RunAdbCommandToString($"pull \"/sdcard/Android/data/{packagename}\" \"{CurrBackups}\"");
            output = ADB.UninstallPackage(packagename);
            Program.form.ChangeTitle("");
            Sideloader.RemoveFolder("/sdcard/Android/obb/" + packagename);
            Sideloader.RemoveFolder("/sdcard/Android/data/" + packagename);
            return output;
        }

        public static ProcessOutput DeleteFile(string GameName)
        {
            ADB.WakeDevice();
            ProcessOutput output = new ProcessOutput("", "");

            string packageName = Sideloader.gameNameToPackageName(GameName);

            DialogResult dialogResult = FlexibleMessageBox.Show($"Are you sure you want to uninstall custom QU settings for {packageName}? this CANNOT be undone!", "WARNING!", MessageBoxButtons.YesNo);
            if (dialogResult != DialogResult.Yes)
                return output;

            output = Sideloader.RemoveFile($"/sdcard/Android/data/{packageName}/private/Config.Json");

            return output;
        }

        //Extracts apk from device, saves it by package name to sideloader folder
        public static ProcessOutput getApk(string GameName)
        {

            ADB.WakeDevice();
            ProcessOutput output = new ProcessOutput("", "");

            string packageName = Sideloader.gameNameToPackageName(GameName);

            output = ADB.RunAdbCommandToString("shell pm path " + packageName);

            string apkPath = output.Output; //Get apk

            apkPath = apkPath.Remove(apkPath.Length - 1);
            apkPath = apkPath.Remove(0, 8); //remove package:
            apkPath = apkPath.Remove(apkPath.Length - 1);
            if (File.Exists($"{Properties.Settings.Default.ADBFolder}\\base.apk"))
                File.Delete($"{Properties.Settings.Default.ADBFolder}\\base.apk");
            if (File.Exists($"{Properties.Settings.Default.MainDir}\\{packageName}\\{packageName}.apk"))
                File.Delete($"{Properties.Settings.Default.MainDir}\\{packageName}\\{packageName}.apk");
            output += ADB.RunAdbCommandToString("pull " + apkPath); //pull apk

            if (Directory.Exists($"{Properties.Settings.Default.MainDir}\\{packageName}"))
                Directory.Delete($"{Properties.Settings.Default.MainDir}\\{packageName}", true);

            Directory.CreateDirectory($"{Properties.Settings.Default.MainDir}\\{packageName}");

            File.Move($"{Properties.Settings.Default.ADBFolder}\\base.apk", $"{Properties.Settings.Default.MainDir}\\{packageName}\\{packageName}.apk");
            return output;
        }

        public static string gameNameToPackageName(string gameName)
        {
            foreach (string[] game in SideloaderRCLONE.games)
            {
                if (gameName.Equals(game[SideloaderRCLONE.GameNameIndex]))
                    return game[SideloaderRCLONE.PackageNameIndex];
                if (gameName.Equals(game[SideloaderRCLONE.ReleaseNameIndex]))
                    return game[SideloaderRCLONE.PackageNameIndex];
            }
            return gameName;
        }

        public static string PackageNametoGameName(string gameName)
        {
            foreach (string[] game in SideloaderRCLONE.games)
            {
                if (gameName.Equals(game[SideloaderRCLONE.PackageNameIndex]))
                    return game[SideloaderRCLONE.ReleaseNameIndex];
            }
            return gameName;
        }

        public static string gameNameToSimpleName(string gameName)
        {
            foreach (string[] game in SideloaderRCLONE.games)
            {
                if (gameName.Equals(game[SideloaderRCLONE.GameNameIndex]))
                    return game[SideloaderRCLONE.GameNameIndex];
                if (gameName.Equals(game[SideloaderRCLONE.ReleaseNameIndex]))
                    return game[SideloaderRCLONE.GameNameIndex];
            }
            return gameName;
        }

        public static string PackageNameToSimpleName(string gameName)
        {
            foreach (string[] game in SideloaderRCLONE.games)
            {
                if (gameName.Contains(game[SideloaderRCLONE.PackageNameIndex]))
                    return game[SideloaderRCLONE.GameNameIndex];
            }
            return gameName;
        }


        //Downloads the required files
        public static void downloadFiles()
        {
            var client = new WebClient();
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            try
            {
                if (!File.Exists("Sideloader Launcher.exe"))
                    client.DownloadFile("https://github.com/nerdunit/androidsideloader/raw/master/Sideloader%20Launcher.exe", "Sideloader Launcher.exe");
                if (!File.Exists("C:\\RSL\\platform-tools\\aug2021.txt") || !File.Exists("C:\\RSL\\platform-tools\\adb.exe")) //if adb is not updated, download and auto extract
                {
                    if (Directory.Exists($"C:\\RSL\\2.8.2"))
                        Directory.Delete("C:\\RSL\\2.8.2", true);
                    if (Directory.Exists($"{Properties.Settings.Default.MainDir}\\adb"))
                        Directory.Delete($"{Properties.Settings.Default.MainDir}\\adb", true);
                    if (!Directory.Exists("C:\\RSL\\platform-tools"))
                    Directory.CreateDirectory("C:\\RSL\\platform-tools");
                    client.DownloadFile("https://github.com/nerdunit/androidsideloader/raw/master/adb2.zip", "Ad.7z");
                    Utilities.Zip.ExtractFile(Environment.CurrentDirectory + "\\Ad.7z", "C:\\RSL\\platform-tools");
                    File.Delete("Ad.7z");
                }

                if (!Directory.Exists(Environment.CurrentDirectory + "\\rclone"))
                {
                    string url;
                    if (Environment.Is64BitOperatingSystem)
                        url = "https://downloads.rclone.org/v1.55.1/rclone-v1.55.1-windows-amd64.zip";
                    else
                        url = "https://downloads.rclone.org/v1.55.1/rclone-v1.55.1-windows-386.zip";
                    //Since sideloader is build for x86, it should work on both x86 and x64 so we download the according rclone version

                    client.DownloadFile(url, "rclone.zip");

                    Utilities.Zip.ExtractFile(Environment.CurrentDirectory + "\\rclone.zip", Environment.CurrentDirectory);

                    File.Delete("rclone.zip");

                    string[] folders = Directory.GetDirectories(Environment.CurrentDirectory);
                    foreach (string folder in folders)
                    {
                        if (folder.Contains("rclone"))
                        {
                            Directory.Move(folder, "rclone");
                            break; //only 1 rclone folder
                        }
                    }
                }

            }
            catch
            {
                FlexibleMessageBox.Show("Your internet is not working properly or rclone/github servers are down, some files may be missing (adb, rclone or launcher)");
            }
        }
    }


}
