# Configuration

Settings are typed (`RxSpatialOptions`) and can be passed straight to the constructor:

```csharp
var vehicle = new RxGeography(new RxSpatialOptions
{
    PreferredDataStruct = RxGeographyDataStruct.RTree,
    IsVisualized = true,
});
```

or bound from any Microsoft.Extensions.Configuration source via the `RxSpatial` section — the parameterless `RxGeography()` constructor uses the ambient `RxSpatialOptions.Default`, typically set once at start-up:

```csharp
IConfiguration configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables(prefix: "RXSPATIAL_")
    .Build();

RxSpatialOptions.Default =
    configuration.GetSection(RxSpatialOptions.SectionName).Get<RxSpatialOptions>() ?? new();
```

```json
{
  "RxSpatial": {
    "PreferredDataStruct": "RTree",
    "QueueLimit": 3,
    "IsVisualized": true,
    "Grid": {
      "LatitudeCells": 10,
      "LongitudeCells": 10,
      "CoverageRegion": {
        "LowerLeftLatitude": 46, "LowerLeftLongitude": -124,
        "UpperRightLatitude": 49, "UpperRightLongitude": -120
      }
    },
    "PlanarGrid": {
      "XCells": 10,
      "YCells": 10,
      "CoverageRegion": { "MinX": 0, "MinY": 0, "MaxX": 1000, "MaxY": 1000 }
    }
  }
}
```

| Option                    | Meaning                                                                                                                                                                                  | Default                           |
|---------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|-----------------------------------|
| `PreferredDataStruct`     | Spatial index for observers: `List`, `HashMap` (grid), `RTree` (R*-tree), or `RumTree`. Historic setting values (`RTREE`, `hashmap`, ...) bind case-insensitively.                       | `List`                            |
| `RumTreeCleaningInterval` | Cleaning-token interval of the RUM-tree backend: garbage-creating operations between purges of one leaf (successor to the legacy `RUMTreeGCInspectionInterval` appSetting).              | `5`                               |
| `QueueLimit`              | Location-push buffer size before `RxSpatialOverloadException` is thrown (buffered mode).                                                                                                 | `1`                               |
| `IsVisualized`            | `true` processes pushes synchronously and forwards raw locations to direct subscribers (used by the WPF demo, the examples and the benchmarks); `false` is the buffered throughput mode. | `false`                           |
| `Grid`                    | Grid backend dimensions and coverage rectangle for `RxGeography` (latitude/longitude).                                                                                                   | `10×10` over `46,-124 .. 49,-120` |
| `PlanarGrid`              | Grid backend dimensions and coverage rectangle for `RxGeometry` (X/Y, in the geometries' coordinate units).                                                                              | `10×10` over `0,0 .. 1000,1000`   |


