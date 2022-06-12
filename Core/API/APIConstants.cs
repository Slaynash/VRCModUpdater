using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VRCModUpdater.Core.API
{
    public static class APIConstants
    {
        public const string API_DOMAIN_URL = "https://api.vrcmg.com/";
        public const int API_VERSION = 1;
        public static readonly string API_ROOT_URL = $"{API_DOMAIN_URL}v{API_VERSION}/";
        public static readonly string MODS_ENDPOINT = $"{API_ROOT_URL}mods";

        public const string USER_AGENT = "VRCModUpdater/" + VRCModUpdaterCore.VERSION;
    }
}
