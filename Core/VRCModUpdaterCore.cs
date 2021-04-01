using MelonLoader;
using Mono.Cecil;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using VRCModUpdater.API;
using VRCModUpdater.Core.Externs;
using VRCModUpdater.Core.Utils;
using Winuser;

namespace VRCModUpdater.Core
{
    public static class VRCModUpdaterCore
    {
        public const string VERSION = "1.0.3";

        private static readonly Dictionary<string, string> oldToNewModNames = new Dictionary<string, string>()
        {
            // Used in case something is missing on Ruby's API
        };

        private static float postUpdateDisplayDuration = 3f;
        private static bool isUpdatingMods = true;

        private static Dictionary<string, ModDetail> remoteMods = new Dictionary<string, ModDetail>();
        private static Dictionary<string, ModDetail> installedMods = new Dictionary<string, ModDetail>();

        public static string currentStatus = "", tmpCurrentStatus = "";
        public static int progressTotal = 0, progressDownload = 0;
        private static int tmpProgressTotal = 0, tmpProgressDownload = 0;

        private static int toUpdateCount = 0;

        private static List<FailedUpdateInfo> failedUpdates = new List<FailedUpdateInfo>();

        public static void Start()
        {
            var prefCategory = MelonPreferences.CreateCategory("VRCModUpdater");
            var diplayTimeEntry = prefCategory.CreateEntry("displaytime", postUpdateDisplayDuration, "Display time (seconds)");
            if (float.TryParse(diplayTimeEntry.GetValueAsString(), out float diplayTime))
                postUpdateDisplayDuration = diplayTime;

            UpdaterWindow.CreateWindow();

            new Thread(() =>
            {
                try
                {
                    UpdateMods();
                }
                catch (Exception e)
                {
                    MelonLogger.Error("Failed to update mods:\n" + e);
                }

                isUpdatingMods = false;
            })
            {
                Name = "VRCModUpdater",
                IsBackground = true
            }.Start();

            while (isUpdatingMods || UpdaterWindow.IsOpen) // We need to do that event w/o the window, because the console need it
            {
                if (!isUpdatingMods && !UpdaterWindow.IsWindowClosing)
                    UpdaterWindow.DestroyWindow();

                DispatchWindowEvents();
            }
        }

        private static void DispatchWindowEvents()
        {
            if (!User32.PeekMessage(out Msg msg, IntPtr.Zero, 0, 0, 1))
            {
                if (currentStatus != tmpCurrentStatus || progressTotal != tmpProgressTotal || progressDownload != tmpProgressDownload)
                {
                    tmpCurrentStatus = currentStatus;
                    tmpProgressTotal = progressTotal;
                    tmpProgressDownload = progressDownload;

                    UpdaterWindow.RedrawWindow();
                }
                Thread.Sleep(16); // ~60/s
            }
            else
            {
                User32.TranslateMessage(ref msg);
                User32.DispatchMessage(ref msg);
            }
        }

        private static void UpdateMods()
        {
            Thread.Sleep(500);
            currentStatus = "Fetching remote mods...";
            FetchRemoteMods();
            currentStatus = "Listing installed mods...";
            ScanModFolder();

            currentStatus = "";
            DownloadAndUpdateMods();

            if (toUpdateCount == 0)
                currentStatus = "All installed mods are already up to date !";
            else if (failedUpdates.Count > 0)
                currentStatus = $"{failedUpdates.Count} mods failed to update ({toUpdateCount - failedUpdates.Count}/{toUpdateCount} succeeded)";
            else
                currentStatus = "Sucessfully updated " + toUpdateCount + " mods !";

            Thread.Sleep((int)(postUpdateDisplayDuration * 1000));
        }

        private static void FetchRemoteMods()
        {
            string apiResponse;
            using (var client = new WebClient())
                apiResponse = client.DownloadString("https://ruby-core.com/api/mods.json");

            APIMod[] apiMods = JsonConvert.DeserializeObject<APIMod[]>(apiResponse);

            remoteMods.Clear();
            
            foreach (APIMod mod in apiMods)
            {
                if (mod.versions.Length == 0)
                    continue;

                APIModVersion versionDetails = mod.versions[0];

                if (versionDetails.ApprovalStatus != 1)
                    continue;

                // Aliases
                foreach (string alias in mod.aliases)
                    if (alias != versionDetails.name)
                        oldToNewModNames[alias] = versionDetails.name;

                // Add to known mods
                remoteMods.Add(versionDetails.name, new ModDetail(versionDetails.name, versionDetails.modversion, versionDetails.downloadlink, versionDetails.hash));
            }

            MelonLogger.Msg("API returned " + apiMods.Length + " mods, including " + remoteMods.Count + " verified mods");
        }

