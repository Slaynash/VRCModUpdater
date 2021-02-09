using MelonLoader;
using Mono.Cecil;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;

namespace VRCModUpdater.Loader
{
    public class VRCModUpdaterPlugin : MelonPlugin
    {
        public const string VERSION = "1.0.0";

        string targetDirectoryPath  = Path.Combine(MelonHandler.ModsDirectory, "..", "UserData");
        string targetFilePath       = Path.Combine(MelonHandler.ModsDirectory, "..", "UserData", "VRCModUpdater.Core.dll");

        public override void OnApplicationStart()
        {

            if (Environment.GetCommandLineArgs().Contains("--updater-dev"))
            {
                MelonLogger.Msg("Running in dev mode");
                if (File.Exists(targetFilePath))
                {
                    System.Reflection.Assembly.LoadFile(targetFilePath);
                    TryStartCore();
                    return;
                }
                else
                    MelonLogger.Warning("No VRCModUpdater.Core found");

            }
            MelonLogger.Msg("Checking latest version for github");

            string latestVersion = GetLatestVersion();
            if (latestVersion == null)
            {
                MelonLogger.Error("Failed to fetch latest VRCModUpdater.Core version");
                TryStartCore();
                return;
            }
            MelonLogger.Msg("Latest VRCModUpdater.Core version: " + latestVersion);

            string assemblyVersion = null;
            if (File.Exists(targetFilePath))
            {
                try
                {
                    using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(targetFilePath, new ReaderParameters { ReadWrite = true }))
                    {
                        CustomAttribute melonInfoAttribute = assembly.CustomAttributes.First(a => a.AttributeType.Name == "AssemblyFileVersionAttribute");
                        assemblyVersion = melonInfoAttribute.ConstructorArguments[0].Value as string;
                    }
                    MelonLogger.Msg("Installed VRCModUpdater.Core version: " + assemblyVersion);
                }
                catch (Exception e)
                {
                    MelonLogger.Error("Failed to load VRCModUpdater.Core. Redownloading.\n" + e);
                }
            }

            if (assemblyVersion != latestVersion)
                UpdateCore(latestVersion);

            TryStartCore();

        }

        private string GetLatestVersion()
        {
            string githubResponse;
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://api.github.com/repos/Slaynash/VRCModUpdater/releases/latest");
                request.Method = "GET";
                request.KeepAlive = true;
                request.ContentType = "application/x-www-form-urlencoded";
                request.UserAgent = $"VRCModUpdater/{VERSION}";
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                using (StreamReader requestReader = new StreamReader(response.GetResponseStream()))
                {
                    githubResponse = requestReader.ReadToEnd();
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error("Failed to fetch latest plugin version info from github:\n" + e);
                return null;
            }

            JObject obj = JsonConvert.DeserializeObject(githubResponse) as JObject;

            return obj.GetValue("tag_name")?.ToString();
        }

        private void UpdateCore(string version)
        {
            MelonLogger.Msg("Downloading VRCModUpdater.Core");

            byte[] data;
            using (WebClient wc = new WebClient())
            {
                data = wc.DownloadData($"https://github.com/Slaynash/VRCModUpdater/releases/download/{version}/VRCModUpdater.Core.dll");
            }

            File.WriteAllBytes(targetFilePath, data);
        }

        private void TryStartCore()
        {
            if (File.Exists(targetFilePath))
            {
                Assembly assembly = System.Reflection.Assembly.LoadFile(targetFilePath);
                assembly.GetType("VRCModUpdater.Core.VRCModUpdaterCore").GetMethod("Start").Invoke(null, null);
            }
            else
                MelonLogger.Warning("No VRCModUpdater.Core found");
        }
    }
}
