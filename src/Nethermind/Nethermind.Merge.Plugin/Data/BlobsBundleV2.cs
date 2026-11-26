// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using CkzgLib;
using Nethermind.Core;
using Nethermind.Serialization.Json;
using System;
using System.Text.Json.Serialization;

namespace Nethermind.Merge.Plugin.Data;

/// <summary>
/// Holds blobs of a block.
///
/// See <a href="https://github.com/ethereum/execution-apis/blob/main/src/engine/experimental/blob-extension.md#blobsbundlev1">BlobsBundleV1</a>
/// </summary>
public class BlobsBundleV2
{
    public BlobsBundleV2(Block block) : this(block, skipUnwrapped: false)
    {
    }

    /// <summary>
    /// Builds the bundle from the blob transactions that carry their network wrapper, leaving out those that come
    /// without one, as the transactions of a testing payload taken from a block do.
    /// </summary>
    public static BlobsBundleV2 FromWrappedTransactions(Block block) => new(block, skipUnwrapped: true);

    private BlobsBundleV2(Block block, bool skipUnwrapped)
    {
        int blobsCount = 0;
        foreach (Transaction? tx in block.Transactions)
        {
            if (skipUnwrapped && !IsWrapped(tx))
            {
                continue;
            }

            blobsCount += tx?.GetBlobCount() ?? 0;
        }

        Commitments = new byte[blobsCount][];
        Blobs = new byte[blobsCount][];
        Proofs = new byte[blobsCount * Ckzg.CellsPerExtBlob][];
        int blockIndex = 0;

        foreach (Transaction? tx in block.Transactions)
        {
            // Must match the type-agnostic GetBlobCount sizing above, or the bundle silently keeps nulls.
            if (!tx.CarriesBlobs)
            {
                continue;
            }

            if (tx is not { NetworkWrapper: ShardBlobNetworkWrapper { Version: ProofVersion.V1 } wrapper })
            {
                if (skipUnwrapped)
                {
                    continue;
                }

                throw new ArgumentException("Shard blob transaction should contain network wrapper data");
            }

            for (int txIndex = 0;
                 txIndex < wrapper.Blobs.Length;
                 blockIndex++, txIndex++)
            {
                Commitments[blockIndex] = wrapper.Commitments[txIndex];
                Blobs[blockIndex] = wrapper.Blobs[txIndex];
                for (int cellIndex = 0; cellIndex < Ckzg.CellsPerExtBlob; cellIndex++)
                {
                    Proofs[blockIndex * Ckzg.CellsPerExtBlob + cellIndex] = wrapper.GetProofs(txIndex * Ckzg.CellsPerExtBlob + cellIndex).ToArray();
                }
            }
        }
    }

    private static bool IsWrapped(Transaction? tx) =>
        tx is { NetworkWrapper: ShardBlobNetworkWrapper { Version: ProofVersion.V1 } };

    [JsonConstructor]
    public BlobsBundleV2(byte[][] commitments, byte[][] blobs, byte[][] proofs)
    {
        Commitments = commitments;
        Blobs = blobs;
        Proofs = proofs;
    }

    [JsonConverter(typeof(BlobsBundleByteArrayArrayConverter))] public byte[][] Commitments { get; }
    [JsonConverter(typeof(BlobsBundleByteArrayArrayConverter))] public byte[][] Blobs { get; }
    [JsonConverter(typeof(BlobsBundleByteArrayArrayConverter))] public byte[][] Proofs { get; }
}

