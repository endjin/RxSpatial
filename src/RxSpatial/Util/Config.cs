using System.Configuration;

namespace RxSpatial.Util;

internal static class Config
{
    /// <summary>
    /// Reads a value from the host application's <c>appSettings</c>, or null when the key is
    /// absent. (Key lookup is case-insensitive, matching <see cref="ConfigurationManager"/>.)
    /// </summary>
    public static string? GetAppConfig(string key) => ConfigurationManager.AppSettings[key];
}
