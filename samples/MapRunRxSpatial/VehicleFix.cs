namespace MapRunRxSpatial;

/// <summary>
/// Where a vehicle is during a push: its centre and, on the Overlap tab, the radius of the reach
/// disc it pushes instead of the point (0 elsewhere). The marker is drawn at the centre, which
/// is kept alongside the pushed location rather than derived from the reach polygon.
/// </summary>
internal readonly record struct VehicleFix(GeoPoint Centre, double Reach);
