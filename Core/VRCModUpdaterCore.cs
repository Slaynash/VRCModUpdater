using MelonLoader;
using Mono.Cecil;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using VRCModUpdater.API;
using VRCModUpdater.Core.API;
using VRCModUpdater.Core.Externs;
using VRCModUpdater.Core.Utils;
using Winuser;

namespace VRCModUpdater.Core
{
    public static class VRCModUpdaterCore
    {
        public const string VERSION = "1.0.6";

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

        private static List<FailedUpdateInfo> failedUpdates = new List<FailedUpdateInfo>();

        private static MelonPreferences_Entry<bool> toFromBroken, resolveDependencies, resolveOptionalDependencies;

        public static void Start()
        {
            var prefCategory = MelonPreferences.CreateCategory("VRCModUpdater");
            var diplayTimeEntry = prefCategory.CreateEntry("displaytime", postUpdateDisplayDuration, "Display time (seconds)");
            toFromBroken = prefCategory.CreateEntry("toFromBroken", true, "Attempt to move mods to and from Broken mods folder based on status in Remote API");
            resolveDependencies = prefCategory.CreateEntry("resolveDependencies", true, "Attempt to download missing required dependencies");
            resolveOptionalDependencies = prefCategory.CreateEntry("resolveOptionalDependencies", false, "Also attempt to download missing OPTIONAL dependencies");

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
                client.Headers.Add("User-Agent", APIConstants.USER_AGENT);
                apiResponse = client.DownloadString(APIConstants.MODS_ENDPOINT);
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
                remoteMods.Add(versionDetails.name, new ModDetail(versionDetails.name, versionDetails.modVersion, versionDetails.downloadLink, versionDetails.hash, versionDetails.approvalStatus));
                if (versionDetails.approvalStatus == 1)
                    verifiedModsCount++;
            }

            MelonLogger.Msg("API returned " + apiMods.Length + " mods, including " + verifiedModsCount + " verified mods");
        }

