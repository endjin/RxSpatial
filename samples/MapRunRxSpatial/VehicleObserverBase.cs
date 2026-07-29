namespace MapRunRxSpatial;

/// <summary>Base for demo observers that colour a vehicle's marker from query results.</summary>
internal abstract class VehicleObserverBase(VehicleMarker marker)
{
    protected VehicleMarker Marker { get; } = marker;
}
