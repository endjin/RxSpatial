using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace RxSpatial
{
    class LocationPush
    {
        public LocationPush(Object content)
        {
            _content = content;
            _startTime = HighResolutionDateTime.UtcNow;
        }
        public Object content
        { get { return this._content; } }
        public double latency
        {
            get { return (this._endTime - this._startTime).Milliseconds; }
        }
        public void finishProc()
        {
            _endTime = HighResolutionDateTime.UtcNow;
        }
        private Object _content;
        private DateTime _startTime;
        private DateTime _endTime;
    }

    public static class HighResolutionDateTime
    {
        public static bool IsAvailable { get; private set; }

        [DllImport("Kernel32.dll", CallingConvention = CallingConvention.Winapi)]
        private static extern void GetSystemTimePreciseAsFileTime(out long filetime);

        public static DateTime UtcNow
        {
            get
            {
                if (!IsAvailable)
                {
                    throw new InvalidOperationException(
                        "High resolution clock isn't available.");
                }

                long filetime;
                GetSystemTimePreciseAsFileTime(out filetime);

                return DateTime.FromFileTimeUtc(filetime);
            }
        }

        static HighResolutionDateTime()
        {
            try
            {
                long filetime;
                GetSystemTimePreciseAsFileTime(out filetime);
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
    }
}
