using MelonLoader;
using Mono.Cecil;
using Newtonsoft.Json;
using Semver;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using VRCModUpdater.API;
using VRCModUpdater.Utils;

namespace VRCModUpdater.Core
{
    public static class VRCModUpdaterCore
    {
        public const string VERSION = "0.0.1";

        private static readonly Dictionary<string, string> oldToNewModNames = new Dictionary<string, string>()
        {
            { "Mute Blink Be Gone", "MuteBlinkBeGone" },
            { "CoreLimiter", "Core Limiter" },
            { "RuntimeGraphicsSettings", "Runtime Graphics Settings" },
            { "VRC Video Library", "VRCVideoLibrary" },
            { "Extra Cameras", "ITR's Melon Cameras" },
            { "OldLoadingScreen", "BetterLoadingScreen" },
            { "EyeTrack" , "VRCEyeTracking" },

            { "Input System", "InputSystem" }, // Unreleased?

        };

        private static float postUpdateDisplayDuration = 3f;
        private static bool isUpdatingMods = true;

        // name, (version, downloadlink)
        private static Dictionary<string, (string, string)> remoteMods = new Dictionary<string, (string, string)>();
        // name, (version, filename)
        private static Dictionary<string, (string, string)> installedMods = new Dictionary<string, (string, string)>();

        public static string currentStatus = "", tmpCurrentStatus = "";
        public static int progressTotal = 0, progressDownload = 0;
        private static int tmpProgressTotal = 0, tmpProgressDownload = 0;

        private static int toUpdateCount = 0;

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
            if (!Externs.PeekMessage(out MSG msg, IntPtr.Zero, 0, 0, 1))
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
                Externs.TranslateMessage(ref msg);
                Externs.DispatchMessage(ref msg);
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
            currentStatus = toUpdateCount > 0 ? "Sucessfully updated " + toUpdateCount + " mods !" : "Every mods are already up to date !";
            Thread.Sleep((int)(postUpdateDisplayDuration * 1000));
        }

