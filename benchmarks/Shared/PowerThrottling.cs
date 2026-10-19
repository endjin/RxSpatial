using System.Runtime.InteropServices;

namespace RxSpatial.Benchmarks.Shared;

/// <summary>
/// Opts the current process out of Windows power throttling (EcoQoS). Windows 11 throttles a
/// process it judges to be in the background — one started from a console that is not the
/// foreground window, as an agent's or a CI runner's shell is — and on a hybrid laptop
/// (performance and efficiency cores) the throttled process runs several times slower a few
/// seconds into sustained load. Measured on an i7-13700H: one thread of Vincenty distances fell
/// from 6.0 M/s to 0.9 M/s after ~4 s, which made multi-thread speed-ups taken against a fresh
/// single-thread run read as ~1×. A measurement harness wants the hardware's steady speed, so it
/// asks for high quality of service. Elsewhere this does nothing.
/// </summary>
internal static class PowerThrottling
{
    /// <summary>Set to 1 to keep the operating system's default, for comparison runs.</summary>
    public const string AllowVariable = "RXSPATIAL_ALLOW_POWER_THROTTLING";

    private const int ProcessPowerThrottling = 4;
    private const uint ExecutionSpeed = 1;
    private const uint CurrentVersion = 1;

    /// <summary>Whether the opt-out is in effect for this process.</summary>
    public static bool OptedOut { get; private set; }

    /// <summary>
    /// Requests full execution speed for the process: the execution-speed throttling control
    /// is taken over (control mask) and switched off (state mask 0). Idempotent.
    /// </summary>
    public static bool OptOut()
    {
        if (OptedOut || !OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable(AllowVariable) == "1")
            return OptedOut;

        PowerThrottlingState state = new(CurrentVersion, ExecutionSpeed, 0);
        OptedOut = NativeMethods.SetProcessInformation(
            NativeMethods.GetCurrentProcess(),
            ProcessPowerThrottling,
            ref state,
            (uint)Marshal.SizeOf<PowerThrottlingState>());
        return OptedOut;
    }

    /// <summary>A one-line description for report headers.</summary>
    public static string Describe()
        => !OperatingSystem.IsWindows() ? "not applicable (not Windows)"
        : OptedOut ? "opted out (full execution speed requested)"
        : "operating-system default (may throttle background processes)";

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct PowerThrottlingState(uint version, uint controlMask, uint stateMask)
    {
        public readonly uint Version = version;
        public readonly uint ControlMask = controlMask;
        public readonly uint StateMask = stateMask;
    }

    private static class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetProcessInformation(IntPtr process, int informationClass, ref PowerThrottlingState information, uint size);

        [DllImport("kernel32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr GetCurrentProcess();
    }
}
