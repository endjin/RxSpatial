namespace MapRunRxSpatial;

/// <summary>
/// What a drawn area means on the Geofence tab, after the restricted/confinement zone alert
/// of ICDE 2017 Fig. 4a (and the DEBS 2016 section 3.4 family locator).
/// </summary>
internal enum AreaKind
{
    /// <summary>A vehicle must stay out: being inside it is an alert.</summary>
    Restricted,

    /// <summary>A vehicle must stay in: being outside every confinement area is an alert.</summary>
    Confinement,
}