        private static void FetchRemoteMods()
        {
            string apiResponse;
            using (var client = new WebClient())
                apiResponse = client.DownloadString("http://client.ruby-core.com/api/mods.json");

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
                remoteMods.Add(versionDetails.name, (versionDetails.modversion, versionDetails.downloadlink));
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

                        if (installedMods.TryGetValue(modName, out (string, string) modDetails))
                        {
                            if (CompareVersion(modDetails.Item1, modVersion) > 0)
                            {
                                File.Delete(filename); // Delete duplicated mods
                                MelonLogger.Msg("Deleted duplicated mod " + modName);
                            }
                            else
                            {
                                File.Delete(modDetails.Item2); // Delete duplicated mods
                                MelonLogger.Msg("Deleted duplicated mod " + modName);
                                installedMods[modName] = (modVersion, filename);
                            }

                            continue;
                        }

                        installedMods.Add(modName, (modVersion, filename));
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

        private static void DownloadAndUpdateMods()
        {
            // name, filename, downloadlink
            List<(string, string, string)> toUpdate = new List<(string, string, string)>();

            // List all installed mods that can be updated
            foreach (KeyValuePair<string, (string, string)> installedMod in installedMods)
            {
                foreach (KeyValuePair<string, (string, string)> remoteMod in remoteMods)
                {
                    
                    if (installedMod.Key == remoteMod.Key)
                    {
                        int compareResult = CompareVersion(remoteMod.Value.Item1, installedMod.Value.Item1);
                        MelonLogger.Msg("(Mod: " + remoteMod.Key + ") version compare between [remote] " + remoteMod.Value.Item1 + " and [local] " + installedMod.Value.Item1 + ": " + compareResult);
                        if (compareResult > 0)
                            toUpdate.Add((installedMod.Key, installedMod.Value.Item2, remoteMod.Value.Item2));

                        break;
                    }
                }
            }

            MelonLogger.Msg("Found " + toUpdate.Count + " outdated mods");

            toUpdateCount = toUpdate.Count;
            for (int i = 0; i < toUpdateCount; ++i)
            {
                (string, string, string) mod = toUpdate[i];

                MelonLogger.Msg("Updating " + mod.Item1);
                progressTotal = (int)(i / (double)toUpdateCount * 100);
                currentStatus = $"Updating {mod.Item1} ({i + 1} / {toUpdateCount})...";

                try
                {
                    // Delete the file
                    if (File.Exists(mod.Item2 + ".tmp"))
                        File.Delete(mod.Item2 + ".tmp");

                    bool errored = false;
                    using (var client = new WebClient())
                    {
                        bool downloading = true;
                        client.DownloadFileCompleted += (sender, e) =>
                        {
                            if (e.Error != null)
                            {
                                MelonLogger.Error("Failed to update " + mod.Item1 + ":\n" + e.Error);
                                errored = true;
                            }

                            progressDownload = 100;
                            downloading = false;
                        };
                        client.DownloadProgressChanged += (sender, e) =>
                        {
                            progressDownload = e.ProgressPercentage;
                        };
                        MelonLogger.Msg(mod.Item3 + " -> " + mod.Item2 + ".tmp");
                        client.DownloadFileAsync(new Uri(mod.Item3), mod.Item2 + ".tmp");

                        while (downloading)
                            Thread.Sleep(50);
                    }

                    if (!errored)
                    {
                        File.Delete(mod.Item2);
                        File.Move(mod.Item2 + ".tmp", mod.Item2);
                    }
                    else
                    {
                        try
                        {
                            if (File.Exists(mod.Item2 + ".tmp"))
                                File.Delete(mod.Item2 + ".tmp");
                        }
                        catch (Exception)
                        {
                            MelonLogger.Error("Failed to delete " + mod.Item2 + ".tmp");
                        }
                    }
                }
                catch (Exception e)
                {
                    MelonLogger.Error("Failed to update " + mod.Item1 + ":\n" + e);
                }

                progressTotal = (int)((i + 1) / (double)toUpdateCount * 100);
                MelonLogger.Msg((i + 1) + "/" + toUpdateCount + " -> " + progressTotal + "%");
;            }
        }

        // left more recent: 1
        // identicals: 0
        // right more recent: -1
        private static int CompareVersion(string left, string right)
        {
            left = SanitizePreSemver(left);
            right = SanitizePreSemver(right);

            if (SemVersion.TryParse(left, out SemVersion leftSemver))
            {
                if (!SemVersion.TryParse(right, out SemVersion rightSemver))
                    return 1;

                MelonLogger.Msg(leftSemver + " vs " + rightSemver);

                return SemVersion.Compare(leftSemver, rightSemver);
            }

            if (SemVersion.TryParse(right, out SemVersion rightSemver_))
                return -1;

            double leftReduced = ReduceVersion(left);
            double rightReduced = ReduceVersion(right);

            int result = leftReduced > rightReduced ? 1 : (leftReduced < rightReduced ? -1 : 0);

            MelonLogger.Warning($"versions \"{left}\" and \"{right}\" aren't semversions. reduced to {leftReduced} and {rightReduced} (result: {result})");

            return result;
        }

        private static string SanitizePreSemver(string original)
        {
            string result = "";

            if (Regex.IsMatch(original, "^[vV]\\d")) // remove "v" at the beginning (case insensitive)
                original = original.Substring(1);

            string[] split = original.Split('.');
            for (int i = 0; i < split.Length; ++i)
            {
                if (i > 0 && i < 3)
                    result += ".";
                else if (i == 3 && int.TryParse(split[2], out int part3)) // 4 digit semversion fix
                {
                    if (part3 < 10)
                        split[2] += "00";
                    else if (part3 < 100)
                        split[2] += "0";
                }

                result += split[i];
            }

            return result;
        }

        private static double ReduceVersion(string original)
        {
            string pattern = @"\d";

            string result = "";

            foreach (Match m in Regex.Matches(original, pattern))
                result += m;

            if (result.Length == 0)
                return 0;

            if (result.Length > 1)
                result = result.StartsWith("0") ? (result.Substring(0, 1) + "." + result.Substring(1)) : result;

            return double.Parse(result);
        }
    }
}
