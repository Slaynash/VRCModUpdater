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
        public const string VERSION = "1.0.4";

        private static readonly Dictionary<string, string> oldToNewModNames = new Dictionary<string, string>()
        {
            // Used in case something is missing on Ruby's API
        };

        private static float postUpdateDisplayDuration = 3f;
        private static bool isUpdatingMods = true;

        private static Dictionary<string, ModDetail> remoteMods = new Dictionary<string, ModDetail>();
        private static Dictionary<string, ModDetail> installedMods = new Dictionary<string, ModDetail>();
        private static Dictionary<string, ModDetail> brokenMods = new Dictionary<string, ModDetail>();

        public static string currentStatus = "", tmpCurrentStatus = "";
        public static int progressTotal = 0, progressDownload = 0;
        private static int tmpProgressTotal = 0, tmpProgressDownload = 0;

        private static int toUpdateCount = 0;
        private static int toBrokenCount = 0;
        private static int toModsCount = 0;

        private static List<FailedUpdateInfo> failedUpdates = new List<FailedUpdateInfo>();

        private static MelonPreferences_Entry<bool> toFromBroken;

        public static void Start()
        {
            var prefCategory = MelonPreferences.CreateCategory("VRCModUpdater");
            var diplayTimeEntry = prefCategory.CreateEntry("displaytime", postUpdateDisplayDuration, "Display time (seconds)");
            toFromBroken = prefCategory.CreateEntry("toFromBroken", true, "Attempt to move mods to and from Broken mods folder based on status in Remote API");
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
            {
                client.Headers["User-Agent"] = "VRCModUpdater";
                apiResponse = client.DownloadString("https://api.vrcmg.com/v0/mods.json");
            }

            APIMod[] apiMods = JsonConvert.DeserializeObject<APIMod[]>(apiResponse);

            remoteMods.Clear();
            int verifiedModsCount = 0;

            foreach (APIMod mod in apiMods)
            {
                if (mod.versions.Length == 0)
                    continue;

                APIModVersion versionDetails = mod.versions[0];

                // Aliases
                foreach (string alias in mod.aliases)
                    if (alias != versionDetails.name)
                        oldToNewModNames[alias] = versionDetails.name;

                // Add to known mods
                remoteMods.Add(versionDetails.name, new ModDetail(versionDetails.name, versionDetails.modversion, versionDetails.downloadlink, versionDetails.hash, versionDetails.ApprovalStatus));
                if (versionDetails.ApprovalStatus == 1)
                    verifiedModsCount++;
            }

            MelonLogger.Msg("API returned " + apiMods.Length + " mods, including " + verifiedModsCount + " verified mods");
        }

        private static void ScanModFolder()
        {
            var list = new [] { installedMods, brokenMods };
            var runOnce = false;
            foreach (var dict in list)
            {   //This is a kinda dirty way to do this
                dict.Clear();
                string basedirectory = !runOnce ? MelonHandler.ModsDirectory : MelonHandler.ModsDirectory + "/Broken";
                if (!Directory.Exists(basedirectory))
                    continue;
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

                            if (dict.TryGetValue(modName, out ModDetail installedModDetail))
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
                                    dict[modName] = new ModDetail(modName, modVersion, filename);
                                }

                                continue;
                            }

                            dict.Add(modName, new ModDetail(modName, modVersion, filename));
                        }
                        catch (Exception)
                        {
                            MelonLogger.Msg("Failed to read assembly " + filename);
                        }
                    }
                }
                MelonLogger.Msg("Found " + dict.Count + " unique non-dev mods " + $"{(!runOnce ? "installed" : "in Broken folder")}");
                runOnce = true;
            }
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
            List<ModDetail> toMods = new List<ModDetail>();
            List<ModDetail> toBroken = new List<ModDetail>();
            List<ModDetail> toUpdate = new List<ModDetail>();

            // Check for broken mods with updates
            foreach (KeyValuePair<string, ModDetail> brokenMod in brokenMods)
            {
                if (remoteMods.TryGetValue(brokenMod.Key, out ModDetail remoteMod))
                {
                    if (remoteMod.approvalStatus != 1)
                        continue;

                    VersionUtils.VersionData brokenModVersion = VersionUtils.GetVersion(brokenMod.Value.version);
                    VersionUtils.VersionData remoteModVersion = VersionUtils.GetVersion(remoteMod.version);
                    int compareResult = VersionUtils.CompareVersion(remoteModVersion, brokenModVersion);
                    
                    MelonLogger.Msg("(Broken Mod: " + remoteMod.name + ") version compare between [remote] " + remoteMod.version + " and [local] " + brokenMod.Value.version + ": " + compareResult);
                    if (compareResult > 0) // Don't move from broken unless new version > old
                        toMods.Add(new ModDetail(brokenMod.Key, brokenMod.Value.version, brokenMod.Value.filepath, remoteMod.downloadUrl, remoteMod.hash));
                    continue;
                }
            }

            // List all installed mods that can be updated or moved to broken
            foreach (KeyValuePair<string, ModDetail> installedMod in installedMods)
            {
                if (remoteMods.TryGetValue(installedMod.Key, out ModDetail remoteMod))
                {
                    VersionUtils.VersionData installedModVersion = VersionUtils.GetVersion(installedMod.Value.version);
                    VersionUtils.VersionData remoteModVersion = VersionUtils.GetVersion(remoteMod.version);
                    int compareResult = VersionUtils.CompareVersion(remoteModVersion, installedModVersion);

                    if(remoteMod.approvalStatus != 1)
                    {
                        MelonLogger.Msg($"(Mod: {installedMod.Key}) Remote Approval Status is: Broken/Other - {remoteMod.approvalStatus}");
                        if (compareResult >= 0) // Don't move to broken if local version is higher than remote
                            toBroken.Add(new ModDetail(installedMod.Key, installedMod.Value.version, installedMod.Value.filepath));
                        else
                            MelonLogger.Msg($"Ignoring as local version is higer than remote. Remote: {remoteMod.version}, Local: {installedMod.Value.version}");
                        continue;
                    }
                    MelonLogger.Msg("(Mod: " + remoteMod.name + ") version compare between [remote] " + remoteMod.version + " and [local] " + installedMod.Value.version + ": " + compareResult);
                    if (compareResult > 0)
                        toUpdate.Add(new ModDetail(installedMod.Key, installedMod.Value.version, installedMod.Value.filepath, remoteMod.downloadUrl, remoteMod.hash));
                    continue;
                }
            }

            MelonLogger.Msg("Found " + toUpdate.Count + " outdated mods | " + toBroken.Count + " broken mods | " + toMods.Count + " fixed mods");

            if (toFromBroken.Value)
            {
                toModsCount = toMods.Count;
                for (int i = 0; i < toModsCount; ++i)
                {
                    ModDetail mod = toMods[i];
                    MelonLogger.Warning(mod.name + " - Moving to Mods from Broken folder");
                    try
                    {
                        if (File.Exists(mod.filepath))
                        {
                            var newDir = Directory.GetParent(Path.GetDirectoryName(mod.filepath)) + "/" + Path.GetFileName(mod.filepath);
                            toUpdate.Add(new ModDetail(mod.name, mod.version, newDir, mod.downloadUrl, mod.hash));
                            File.Delete(mod.filepath);
                        }
                    }
                    catch (Exception e)
                    {
                        MelonLogger.Error("Failed to updated fixed mod" + mod.filepath + ":\n" + e);
                    }
                }

                toBrokenCount = toBroken.Count;
                for (int i = 0; i < toBrokenCount; ++i)
                {
                    ModDetail mod = toBroken[i];
                    MelonLogger.Warning(mod.name + " - Moving to Broken folder");
                    try
                    {
                        if (File.Exists(mod.filepath))
                        {
                            var newDir = Path.GetDirectoryName(mod.filepath) + "/Broken";
                            if (!Directory.Exists(newDir))
                                Directory.CreateDirectory(newDir);
                            var newFilePath = newDir + "/" + Path.GetFileName(mod.filepath);
                            if (!File.Exists(newFilePath))
                                File.Move(mod.filepath, newFilePath);
                            else
                                File.Delete(mod.filepath);
                        }
                    }
                    catch (Exception e)
                    {
                        MelonLogger.Error("Failed to move mod to broken folder" + mod.filepath + ":\n" + e);
                    }
                }
            }

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
