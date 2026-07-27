// SPDX-FileCopyrightText: 2026 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nethermind.Serialization.Json;

/// <summary>
/// Converts a <see cref="BitArray"/> to and from a DATA-style hex string,
/// storing bit <c>i</c> at byte <c>i / 8</c>, bit position <c>i % 8</c>
/// (little-endian bit order), as used by the engine API bitmaps
/// (<c>indicesBitarray</c>, <c>custodyColumns</c>).
/// </summary>
public class BitArrayConverter : JsonConverter<BitArray>
{
    public override BitArray? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        byte[]? bytes = ByteArrayConverter.ConvertData(ref reader);
        return bytes is null ? null : new BitArray(bytes);
    }

    public override void Write(
        Utf8JsonWriter writer,
        BitArray value,
        JsonSerializerOptions options)
    {
        byte[] bytes = new byte[(value.Length + 7) / 8];
        value.CopyTo(bytes, 0);
        ByteArrayConverter.Convert(writer, bytes, skipLeadingZeros: false);
    }
}
