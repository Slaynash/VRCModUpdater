using MelonLoader;
using Mono.Cecil;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using VRCModUpdater.API;
using VRCModUpdater.Utils;

namespace VRCModUpdater
{
    public class VRCModUpdaterPlugin : MelonPlugin
    {
        public bool isUpdatingMods = true;

        // name, (version, downloadlink)
        public Dictionary<string, (string, string)> remoteMods = new Dictionary<string, (string, string)>();
        // name, (version, filename)
        public Dictionary<string, (string, string)> installedMods = new Dictionary<string, (string, string)>();

        public static string currentStatus = "", tmpCurrentStatus = "";
        public static int progressTotal = 0, tmpProgressTotal = 0;
        public static int progressDownload = 0, tmpProgressDownload = 0;

        private int toUpdateCount = 0;

        public override void OnApplicationStart()
        {
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

        private void DispatchWindowEvents()
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

        private void UpdateMods()
        {
            Thread.Sleep(500);
            currentStatus = "Fetching remote mods...";
            FetchRemoteMods();
            currentStatus = "Listing installed mods...";
            ScanModFolder();

            currentStatus = "";
            DownloadAndUpdateMods();
            currentStatus = toUpdateCount > 0 ? "Sucessfully updated " + toUpdateCount + " mods !" : "Every mods are already up to date !";
            Thread.Sleep(1000);
        }

        private void FetchRemoteMods()
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

                remoteMods.Add(versionDetails.name, (versionDetails.modversion, versionDetails.downloadlink));
            }

            MelonLogger.Msg("API returned " + apiMods.Length + " mods, including " + remoteMods.Count + " verified mods");
        }

        private void ScanModFolder()
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
                        using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(filename, new ReaderParameters { ReadWrite = true }))
                        {

                            CustomAttribute melonInfoAttribute = assembly.CustomAttributes.FirstOrDefault(a =>
                                a.AttributeType.Name == "MelonModInfoAttribute" || a.AttributeType.Name == "MelonInfoAttribute");

                            if (melonInfoAttribute == null)
                                continue;

                            string modName = melonInfoAttribute.ConstructorArguments[1].Value as string;
                            string modVersion = melonInfoAttribute.ConstructorArguments[2].Value as string;

                            if (installedMods.ContainsKey(modName))
                            {
                                File.Delete(filename); // Delete duplicated mods
                                MelonLogger.Msg("Deleted duplicated mod " + modName);
                                continue;
                            }

                            installedMods.Add(modName, (modVersion, filename));
                        }
                    }
                    catch (Exception e)
                    {
                        MelonLogger.Msg("Failed to read assembly " + filename);
                    }
                }
            }

            MelonLogger.Msg("Found " + installedMods.Count + " unique non-dev mods installed");
        }

        private void DownloadAndUpdateMods()
        {
            // name, filename, downloadlink
            List<(string, string, string)> toUpdate = new List<(string, string, string)>();

            // List all installed mods that can be updated
            foreach (KeyValuePair<string, (string, string)> installedMod in installedMods)
            {
                foreach (KeyValuePair<string, (string, string)> remoteMod in remoteMods)
                {
                    if (remoteMod.Key == installedMod.Key)
                    {
                        if (true || remoteMod.Value.Item1 != installedMod.Value.Item1)
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
                currentStatus = $"Updating {mod.Item1} ({(i+1)} / {toUpdateCount})...";

                try
                {
                    // Delete the file
                    File.Delete(mod.Item2);

                    using (var client = new WebClient())
                    {
                        bool downloading = true;
                        client.DownloadFileCompleted += (sender, e) =>
                        {
                            if (e.Error != null)
                                MelonLogger.Error(e.Error);

                            progressDownload = 100;
                            downloading = false;
                        };
                        client.DownloadProgressChanged += (sender, e) =>
                        {
                            progressDownload = e.ProgressPercentage;
                        };
                        MelonLogger.Msg(mod.Item3 + " -> " + mod.Item2);
                        client.DownloadFileAsync(new Uri(mod.Item3), mod.Item2);

                        while (downloading)
                            Thread.Sleep(50);
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
    }
}
