namespace RxSpatial.Experiments;

/// <summary>Settings and shared state for one invocation of the harness.</summary>
internal sealed class ExperimentContext(bool full, int seed, int objectCap, Report report)
{
    /// <summary>The paper's geofence count for a single moving object (§VI-A, §VI-D).</summary>
    public const int PaperFences = 2000;

    /// <summary>
    /// The input buffer size, the paper's only stated one ("we kept the system's input buffer
    /// to 10", Fig. 12); used for every experiment.
    /// </summary>
    public const int InputBuffer = 10;

    private SeattleWorkload? workload;

    public static IReadOnlyList<Variant> Variants { get; } = Enum.GetValues<Variant>();

    /// <summary>Run at the paper's full scale rather than the quick default.</summary>
    public bool Full { get; } = full;

    public int Seed { get; } = seed;

    /// <summary>The most moving objects a maximum-load search may build.</summary>
    public int ObjectCap { get; } = objectCap;

    public Report Report { get; } = report;

    public SeattleWorkload Workload => workload ??= new SeattleWorkload(Seed, PaperFences);

    public T Scale<T>(T quick, T paper) => Full ? paper : quick;

    public static void Log(string message)
        => Console.WriteLine(FormattableString.Invariant($"[{DateTime.Now:HH:mm:ss}] {message}"));

    /// <summary>One single-object fleet per variant, for experiments that interleave variants.</summary>
    public Dictionary<Variant, Fleet> SingleObjectFleets(int fences, int routeLength)
        => Variants.ToDictionary(v => v, v => new Fleet(v, Workload, fences, InputBuffer, routeLength));
}
