// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Crypto;
using Nethermind.Int256;
using System.Text.Json.Serialization;

namespace Nethermind.JsonRpc.Modules.Parity
{
    public class EthProtocolInfo
    {
        [JsonPropertyName("version")]
        public byte Version { get; set; }

        [JsonPropertyName("difficulty"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public UInt256? Difficulty { get; set; } = UInt256.Zero;

        [JsonPropertyName("head")]
        public Hash256 HeadHash { get; set; }
    }
}