        private static void ScanModFolder()
        {
            installedMods.Clear();

            string basedirectory = MelonHandler.ModsDirectory;

            string[] dlltbl = Directory.GetFiles(basedirectory, "*.dll");
            if (dlltbl.Length > 0)
            {
                for (int i = 0; i < dlltbl.Length; i++)
                {
                    string filename = dlltbl[i];
                    if (string.IsNullOrEmpty(filename))
                        continue;

                    if (filename.EndsWith(".dev.dll"))
                        continue;

                    try
                    {
                        string modName;
                        string modVersion;
                        using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(filename, new ReaderParameters { ReadWrite = true }))
                        {

                            CustomAttribute melonInfoAttribute = assembly.CustomAttributes.FirstOrDefault(a =>
                                a.AttributeType.Name == "MelonModInfoAttribute" || a.AttributeType.Name == "MelonInfoAttribute");

                            if (melonInfoAttribute == null)
                                continue;

                            modName = melonInfoAttribute.ConstructorArguments[1].Value as string;
                            modVersion = melonInfoAttribute.ConstructorArguments[2].Value as string;
                        }

                        modName = GetNewModName(modName); // Backward mod compatibility

                        if (installedMods.TryGetValue(modName, out ModDetail installedModDetail))
                        {
                            if (VersionUtils.CompareVersion(installedModDetail.version, modVersion) > 0)
                            {
                                File.Delete(filename); // Delete duplicated mods
                                MelonLogger.Msg("Deleted duplicated mod " + modName);
                            }
                            else
                            {
                                File.Delete(installedModDetail.filepath); // Delete duplicated mods
                                MelonLogger.Msg("Deleted duplicated mod " + modName);
                                installedMods[modName] = new ModDetail(modName, modVersion, filename);
                            }

                            continue;
                        }

                        installedMods.Add(modName, new ModDetail(modName, modVersion, filename));
                    }
                    catch (Exception)
                    {
                        MelonLogger.Msg("Failed to read assembly " + filename);
                    }
                }
            }

            MelonLogger.Msg("Found " + installedMods.Count + " unique non-dev mods installed");
        }

        private static string GetNewModName(string currentName)
        {
            return oldToNewModNames.TryGetValue(currentName, out string newName) ? newName : currentName;
        }

        private static string ComputeSha256Hash(byte[] rawData)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(rawData);
                return Convert.ToBase64String(bytes);
            }
        }

        private static void DownloadAndUpdateMods()
        {
            List<ModDetail> toUpdate = new List<ModDetail>();

            // List all installed mods that can be updated
            foreach (KeyValuePair<string, ModDetail> installedMod in installedMods)
            {
                VersionUtils.VersionData installedModVersion = VersionUtils.GetVersion(installedMod.Value.version);
                foreach (KeyValuePair<string, ModDetail> remoteMod in remoteMods)
                {
                    if (installedMod.Key == remoteMod.Key)
                    {
                        VersionUtils.VersionData remoteModVersion = VersionUtils.GetVersion(remoteMod.Value.version);
                        int compareResult = VersionUtils.CompareVersion(remoteModVersion, installedModVersion);
                        MelonLogger.Msg("(Mod: " + remoteMod.Key + ") version compare between [remote] " + remoteMod.Value.version + " and [local] " + installedMod.Value.version + ": " + compareResult);
                        if (compareResult > 0)
                            toUpdate.Add(new ModDetail(installedMod.Key, installedMod.Value.version, installedMod.Value.filepath, remoteMod.Value.downloadUrl, remoteMod.Value.hash));

                        break;
                    }
                }
            }

            MelonLogger.Msg("Found " + toUpdate.Count + " outdated mods");

            toUpdateCount = toUpdate.Count;
            for (int i = 0; i < toUpdateCount; ++i)
            {
                ModDetail mod = toUpdate[i];

                MelonLogger.Msg("Updating " + mod.name);
                progressTotal = (int)(i / (double)toUpdateCount * 100);
                currentStatus = $"Updating {mod.name} ({i + 1} / {toUpdateCount})...";

                try
                {
                    bool errored = false;
                    using (var client = new WebClient())
                    {
                        bool downloading = true;
                        byte[] downloadedFileData = null;
                        client.DownloadDataCompleted += (sender, e) =>
                        {
                            if (e.Error != null)
                            {
                                MelonLogger.Error("Failed to update " + mod.name + ":\n" + e.Error);
                                errored = true;
                                failedUpdates.Add(new FailedUpdateInfo(mod, FailedUpdateReason.DownloadError, e.ToString()));
                            }
                            else
                                downloadedFileData = e.Result;

                            progressDownload = 100;
                            downloading = false;
                        };
                        client.DownloadProgressChanged += (sender, e) =>
                        {
                            progressDownload = e.ProgressPercentage;
                        };
                        client.DownloadDataAsync(new Uri(mod.downloadUrl));

                        while (downloading)
                            Thread.Sleep(50);

                        if (!errored)
                        {
                            string downloadedHash = ComputeSha256Hash(downloadedFileData);
                            MelonLogger.Msg("Downloaded file hash: " + downloadedHash);

                            if (downloadedHash == mod.hash)
                            {
                                try
                                {
                                    File.WriteAllBytes(mod.filepath, downloadedFileData);
                                }
                                catch (Exception e)
                                {
                                    MelonLogger.Error("Failed to save " + mod.filepath + ":\n" + e);
                                    failedUpdates.Add(new FailedUpdateInfo(mod, FailedUpdateReason.SaveError, e.ToString()));
                                }

                            }
                            else
                            {
                                MelonLogger.Error("Downloaded file hash mismatches database hash!");
                                failedUpdates.Add(new FailedUpdateInfo(mod, FailedUpdateReason.HashMismatch, $"Expected hash: {mod.hash}, Downloaded file hash: {downloadedHash}"));
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    MelonLogger.Error("Failed to update " + mod.filepath + ":\n" + e);
                    failedUpdates.Add(new FailedUpdateInfo(mod, FailedUpdateReason.Unknown, e.ToString()));
                }

                progressTotal = (int)((i + 1) / (double)toUpdateCount * 100);
                MelonLogger.Msg($"Progress: {i + 1}/{toUpdateCount} -> {progressTotal}%");
;            }
        }
    }
}
