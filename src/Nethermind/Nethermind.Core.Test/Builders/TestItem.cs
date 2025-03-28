// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Net;
using DotNetty.Common.Utilities;
using Nethermind.Core.Crypto;
using Nethermind.Crypto;
using Nethermind.Int256;
using Nethermind.Serialization.Json;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Core.Test.Builders
{
    public static partial class TestItem
    {
        public static Random Random { get; } = new(1337); // 1337 - to make tests predictable, reproducible
        private static readonly AccountDecoder _accountDecoder = new();

        static TestItem()
        {
            NonZeroBloom = new Bloom();
            NonZeroBloom.Set(KeccakA.Bytes);

            PrivateKeys = new PrivateKey[255];
            PublicKeys = new PublicKey[255];
            Addresses = new Address[255];
            Keccaks = new Hash256[255];
            ValueKeccaks = new ValueHash256[255];

            for (byte i = 1; i > 0; i++) // this will wrap around
            {
                byte[] bytes = new byte[32];
                bytes[31] = i;
                PrivateKeys[i - 1] = new PrivateKey(bytes);
                PublicKeys[i - 1] = PrivateKeys[i - 1].PublicKey;
                Addresses[i - 1] = PublicKeys[i - 1].Address;
                Keccaks[i - 1] = Keccak.Compute(PublicKeys[i - 1].Bytes);
                ValueKeccaks[i - 1] = Keccaks[i - 1];
            }

            byte[] r = new byte[32];
            byte[] s = new byte[32];
            r[1] = 1;
            s[2] = 2;
            RandomSignatureA = new Signature(r, s, 27);
            RandomSignatureB = new Signature(r, s, 28);
        }

        public static Hash256 KeccakFromNumber(int i)
        {
            UInt256 keccakNumber = (UInt256)i;
            byte[] keccakBytes = new byte[32];
            keccakNumber.ToBigEndian(keccakBytes);
            return new Hash256(keccakBytes);
        }

        public static byte[] RandomDataA = { 1, 2, 3 };
        public static byte[] RandomDataB = { 4, 5, 6, 7 };
        public static byte[] RandomDataC = { 1, 2, 8, 9, 10 };
        public static byte[] RandomDataD = { 1, 2, 8, 9, 10, 17 };

        public static Hash256 KeccakA = Keccak.Compute("A");
        public static Hash256 KeccakB = Keccak.Compute("B");
        public static Hash256 KeccakC = Keccak.Compute("C");
        public static Hash256 KeccakD = Keccak.Compute("D");
        public static Hash256 KeccakE = Keccak.Compute("E");
        public static Hash256 KeccakF = Keccak.Compute("F");
        public static Hash256 KeccakG = Keccak.Compute("G");
        public static Hash256 KeccakH = Keccak.Compute("H");

        public static PrivateKey PrivateKeyA = new("010102030405060708090a0b0c0d0e0f000102030405060708090a0b0c0d0e0f");
        public static PrivateKey PrivateKeyB = new("020102030405060708090a0b0c0d0e0f000102030405060708090a0b0c0d0e0f");
        public static PrivateKey PrivateKeyC = new("030102030405060708090a0b0c0d0e0f000102030405060708090a0b0c0d0e0f");
        public static PrivateKey PrivateKeyD = new("040102030405060708090a0b0c0d0e0f000102030405060708090a0b0c0d0e0f");
        public static PrivateKey PrivateKeyE = new("050102030405060708090a0b0c0d0e0f000102030405060708090a0b0c0d0e0f");
        public static PrivateKey PrivateKeyF = new("060102030405060708090a0b0c0d0e0f000102030405060708090a0b0c0d0e0f");

        public static PublicKey PublicKeyA = PrivateKeyA.PublicKey;
        public static PublicKey PublicKeyB = PrivateKeyB.PublicKey;
        public static PublicKey PublicKeyC = PrivateKeyC.PublicKey;
        public static PublicKey PublicKeyD = PrivateKeyD.PublicKey;
        public static PublicKey PublicKeyE = PrivateKeyE.PublicKey;
        public static PublicKey PublicKeyF = PrivateKeyF.PublicKey;

        public static PrivateKey IgnoredPrivateKey = new("040102030405060708090a0b0c0d0e0f0001abe120919026fffff12155555555");
        public static PublicKey IgnoredPublicKey = IgnoredPrivateKey.PublicKey;

        public static PrivateKey[] PrivateKeys;
        public static PublicKey[] PublicKeys;
        public static Address[] Addresses;
        public static Hash256[] Keccaks;
        public static ValueHash256[] ValueKeccaks;

        public static Address AddressA = PublicKeyA.Address;
        public static Address AddressB = PublicKeyB.Address;
        public static Address AddressC = PublicKeyC.Address;
        public static Address AddressD = PublicKeyD.Address;
        public static Address AddressE = PublicKeyE.Address;
        public static Address AddressF = PublicKeyF.Address;

        public static readonly Signature RandomSignatureA;
        public static readonly Signature RandomSignatureB;

        public static Withdrawal WithdrawalA_1Eth = new() { Address = AddressA, Index = 1, ValidatorIndex = 2001, AmountInGwei = 1_000_000_000 };
        public static Withdrawal WithdrawalB_2Eth = new() { Address = AddressB, Index = 2, ValidatorIndex = 2002, AmountInGwei = 2_000_000_000 };
        public static Withdrawal WithdrawalC_3Eth = new() { Address = AddressC, Index = 3, ValidatorIndex = 2003, AmountInGwei = 3_000_000_000 };
        public static Withdrawal WithdrawalD_4Eth = new() { Address = AddressD, Index = 4, ValidatorIndex = 2004, AmountInGwei = 4_000_000_000 };
        public static Withdrawal WithdrawalE_5Eth = new() { Address = AddressE, Index = 5, ValidatorIndex = 2005, AmountInGwei = 5_000_000_000 };
        public static Withdrawal WithdrawalF_6Eth = new() { Address = AddressF, Index = 6, ValidatorIndex = 2006, AmountInGwei = 6_000_000_000 };

        public static byte[] SignatureBytes = [.. new Signature("0x9242685bf161793cc25603c231bc2f568eb630ea16aa137d2664ac80388256084f8ae3bd7535248d0bd448298cc2e2071e56992d0774dc340c368ae950852ada1c").Bytes, .. KeccakA.Bytes];

        public static TestExecutionRequest ExecutionRequestA = new() { RequestType = 0, RequestDataParts = [PublicKeyA.Bytes.Slice(0, 48), KeccakA.Bytes.ToArray(), BitConverter.GetBytes((ulong)1_000_000_000), SignatureBytes, BitConverter.GetBytes((ulong)1)] };
        public static TestExecutionRequest ExecutionRequestB = new() { RequestType = 0, RequestDataParts = [PublicKeyB.Bytes.Slice(0, 48), KeccakB.Bytes.ToArray(), BitConverter.GetBytes((ulong)2_000_000_000), SignatureBytes, BitConverter.GetBytes((ulong)2)] };
        public static TestExecutionRequest ExecutionRequestC = new() { RequestType = 0, RequestDataParts = [PublicKeyC.Bytes.Slice(0, 48), KeccakC.Bytes.ToArray(), BitConverter.GetBytes((ulong)3_000_000_000), SignatureBytes, BitConverter.GetBytes((ulong)3)] };
        public static TestExecutionRequest ExecutionRequestD = new() { RequestType = 1, RequestDataParts = [AddressA.Bytes, PublicKeyA.Bytes.Slice(0, 48), BitConverter.GetBytes((ulong)1_000_000_000)] };
        public static TestExecutionRequest ExecutionRequestE = new() { RequestType = 1, RequestDataParts = [AddressB.Bytes, PublicKeyB.Bytes.Slice(0, 48), BitConverter.GetBytes((ulong)2_000_000_000)] };
        public static TestExecutionRequest ExecutionRequestF = new() { RequestType = 1, RequestDataParts = [AddressC.Bytes, PublicKeyC.Bytes.Slice(0, 48), BitConverter.GetBytes((ulong)3_000_000_000)] };
        public static TestExecutionRequest ExecutionRequestG = new() { RequestType = 2, RequestDataParts = [AddressA.Bytes, PublicKeyA.Bytes.Slice(0, 48), PublicKeyB.Bytes.Slice(0, 48)] };
        public static TestExecutionRequest ExecutionRequestH = new() { RequestType = 2, RequestDataParts = [AddressB.Bytes, PublicKeyB.Bytes.Slice(0, 48), PublicKeyC.Bytes.Slice(0, 48)] };
        public static TestExecutionRequest ExecutionRequestI = new() { RequestType = 2, RequestDataParts = [AddressC.Bytes, PublicKeyC.Bytes.Slice(0, 48), PublicKeyA.Bytes.Slice(0, 48)] };

        public static IPEndPoint IPEndPointA = IPEndPoint.Parse("10.0.0.1");
        public static IPEndPoint IPEndPointB = IPEndPoint.Parse("10.0.0.2");
        public static IPEndPoint IPEndPointC = IPEndPoint.Parse("10.0.0.3");
        public static IPEndPoint IPEndPointD = IPEndPoint.Parse("10.0.0.4");
        public static IPEndPoint IPEndPointE = IPEndPoint.Parse("10.0.0.5");
        public static IPEndPoint IPEndPointF = IPEndPoint.Parse("10.0.0.6");

        public static Bloom NonZeroBloom;

        public static T CloneObject<T>(T value)
        {
            var serializer = new EthereumJsonSerializer();
            string data = serializer.Serialize(value);
            return serializer.Deserialize<T>(data);
        }

        public static Address GetRandomAddress(Random? random = null)
        {
            byte[] bytes = new byte[20];
            (random ?? Random).NextBytes(bytes);
            return new Address(bytes);
        }

        public static Hash256 GetRandomKeccak(Random? random = null)
        {
            byte[] bytes = new byte[32];
            (random ?? Random).NextBytes(bytes);
            return new Hash256(bytes);
        }

        public static Account GenerateRandomAccount(Random? random = null)
        {
            random ??= Random;

            Account account = new(
                (UInt256)random.Next(1000),
                (UInt256)random.Next(1000),
                Keccak.EmptyTreeHash,
                Keccak.OfAnEmptyString);

            return account;
        }

        public static byte[] GenerateRandomAccountRlp(AccountDecoder? accountDecoder = null)
        {
            accountDecoder ??= _accountDecoder;
            Account account = GenerateRandomAccount();
            byte[] value = accountDecoder.Encode(account).Bytes;
            return value;
        }

        public static Account GenerateIndexedAccount(int index)
        {
            Account account = new(
                (UInt256)index,
                (UInt256)index);

            return account;
        }

        public static byte[] GenerateIndexedAccountRlp(int index, AccountDecoder? accountDecoder = null)
        {
            accountDecoder ??= _accountDecoder;

            Account account = GenerateIndexedAccount(index);
            byte[] value = accountDecoder.Encode(account).Bytes;
            return value;
        }

        public static UInt256 GetRandomAmount(Random? random = null)
        {
            Span<byte> buffer = stackalloc byte[32];
            (random ?? Random).NextBytes(buffer);
            return new UInt256(buffer);
        }
    }
}