        private static void ScanModFolder()
        {
            var list = new[] { installedMods, brokenMods };
            var runOnce = false;
            var allDepList = new List<string>();
            var allOptDepList = new List<string>();
            foreach (var dict in list)
            {   //This is a kinda dirty way to do this
                dict.Clear();

                string modsFolder = MelonHandler.ModsDirectory;
                string desktopFolder = $"{modsFolder}/Desktop";
                string vrFolder = $"{modsFolder}/VR";

                string[] folders = new string[3] { modsFolder, desktopFolder, vrFolder };

                foreach (string folder in folders)
                {
                    string basedirectory = !runOnce ? folder : folder + "/Broken";
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
                                string[] optDependencies = new string[0];

                                using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(filename, new ReaderParameters { ReadWrite = true }))
                                {

                                    CustomAttribute melonInfoAttribute = assembly.CustomAttributes.FirstOrDefault(a =>
                                        a.AttributeType.Name == "MelonModInfoAttribute" || a.AttributeType.Name == "MelonInfoAttribute");

                                    if (melonInfoAttribute == null)
                                        continue;

                                    modName = melonInfoAttribute.ConstructorArguments[1].Value as string;
                                    modVersion = melonInfoAttribute.ConstructorArguments[2].Value as string;

                                }

                                List<string> depList = new List<string>();
                                if (resolveDependencies.Value)
                                {
                                    Assembly modAssembly = Assembly.Load(File.ReadAllBytes(filename)); //Only part about all of this I am suss about

                                    MelonOptionalDependenciesAttribute optionals = (MelonOptionalDependenciesAttribute)Attribute.GetCustomAttribute(modAssembly, typeof(MelonOptionalDependenciesAttribute));

                                    foreach (AssemblyName dependency in modAssembly.GetReferencedAssemblies())
                                    {
                                        var depName = GetNewModName(dependency.Name);
                                        if (remoteMods.ContainsKey(depName))
                                        {//If dep is in RemoteAPI
                                            if (!optionals?.AssemblyNames?.Contains(dependency.Name) ?? true) //Sum up all the deps
                                            {
                                                if (!allDepList.Contains(depName)) allDepList.Add(depName);
                                            }
                                            else
                                            {
                                                if (!allDepList.Contains(depName) && !allOptDepList.Contains(depName)) allOptDepList.Add(depName);
                                            }

                                            if (resolveOptionalDependencies.Value || (!optionals?.AssemblyNames?.Contains(dependency.Name) ?? true))
                                            {//If we want to add optional deps OR it's not in the optional dep list)
                                                depList.Add(depName);
                                            }
                                        }
                                    }
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
                                        dict[modName] = new ModDetail(modName, modVersion, filename, depList.ToArray(), optDependencies);
                                    }

                                    continue;
                                }

                                dict.Add(modName, new ModDetail(modName, modVersion, filename, depList.ToArray(), optDependencies));
                            }
                            catch (Exception ex)
                            {
                                MelonLogger.Msg("Failed to read assembly " + filename + "\n" + ex.ToString());
                            }
                        }
                    }
                }

                MelonLogger.Msg(ConsoleColor.DarkCyan, "Found " + dict.Count + " unique non-dev mods " + $"{(!runOnce ? "installed" : "in Broken folder")}");
                runOnce = true;
            }

            if (resolveDependencies.Value)
            { //Just some stats
                string depString, optDepString;
                if (allDepList.Count > 0)
                    depString = string.Join(", ", allDepList);
                else
                    depString = "N/A";
                if (allOptDepList.Count > 0)
                    optDepString = string.Join(", ", allOptDepList);
                else
                    optDepString = "N/A";

                MelonLogger.Msg(ConsoleColor.DarkCyan, $"Found {allDepList.Count} required dependencies: '{depString}' " +
                    $"and {allOptDepList.Count} optional dependencies: '{optDepString}' used by the mods in your mod folders.\n" +
                    $"{(resolveOptionalDependencies.Value ? "Both Required and Optional dependencies" : "Only Required dependencies")} will be downloaded, you can change this in Mod Settings");
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
            Dictionary<string, ModDetail> toMods = new Dictionary<string, ModDetail>();
            Dictionary<string, ModDetail> toBroken = new Dictionary<string, ModDetail>();
            Dictionary<string, ModDetail> toUpdate = new Dictionary<string, ModDetail>();
            Dictionary<string, ModDetail> checkDeps = new Dictionary<string, ModDetail>();

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
                    {
                        toMods.Add(brokenMod.Key, new ModDetail(brokenMod.Key, brokenMod.Value.version, brokenMod.Value.filepath, remoteMod.downloadUrl, remoteMod.hash));
                        checkDeps.Add(brokenMod.Key, brokenMod.Value);
                    }
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

                    if (remoteMod.approvalStatus != 1)
                    {
                        MelonLogger.Msg($"(Mod: {installedMod.Key}) Remote Approval Status is: Broken/Other - {remoteMod.approvalStatus}");
                        if (compareResult >= 0) // Don't move to broken if local version is higher than remote
                            toBroken.Add(installedMod.Key, new ModDetail(installedMod.Key, installedMod.Value.version, installedMod.Value.filepath));
                        else
                            MelonLogger.Msg($"Ignoring as local version is higer than remote. Remote: {remoteMod.version}, Local: {installedMod.Value.version}");
                        continue;
                    }
                    MelonLogger.Msg("(Mod: " + remoteMod.name + ") version compare between [remote] " + remoteMod.version + " and [local] " + installedMod.Value.version + ": " + compareResult);
                    if (compareResult > 0)
                        toUpdate.Add(installedMod.Key, new ModDetail(installedMod.Key, installedMod.Value.version, installedMod.Value.filepath, remoteMod.downloadUrl, remoteMod.hash));
                    checkDeps.Add(installedMod.Key, installedMod.Value);
                    continue;
                }
            }

            MelonLogger.Msg(ConsoleColor.DarkCyan, "Found " + toUpdate.Count + " outdated mods | " + toBroken.Count + " broken mods | " + toMods.Count + " fixed mods");

            if (resolveDependencies.Value)
            {
                foreach (var modtoCheck in checkDeps)
                {//Check all installed mods
                    foreach (var dep in modtoCheck.Value.dependencies)
                    {
                        if (!installedMods.ContainsKey(dep) && !toMods.ContainsKey(dep) && !toUpdate.ContainsKey(dep) && !brokenMods.ContainsKey(dep))
                        {//If dep is not in Installed Mods, & it is not being added to Mods from Broken, & it isn't already in toUpdate, & it isn't in the broken mods folder
                         //Dont want to install it twice... or more
                            if (remoteMods.TryGetValue(dep, out ModDetail remoteDep))
                            {
                                if (remoteDep.approvalStatus != 1) //Dont add broken deps
                                    continue;
                                var newFileName = remoteDep.downloadUrl.Split('/').Last();
                                var newPath = "Mods/" + newFileName;
                                MelonLogger.Warning($"Adding Dependency '{newPath}' for Mod '{modtoCheck.Value.name}'");
                                toUpdate.Add(remoteDep.name, new ModDetail(remoteDep.name, remoteDep.version, newPath, remoteDep.downloadUrl, remoteDep.hash));
                            }
                        }
                    }
                }
            }

            if (toFromBroken.Value)
            {
                foreach (KeyValuePair<string, ModDetail> mod in toMods)
                {
                    MelonLogger.Warning(mod.Value.name + " - Moving to Mods from Broken folder");
                    try
                    {
                        if (File.Exists(mod.Value.filepath))
                        {
                            var newDir = Directory.GetParent(Path.GetDirectoryName(mod.Value.filepath)) + "/" + Path.GetFileName(mod.Value.filepath);
                            toUpdate.Add(mod.Key, new ModDetail(mod.Key, mod.Value.version, newDir, mod.Value.downloadUrl, mod.Value.hash));
                            File.Delete(mod.Value.filepath);
                        }
                    }
                    catch (Exception e)
                    {
                        MelonLogger.Error("Failed to updated fixed mod" + mod.Value.filepath + ":\n" + e);
                    }
                }

                foreach (KeyValuePair<string, ModDetail> mod in toBroken)
                {
                    {
                        MelonLogger.Warning(mod.Value.name + " - Moving to Broken folder");
                        try
                        {
                            if (File.Exists(mod.Value.filepath))
                            {
                                var newDir = Path.GetDirectoryName(mod.Value.filepath) + "/Broken";
                                if (!Directory.Exists(newDir))
                                    Directory.CreateDirectory(newDir);
                                var newFilePath = newDir + "/" + Path.GetFileName(mod.Value.filepath);
                                if (!File.Exists(newFilePath))
                                    File.Move(mod.Value.filepath, newFilePath);
                                else
                                    File.Delete(mod.Value.filepath);
                            }
                        }
                        catch (Exception e)
                        {
                            MelonLogger.Error("Failed to move mod to broken folder" + mod.Value.filepath + ":\n" + e);
                        }
                    }
                }

                toUpdateCount = toUpdate.Count;
                int i = 0;
                foreach (KeyValuePair<string, ModDetail> mod in toUpdate)
                {
                    MelonLogger.Msg("Updating " + mod.Value.name);
                    progressTotal = (int)(i / (double)toUpdateCount * 100);
                    currentStatus = $"Updating {mod.Value.name} ({i + 1} / {toUpdateCount})...";

                    try
                    {
                        bool errored = false;
                        using (var client = new WebClient())
                        {
                            client.Headers.Add("User-Agent", APIConstants.USER_AGENT);
                            bool downloading = true;
                            byte[] downloadedFileData = null;
                            client.DownloadDataCompleted += (sender, e) =>
                            {
                                if (e.Error != null)
                                {
                                    MelonLogger.Error("Failed to update " + mod.Value.name + ":\n" + e.Error);
                                    errored = true;
                                    failedUpdates.Add(new FailedUpdateInfo(mod.Value, FailedUpdateReason.DownloadError, e.ToString()));
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
                            client.DownloadDataAsync(new Uri(mod.Value.downloadUrl));

                            while (downloading)
                                Thread.Sleep(50);

                            if (!errored)
                            {
                                string downloadedHash = ComputeSha256Hash(downloadedFileData);
                                MelonLogger.Msg("Downloaded file hash: " + downloadedHash);

                                if (downloadedHash == mod.Value.hash)
                                {
                                    try
                                    {
                                        File.WriteAllBytes(mod.Value.filepath, downloadedFileData);
                                    }
                                    catch (Exception e)
                                    {
                                        MelonLogger.Error("Failed to save " + mod.Value.filepath + ":\n" + e);
                                        failedUpdates.Add(new FailedUpdateInfo(mod.Value, FailedUpdateReason.SaveError, e.ToString()));
                                    }

                                }
                                else
                                {
                                    MelonLogger.Error("Downloaded file hash mismatches database hash!");
                                    failedUpdates.Add(new FailedUpdateInfo(mod.Value, FailedUpdateReason.HashMismatch, $"Expected hash: {mod.Value.hash}, Downloaded file hash: {downloadedHash}"));
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        MelonLogger.Error("Failed to update " + mod.Value.filepath + ":\n" + e);
                        failedUpdates.Add(new FailedUpdateInfo(mod.Value, FailedUpdateReason.Unknown, e.ToString()));
                    }

                    progressTotal = (int)((i + 1) / (double)toUpdateCount * 100);
                    MelonLogger.Msg($"Progress: {i + 1}/{toUpdateCount} -> {progressTotal}%");
                    i++;
                }
            }
        }
    }
}
