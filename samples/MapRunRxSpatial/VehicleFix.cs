using RxSpatial.Geo;

namespace MapRunRxSpatial;

/// <summary>
/// Where a vehicle is during a push: its centre and, on the Overlap tab, the reach disc it
/// pushes instead of the point (null elsewhere). The marker is drawn at the centre, which is
/// kept alongside the pushed location rather than derived from the reach; the map draws the
/// reach's outline from the same <see cref="GeoCircle"/> the queries see.
/// </summary>
internal readonly record struct VehicleFix(GeoPoint Centre, GeoCircle? Reach);
