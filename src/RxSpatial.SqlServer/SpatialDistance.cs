// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

using System.Data.SqlTypes;

namespace RxSpatial.SqlServer;

/// <summary>
/// The single place where SQL Server's <see cref="SqlDouble"/> distance becomes the
/// <see cref="double"/> the RxSpatial papers declare for the distance queries.
/// </summary>
internal static class SpatialDistance
{
    /// <summary>
    /// An <c>STDistance</c> result as a <see cref="double"/>, or <see cref="double.NaN"/> when
    /// it is <see cref="SqlDouble.Null"/> (e.g. different SRIDs or an empty shape). Mapping
    /// Null to NaN, rather than dropping it, keeps one notification per location update.
    /// </summary>
    public static double ToDouble(SqlDouble distance) => distance.IsNull ? double.NaN : distance.Value;
}
