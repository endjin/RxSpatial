// Copyright (c) The RxSpatial Authors and Contributors. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.
// Modified by Endjin Limited.

namespace RxSpatial;

/// <summary>
/// Thrown when a moving object's location-push buffer exceeds the configured
/// <see cref="RxSpatialOptions.QueueLimit"/> in buffered (non-visualised) mode.
/// </summary>
public sealed class RxSpatialOverloadException : Exception
{
    public RxSpatialOverloadException()
    {
    }

    public RxSpatialOverloadException(string message)
        : base(message)
    {
    }

    public RxSpatialOverloadException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
