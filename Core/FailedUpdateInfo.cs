namespace VRCModUpdater.Core
{
    public class FailedUpdateInfo
    {
        public ModDetail mod;
        public FailedUpdateReason reason;
        public string message;

        public FailedUpdateInfo(ModDetail mod, FailedUpdateReason reason, string message)
        {
            this.mod = mod;
            this.reason = reason;
            this.message = message;
        }
    }
}