using System;
namespace VRCModUpdater.Core
{
    public class ModDetail
    {
        public string name;
        public string version;
        public string filepath;
        public string downloadUrl;
        public string hash;
        public int approvalStatus;
        public string[] dependencies = new string[0];
        public string[] optDependencies = new string[0];
        public DateTime lastMod;
        public DateTime lastAccess;

        ModDetail() { }

        public ModDetail(string name, string version, string filepath)
        {
            this.name = name;
            this.version = version;
            this.filepath = filepath;
        }

        public ModDetail(string name, string version, string filepath, string[] dependencies, string[] optDependencies)
        {
            this.name = name;
            this.version = version;
            this.filepath = filepath;
            this.dependencies = dependencies;
            this.optDependencies = optDependencies;
        }

        public ModDetail(string name, string version, string filepath, string[] dependencies, string[] optDependencies, DateTime lastMod, DateTime lastAccess)
        {
            this.name = name;
            this.version = version;
            this.filepath = filepath;
            this.dependencies = dependencies;
            this.optDependencies = optDependencies;
            this.lastMod = lastMod;
            this.lastAccess = lastAccess;
        }

        public ModDetail(string name, string version, string downloadUrl, string hash)
        {
            this.name = name;
            this.version = version;
            this.downloadUrl = downloadUrl;
            this.hash = hash;
        }

        public ModDetail(string name, string version, string filepath, string downloadUrl, string hash)
        {
            this.name = name;
            this.version = version;
            this.filepath = filepath;
            this.downloadUrl = downloadUrl;
            this.hash = hash;
        }

        public ModDetail(string name, string version, string downloadUrl, string hash, int approvalStatus)
        {
            this.name = name;
            this.version = version;
            this.downloadUrl = downloadUrl;
            this.hash = hash;
            this.approvalStatus = approvalStatus;
        }
    }
}
