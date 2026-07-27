// SPDX-FileCopyrightText: 2026 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using CkzgLib;
using Nethermind.Core.Collections;
using Nethermind.JsonRpc;
using Nethermind.Merge.Plugin.Data;
using Nethermind.Serialization.Json;
using NUnit.Framework;

namespace Nethermind.Merge.Plugin.Test;

[TestFixture]
public class BlobCellsAndProofsJsonTests
{
    [Test]
    public void Serializes_compact_blob_cells_and_proofs()
    {
        byte[]?[] cells = new byte[]?[Ckzg.CellsPerExtBlob];
        byte[]?[] proofs = new byte[]?[Ckzg.CellsPerExtBlob];
        cells[3] = Enumerable.Repeat((byte)0xaa, Ckzg.BytesPerCell).ToArray();
        cells[100] = Enumerable.Repeat((byte)0xbb, Ckzg.BytesPerCell).ToArray();
        proofs[3] = Enumerable.Repeat((byte)0x0c, Ckzg.BytesPerProof).ToArray();
        proofs[100] = Enumerable.Repeat((byte)0x0d, Ckzg.BytesPerProof).ToArray();

        BlobCellsAndProofs value = new()
        {
            Available = true,
            BlobCells = cells,
            Proofs = proofs
        };
        string json = new EthereumJsonSerializer().Serialize(value);

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        // Engine API `BlobCellsAndProofsV1`: exactly blob_cells + proofs,
        // compact (only present cells), ascending cell-index order.
        Assert.That(root.EnumerateObject().Count(), Is.EqualTo(2));
        JsonElement blobCells = root.GetProperty("blob_cells");
        JsonElement proofsElement = root.GetProperty("proofs");
        Assert.That(blobCells.GetArrayLength(), Is.EqualTo(2));
        Assert.That(proofsElement.GetArrayLength(), Is.EqualTo(2));
        Assert.That(blobCells[0].GetString(), Does.StartWith("0xaaaa"));
        Assert.That(blobCells[1].GetString(), Does.StartWith("0xbbbb"));
        Assert.That(blobCells[0].GetString()!.Length,
            Is.EqualTo(2 + 2 * Ckzg.BytesPerCell));
        Assert.That(proofsElement[0].GetString()!.Length,
            Is.EqualTo(2 + 2 * Ckzg.BytesPerProof));
    }

    [Test]
    public void Serializes_unavailable_as_null()
    {
        string json = new EthereumJsonSerializer()
            .Serialize(BlobCellsAndProofs.Unavailable);
        Assert.That(json, Is.EqualTo("null"));
    }

    [Test]
    public async Task Streamed_response_matches_plain_serialization()
    {
        byte[]?[] cells = new byte[]?[Ckzg.CellsPerExtBlob];
        byte[]?[] proofs = new byte[]?[Ckzg.CellsPerExtBlob];
        cells[1] = Enumerable.Repeat((byte)0xaa, Ckzg.BytesPerCell).ToArray();
        cells[127] = Enumerable.Repeat((byte)0xbb, Ckzg.BytesPerCell).ToArray();
        proofs[1] = Enumerable.Repeat((byte)0x0c, Ckzg.BytesPerProof).ToArray();
        proofs[127] = Enumerable.Repeat((byte)0x0d, Ckzg.BytesPerProof).ToArray();

        BlobCellsAndProofs?[] response =
        [
            null, // non-existing versioned hash
            new() { Available = true, BlobCells = cells, Proofs = proofs },
        ];
        BlobsV4DirectResponse direct = new(
            new ArrayPoolList<byte[]?>(2, 2),
            new ArrayPoolList<System.ReadOnlyMemory<byte[]>>(2, 2),
            response,
            2);

        using MemoryStream stream = new();
        PipeWriter writer = PipeWriter.Create(
            stream, new StreamPipeWriterOptions(leaveOpen: true));
        await direct.WriteToAsync(writer, CancellationToken.None);
        await writer.FlushAsync();
        await writer.CompleteAsync();
        string streamed = System.Text.Encoding.UTF8.GetString(stream.ToArray());

        string plain = new EthereumJsonSerializer()
            .Serialize<System.Collections.Generic.IReadOnlyList<BlobCellsAndProofs?>>(response);
        Assert.That(
            JsonNode.DeepEquals(JsonNode.Parse(streamed), JsonNode.Parse(plain)),
            Is.True,
            () => $"streamed: {streamed[..200]}... plain: {plain[..200]}...");

        using JsonDocument doc = JsonDocument.Parse(streamed);
        Assert.That(doc.RootElement[0].ValueKind, Is.EqualTo(JsonValueKind.Null));
        JsonElement entry = doc.RootElement[1];
        Assert.That(entry.EnumerateObject().Count(), Is.EqualTo(2));
        Assert.That(entry.GetProperty("blob_cells").GetArrayLength(), Is.EqualTo(2));
        Assert.That(entry.GetProperty("proofs").GetArrayLength(), Is.EqualTo(2));
    }
}
