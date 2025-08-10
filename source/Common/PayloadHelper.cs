// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Azure.Maps.Mcp.Common;

/// <summary>
/// Standard payload builders to keep tool outputs consistent.
/// Each tool should return one of these shapes from the operation in BaseMapsTool.ExecuteWithErrorHandling:
/// { query, items, summary? }  OR  { query, result, summary? }
/// </summary>
public static class PayloadHelper
{
    public static object Items(object query, IEnumerable<object> items, object? summary = null)
        => new
        {
            query,
            items,
            summary
        };

    public static object Result(object query, object result, object? summary = null)
        => new
        {
            query,
            result,
            summary
        };
}
