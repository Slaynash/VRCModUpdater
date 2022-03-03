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

        public ModDetail(string name, string version, string filepath)
        {
            this.name = name;
            this.version = version;
            this.filepath = filepath;
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
