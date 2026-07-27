// SPDX-FileCopyrightText: 2026 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections;
using System.Text.Json;
using Nethermind.Serialization.Json;
using NUnit.Framework;

namespace Nethermind.Core.Test.Json;

[TestFixture]
public class BitArrayConverterTests : ConverterTestBase<BitArray>
{
    private static readonly JsonSerializerOptions _options =
        new() { Converters = { new BitArrayConverter() } };

    [Test]
    public void Roundtrip_preserves_bits()
    {
        BitArray bits = new(128);
        bits.Set(0, true);
        bits.Set(7, true);
        bits.Set(8, true);
        bits.Set(127, true);

        TestConverter(
            bits,
            static (before, after) =>
            {
                if (before.Count != after!.Count) return false;
                for (int i = 0; i < before.Count; i++)
                    if (before.Get(i) != after.Get(i)) return false;
                return true;
            },
            new BitArrayConverter());
    }

    [Test]
    public void Reads_little_endian_bit_order()
    {
        // Bit i lives at byte i / 8, bit position i % 8: bit 0 is the
        // lowest bit of the first byte, bit 120 the lowest of the last.
        BitArray? bits = JsonSerializer.Deserialize<BitArray>(
            "\"0x01000000000000000000000000000001\"", _options);

        Assert.That(bits, Is.Not.Null);
        Assert.That(bits!.Count, Is.EqualTo(128));
        Assert.That(bits.Get(0), Is.True);
        Assert.That(bits.Get(120), Is.True);
        Assert.That(bits.Get(1), Is.False);
        Assert.That(bits.Get(127), Is.False);
    }

    [Test]
    public void Writes_little_endian_bit_order()
    {
        BitArray bits = new(16);
        bits.Set(0, true);
        bits.Set(15, true);

        string json = JsonSerializer.Serialize(bits, _options);

        Assert.That(json, Is.EqualTo("\"0x0180\""));
    }
}
