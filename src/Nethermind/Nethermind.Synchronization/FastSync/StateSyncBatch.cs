// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Diagnostics;

namespace Nethermind.Synchronization.FastSync
{
    [DebuggerDisplay("Requested Nodes: {RequestedNodes?.Length ?? 0}, Responses: {Responses?.Length ?? 0}, Assigned: {AssignedPeer?.Current}")]
    public class StateSyncBatch
    {
        public StateSyncBatch(StateSyncItem[] requestedNodes)
        {
            RequestedNodes = requestedNodes;
        }

        public StateSyncItem[]? RequestedNodes { get; }

        public byte[][]? Responses { get; set; }

        public int ConsumerId { get; set; }

        public override string ToString()
        {
            return $"{RequestedNodes?.Length ?? 0} state sync requests with {Responses?.Length ?? 0} responses";
        }
    }
}
