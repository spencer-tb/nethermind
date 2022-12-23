// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only 

using System;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Nethermind.Core.Test.Builders;
using Nethermind.Network.P2P;
using Nethermind.Network.P2P.Subprotocols.Snap.Messages;
using Nethermind.State.Snap;
using NUnit.Framework;

namespace Nethermind.Network.Test.P2P.Subprotocols.Snap.Messages
{
    [TestFixture, Parallelizable(ParallelScope.All)]
    public class GetTrieNodesMessageSerializerTests
    {
        [Test]
        public void Roundtrip_NoPaths()
        {
            GetTrieNodesMessage msg = new()
            {
                RequestId = MessageConstants.Random.NextLong(),
                RootHash = TestItem.KeccakA,
                Paths = Array.Empty<PathGroup>(), //new MeasuredArray<MeasuredArray<byte[]>>(<MeasuredArray<byte[]>>()) ,
                Bytes = 10
            };
            GetTrieNodesMessageSerializer serializer = new();

            SerializerTester.TestZero(serializer, msg);
        }

        [Test]
        public void Roundtrip_OneAccountPath()
        {
            GetTrieNodesMessage msg = new()
            {
                RequestId = MessageConstants.Random.NextLong(),
                RootHash = TestItem.KeccakA,
                Paths = new PathGroup[]
                    {
                        new PathGroup(){Group = new []{TestItem.RandomDataA}}
                    },
                Bytes = 10
            };
            GetTrieNodesMessageSerializer serializer = new();

            SerializerTester.TestZero(serializer, msg);
        }

        [Test]
        public void Roundtrip_MultiplePaths()
        {
            GetTrieNodesMessage msg = new()
            {
                RequestId = MessageConstants.Random.NextLong(),
                RootHash = TestItem.KeccakA,
                Paths = new PathGroup[]
                    {
                        new PathGroup(){Group = new []{TestItem.RandomDataA, TestItem.RandomDataB}},
                        new PathGroup(){Group = new []{TestItem.RandomDataC}}
                    },
                Bytes = 10
            };
            GetTrieNodesMessageSerializer serializer = new();

            SerializerTester.TestZero(serializer, msg);
        }
    }
}
