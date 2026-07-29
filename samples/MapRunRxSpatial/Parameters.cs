using System;

namespace MapRunRxSpatial
{
    class Parameters
    {
        /// <summary>Folder holding the recorded vehicle trajectories (0.txt .. 19.txt).</summary>
        public static string dataPath = System.IO.Path.Combine(AppContext.BaseDirectory, "trafficData") + System.IO.Path.DirectorySeparatorChar;
    }
}
