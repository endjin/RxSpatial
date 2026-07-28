using System.Runtime.InteropServices;

namespace RxSpatial;

/// <summary>
/// Sub-microsecond UTC clock backed by the Windows <c>GetSystemTimePreciseAsFileTime</c> API.
/// </summary>
public static class HighResolutionDateTime
{
    static HighResolutionDateTime()
    {
        try
        {
            GetSystemTimePreciseAsFileTime(out _);
            IsAvailable = true;
        }
        catch (EntryPointNotFoundException)
        {
            // Not running Windows 8 or higher.
            IsAvailable = false;
        }
        catch (DllNotFoundException)
        {
            // Not running on Windows at all.
            IsAvailable = false;
        }
    }

    public static bool IsAvailable { get; }

    public static DateTime UtcNow
    {
        get
        {
            if (!IsAvailable)
                throw new InvalidOperationException("High resolution clock isn't available.");

            GetSystemTimePreciseAsFileTime(out long filetime);
            return DateTime.FromFileTimeUtc(filetime);
        }
    }

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern void GetSystemTimePreciseAsFileTime(out long filetime);
}
