// SPDX-FileCopyrightText: 2026 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using CkzgLib;
using Nethermind.Serialization.Json;

namespace Nethermind.Merge.Plugin.Data;

[JsonConverter(typeof(BlobCellsAndProofsJsonConverter))]
public class BlobCellsAndProofs
{
    public bool Available { get; init; }
    public byte[]?[]? BlobCells { get; init; }
    public byte[]?[]? Proofs { get; init; }
    public static BlobCellsAndProofs Unavailable { get; } = new() { Available = false };
}

/// <summary>
/// Serializes to the engine API `BlobCellsAndProofsV1` JSON shape: a compact
/// `blob_cells` / `proofs` pair holding only the requested cells in ascending
/// cell-index order (matching geth), or `null` when the blob is unavailable.
/// The positional in-memory layout (cell at its absolute index) is kept for
/// the SSZ codec, which encodes a fixed 128-entry wire structure.
/// </summary>
public class BlobCellsAndProofsJsonConverter : JsonConverter<BlobCellsAndProofs>
{
    public override BlobCellsAndProofs Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
        => throw new NotSupportedException(
            "BlobCellsAndProofs is a response-only type.");

    public override void Write(
        Utf8JsonWriter writer,
        BlobCellsAndProofs value,
        JsonSerializerOptions options)
    {
        if (!value.Available || value.BlobCells is null || value.Proofs is null)
        {
            writer.WriteNullValue();
            return;
        }
        writer.WriteStartObject();

        writer.WritePropertyName("blob_cells"u8);
        writer.WriteStartArray();
        int cells = Math.Min(value.BlobCells.Length, Ckzg.CellsPerExtBlob);
        for (int i = 0; i < cells; i++)
        {
            // Cell buffers may be pooled and larger than a cell; slice exactly.
            if (value.BlobCells[i] is { } cell)
                ByteArrayConverter.Convert(writer, cell.AsSpan(0, Ckzg.BytesPerCell), skipLeadingZeros: false);
        }
        writer.WriteEndArray();

        writer.WritePropertyName("proofs"u8);
        writer.WriteStartArray();
        int proofs = Math.Min(value.Proofs.Length, Ckzg.CellsPerExtBlob);
        for (int i = 0; i < proofs; i++)
        {
            if (value.Proofs[i] is { } proof)
                ByteArrayConverter.Convert(writer, proof.AsSpan(0, Ckzg.BytesPerProof), skipLeadingZeros: false);
        }
        writer.WriteEndArray();

        writer.WriteEndObject();
    }
}
