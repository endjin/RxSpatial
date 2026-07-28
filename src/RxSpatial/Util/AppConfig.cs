using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Configuration;

namespace RxSpatial.Util
{

    class Config
    {
        public static string GetAppConfig(string strKey)
        {
            foreach (string key in ConfigurationManager.AppSettings)
            {
                if (key.Equals(strKey)) return ConfigurationManager.AppSettings[strKey];
            }
            return null;
        }
    }
}
