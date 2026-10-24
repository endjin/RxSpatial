// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

namespace MapRunRxSpatial;

/// <summary>
/// What a drawn area means: on the Geofence tab, the restricted/confinement zone alert of
/// ICDE 2017 Fig. 4a (and the DEBS 2016 section 3.4 family locator); on the Overlap tab, a
/// region the vehicles' reaches are intersected with.
/// </summary>
internal enum AreaKind
{
    /// <summary>A vehicle must stay out: being inside it is an alert.</summary>
    Restricted,

    /// <summary>A vehicle must stay in: being outside every confinement area is an alert.</summary>
    Confinement,

    /// <summary>
    /// Overlap tab: a region of interest (the DEBS 2016 section 3.1 "range over the map"); the
    /// part of each vehicle's reach inside it is shaded.
    /// </summary>
    Zone,
}
