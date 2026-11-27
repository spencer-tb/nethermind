// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nethermind.Core.Crypto;

namespace Ethereum.Test.Base
{
    public class EthereumTestResult
    {
        public EthereumTestResult(string? name, string? fork, bool pass)
        {
            Name = name ?? "unnamed";
            Fork = fork ?? "unknown";
            Pass = pass;
        }

        public EthereumTestResult(string? name, string? fork, string loadFailure)
        {
            Name = name ?? "unnamed";
            Fork = fork ?? "unknown";
            Pass = false;
            LoadFailure = loadFailure;
            Error = loadFailure;
        }

        public EthereumTestResult(string? name, string? loadFailure)
            : this(name, null, loadFailure)
        {
        }

        [JsonIgnore]
        public string? LoadFailure { get; set; }
        public string Name { get; set; }
        public bool Pass { get; set; }
        public string Fork { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Error { get; set; }

        [JsonIgnore]
        public double TimeInMs { get; set; }

        /// <summary>
        /// Post-execution state root. Only populated by state tests; blockchain/engine and
        /// transaction results leave it null, so the field is omitted from their JSON.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Hash256? StateRoot { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Hash256? LastBlockHash { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? LastPayloadStatus { get; set; }

        /// <summary>
        /// Every block or payload the client rejected, in fixture order, with the client's own error. Only
        /// blockchain/engine results carry it; the runner reports the reason and leaves checking it to the consumer.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<BlockRejection>? Rejections { get; set; }

        /// <summary>
        /// The number of payloads the client built. Only build test results carry it.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Built { get; set; }

        /// <summary>
        /// Every field in which a built payload differs from the fixture's, in fixture order. Only build test
        /// results carry it.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<BuildMismatch>? Mismatches { get; set; }
    }

    /// <param name="Index">The payload's position in the fixture's <c>engineNewPayloads</c>.</param>
    /// <param name="Field">The field's name in the payload JSON, or <c>executionRequests</c>.</param>
    /// <param name="Expected">The fixture's value, as in its JSON.</param>
    /// <param name="Got">The built payload's value, as in its JSON.</param>
    public sealed record BuildMismatch(int Index, string Field, JsonElement Expected, JsonElement Got);

    /// <summary>
    /// What building an engine fixture's valid payloads found. Kept outside the run so that a test that throws still
    /// reports it.
    /// </summary>
    public sealed class PayloadBuildOutcome
    {
        /// <summary>The number of payloads the client built.</summary>
        public int Built { get; set; }

        /// <summary>The number of payloads the client refused to build.</summary>
        public int Refused { get; set; }

        /// <summary>Every field in which a built payload differs from the fixture's, in fixture order.</summary>
        public List<BuildMismatch> Mismatches { get; } = [];
    }

    /// <param name="Index">The block's position in the fixture's <c>blocks</c>, or the payload's in <c>engineNewPayloads</c>.</param>
    /// <param name="Hash">The rejected block's hash, when the client computed one.</param>
    /// <param name="Error">The client's error, verbatim.</param>
    public sealed record BlockRejection(
        int Index,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Hash256? Hash,
        string Error);
}
