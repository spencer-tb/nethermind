// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Core
{
    public class RlpBase
    {
        public RlpBase(byte[] bytes)
        {
            Bytes = bytes;
        }

        protected RlpBase(byte singleByte)
        {
            Bytes = new[] { singleByte };
        }

        public byte[] Bytes { get; protected set; }
        public byte this[int index] => Bytes[index];

        public int Length => Bytes.Length;
    }
}
