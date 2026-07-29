# Prototype source (not compiled)

These files are alternative backend prototypes and experiments from the original
2014-2016 research codebase. They were **never part of the compiled assemblies** —
the original Visual Studio projects excluded them from compilation — and they are
kept here purely as a historical record of the design space explored for the
SIGMOD/DEBS 2016 papers.

| Folder | Origin | Contents |
| --- | --- | --- |
| `rxspatial/` | `RxSpatial` core library | `RxGeography_.cs` (an earlier full `RxGeography` in the `RxSpatial_` namespace) and per-backend front-end variants (`RxGeographyGrid`, `RxGeographyRTree`, `RxGeographyRUMTree`, `RxGeographySqlSpatial`, `RxGeographyTree`). The shipped library instead selects its backend at runtime via the `preferredDataStruct` setting. |
| `maprun/` | `MapRunRxSpatial` WPF demo | `MovingObj.cs`, `IntersctObserver.cs`, `Observer.cs` — superseded by `MovingObject.cs` / `IntersectObserver.cs` / the observer classes in the demo. `SimulationFromText.cs` and `InitTestParams.cs` were compiled in 2016 but unreachable from the UI (their wiring was already commented out); archived during the 2026 defect review. |

They are not referenced by any project and may not compile against the current
library surface.
