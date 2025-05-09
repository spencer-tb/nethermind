// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Equivalency;
using Nethermind.Blockchain.Blocks;
using Nethermind.Blockchain.Receipts;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test;
using Nethermind.Core.Test.Builders;
using Nethermind.Crypto;
using Nethermind.Db;
using Nethermind.Serialization.Rlp;
using Nethermind.Specs;
using Nethermind.Specs.Forks;
using NSubstitute;
using NSubstitute.Core;
using NUnit.Framework;

namespace Nethermind.Blockchain.Test.Receipts;


[TestFixture(true)]
[TestFixture(false)]
public class PersistentReceiptStorageTests
{
    private readonly TestSpecProvider _specProvider = new TestSpecProvider(Byzantium.Instance);
    private TestMemColumnsDb<ReceiptsColumns> _receiptsDb = null!;
    private ReceiptsRecovery _receiptsRecovery = null!;
    private IBlockTree _blockTree = null!;
    private IBlockStore _blockStore = null!;
    private readonly bool _useCompactReceipts;
    private ReceiptConfig _receiptConfig = null!;
    private PersistentReceiptStorage _storage = null!;
    private ReceiptArrayStorageDecoder _decoder = null!;

    public PersistentReceiptStorageTests(bool useCompactReceipts)
    {
        _useCompactReceipts = useCompactReceipts;
    }

    [SetUp]
    public void SetUp()
    {
        EthereumEcdsa ethereumEcdsa = new(_specProvider.ChainId);
        _receiptConfig = new ReceiptConfig();
        _receiptsRecovery = new(ethereumEcdsa, _specProvider);
        _receiptsDb = new TestMemColumnsDb<ReceiptsColumns>();
        _receiptsDb.GetColumnDb(ReceiptsColumns.Blocks).Set(Keccak.Zero, Array.Empty<byte>());
        _blockTree = Substitute.For<IBlockTree>();
        _blockStore = Substitute.For<IBlockStore>();
        CreateStorage();
    }

    [TearDown]
    public void TearDown()
    {
        _receiptsDb.Dispose();
    }

    private void CreateStorage()
    {
        _decoder = new ReceiptArrayStorageDecoder(_useCompactReceipts);
        _storage = new PersistentReceiptStorage(
            _receiptsDb,
            _specProvider,
            _receiptsRecovery,
            _blockTree,
            _blockStore,
            _receiptConfig,
            _decoder
        )
        { MigratedBlockNumber = 0 };
    }

    [Test, MaxTime(Timeout.MaxTestTime)]
    public void Returns_null_for_missing_tx()
    {
        Hash256 blockHash = _storage.FindBlockHash(Keccak.Zero);
        blockHash.Should().BeNull();
    }

    [Test, MaxTime(Timeout.MaxTestTime)]
    public void ReceiptsIterator_doesnt_throw_on_empty_span()
    {
        _storage.TryGetReceiptsIterator(1, Keccak.Zero, out ReceiptsIterator iterator);
        iterator.TryGetNext(out _).Should().BeFalse();
    }

    [Test, MaxTime(Timeout.MaxTestTime)]
    public void ReceiptsIterator_doesnt_throw_on_null()
    {
        _receiptsDb.GetColumnDb(ReceiptsColumns.Blocks).Set(Keccak.Zero, null!);
        _storage.TryGetReceiptsIterator(1, Keccak.Zero, out ReceiptsIterator iterator);
        iterator.TryGetNext(out _).Should().BeFalse();
    }

    [Test, MaxTime(Timeout.MaxTestTime)]
    public void Get_returns_empty_on_empty_span()
    {
        _storage.Get(Keccak.Zero).Should().BeEquivalentTo(Array.Empty<TxReceipt>());
    }

    [Test, MaxTime(Timeout.MaxTestTime)]
    public void Adds_and_retrieves_receipts_for_block()
    {
        var (block, receipts) = InsertBlock();

        _storage.ClearCache();
        _storage.Get(block).Should().BeEquivalentTo(receipts, ReceiptCompareOpt);
        // second should be from cache
        _storage.Get(block).Should().BeEquivalentTo(receipts, ReceiptCompareOpt);
    }

    [Test]
    public void Adds_should_prefix_key_with_blockNumber()
    {
        (Block block, _) = InsertBlock();

        Span<byte> blockNumPrefixed = stackalloc byte[40];
        block.Number.ToBigEndianByteArray().CopyTo(blockNumPrefixed); // TODO: We don't need to create an array here...
        block.Hash!.Bytes.CopyTo(blockNumPrefixed[8..]);

        _receiptsDb.GetColumnDb(ReceiptsColumns.Blocks)[blockNumPrefixed].Should().NotBeNull();
    }

    [Test]
    public void Adds_should_forward_write_flags()
    {
        (Block block, _) = InsertBlock(writeFlags: WriteFlags.DisableWAL);

        Span<byte> blockNumPrefixed = stackalloc byte[40];
        block.Number.ToBigEndianByteArray().CopyTo(blockNumPrefixed); // TODO: We don't need to create an array here...
        block.Hash!.Bytes.CopyTo(blockNumPrefixed[8..]);

        TestMemDb blockDb = (TestMemDb)_receiptsDb.GetColumnDb(ReceiptsColumns.Blocks);

        blockDb.KeyWasWrittenWithFlags(blockNumPrefixed.ToArray(), WriteFlags.DisableWAL);
    }

    [Test, MaxTime(Timeout.MaxTestTime)]
    public void Get_receipts_for_block_without_recovering_sender()
    {
        var (block, receipts) = InsertBlock();
        foreach (Transaction tx in block.Transactions)
        {
            tx.SenderAddress = null;
        }

        _storage.ClearCache();
        _storage.Get(block, recoverSender: false).Should().BeEquivalentTo(receipts, ReceiptCompareOpt);

        foreach (Transaction tx in block.Transactions)
        {
            tx.SenderAddress.Should().BeNull();
        }
    }

    [Test]
    public void Adds_should_attempt_hash_key_first_if_inserted_with_hashkey()
    {
        (Block block, TxReceipt[] receipts) = PrepareBlock();

        using NettyRlpStream rlpStream = _decoder.EncodeToNewNettyStream(receipts, RlpBehaviors.Storage);
        _receiptsDb.GetColumnDb(ReceiptsColumns.Blocks)[block.Hash!.Bytes] = rlpStream.AsSpan().ToArray();

        CreateStorage();
        _storage.Get(block);

        Span<byte> blockNumPrefixed = stackalloc byte[40];
        block.Number.ToBigEndianByteArray().CopyTo(blockNumPrefixed); // TODO: We don't need to create an array here...
        block.Hash!.Bytes.CopyTo(blockNumPrefixed[8..]);

        TestMemDb blocksDb = (TestMemDb)_receiptsDb.GetColumnDb(ReceiptsColumns.Blocks);
        blocksDb.KeyWasRead(blockNumPrefixed.ToArray(), times: 0);
        blocksDb.KeyWasRead(block.Hash.BytesToArray(), times: 1);
    }

    [Test]
    public void Should_be_able_to_get_block_with_hash_address()
    {
        (Block block, TxReceipt[] receipts) = PrepareBlock();

        Span<byte> blockNumPrefixed = stackalloc byte[40];
        block.Number.ToBigEndianByteArray().CopyTo(blockNumPrefixed); // TODO: We don't need to create an array here...
        block.Hash!.Bytes.CopyTo(blockNumPrefixed[8..]);

        using NettyRlpStream rlpStream = _decoder.EncodeToNewNettyStream(receipts, RlpBehaviors.Storage);
        _receiptsDb.GetColumnDb(ReceiptsColumns.Blocks)[block.Hash.Bytes] = rlpStream.AsSpan().ToArray();

        _storage.Get(block).Length.Should().Be(receipts.Length);
    }

    [Test, MaxTime(Timeout.MaxTestTime)]
    public void Should_not_cache_empty_non_processed_blocks()
    {
        Block block = Build.A.Block
            .WithTransactions(Build.A.Transaction.SignedAndResolved().TestObject)
            .WithReceiptsRoot(TestItem.KeccakA)
            .TestObject;

        TxReceipt[] emptyReceipts = [];
        _storage.Get(block).Should().BeEquivalentTo(emptyReceipts);
        // can be from cache:
        _storage.Get(block).Should().BeEquivalentTo(emptyReceipts);
        (_, TxReceipt[] receipts) = InsertBlock(block);
        // before should not be cached
        _storage.Get(block).Should().BeEquivalentTo(receipts);
    }

    [Test, MaxTime(Timeout.MaxTestTime)]
    public void Adds_and_retrieves_receipts_for_block_with_iterator_from_cache_after_insert()
    {
        var (block, receipts) = InsertBlock();

        _storage.TryGetReceiptsIterator(0, block.Hash!, out ReceiptsIterator iterator).Should().BeTrue();
        iterator.TryGetNext(out TxReceiptStructRef receiptStructRef).Should().BeTrue();
        receiptStructRef.LogsRlp.ToArray().Should().BeEmpty();
        receiptStructRef.Logs.Should().BeEquivalentTo(receipts.First().Logs);
        iterator.TryGetNext(out _).Should().BeFalse();
    }

    [Test, MaxTime(Timeout.MaxTestTime)]
    public void Adds_and_retrieves_receipts_for_block_with_iterator()
    {
        var (block, _) = InsertBlock();

        _storage.ClearCache();
        _storage.TryGetReceiptsIterator(block.Number, block.Hash!, out ReceiptsIterator iterator).Should().BeTrue();
        iterator.TryGetNext(out TxReceiptStructRef receiptStructRef).Should().BeTrue();
        receiptStructRef.LogsRlp.ToArray().Should().NotBeEmpty();
        receiptStructRef.Logs.Should().BeNullOrEmpty();

        iterator.TryGetNext(out _).Should().BeFalse();
    }

    [Test, MaxTime(Timeout.MaxTestTime)]
    public void Adds_and_retrieves_receipts_for_block_with_iterator_from_cache_after_get()
    {
        var (block, receipts) = InsertBlock();

        _storage.ClearCache();
        _storage.Get(block);
        _storage.TryGetReceiptsIterator(0, block.Hash!, out ReceiptsIterator iterator).Should().BeTrue();
        iterator.TryGetNext(out TxReceiptStructRef receiptStructRef).Should().BeTrue();
        receiptStructRef.LogsRlp.ToArray().Should().BeEmpty();
        receiptStructRef.Logs.Should().BeEquivalentTo(receipts.First().Logs);
        iterator.TryGetNext(out _).Should().BeFalse();
    }

    [Test, MaxTime(Timeout.MaxTestTime)]
    public void Should_handle_inserting_null_receipts()
    {
        Block block = Build.A.Block.WithReceiptsRoot(TestItem.KeccakA).TestObject;
        _storage.Insert(block, null);
    }

    [Test, MaxTime(Timeout.MaxTestTime)]
    public void HasBlock_should_returnFalseForMissingHash()
    {
        _storage.HasBlock(0, Keccak.Compute("missing-value")).Should().BeFalse();
    }

    [Test, MaxTime(Timeout.MaxTestTime)]
    public void HasBlock_should_returnTrueForKnownHash()
    {
        var (block, _) = InsertBlock();
        _storage.HasBlock(block.Number, block.Hash!).Should().BeTrue();
    }

    [Test, MaxTime(Timeout.MaxTestTime)]
    public void EnsureCanonical_should_change_tx_blockhash(
        [Values(false, true)] bool ensureCanonical,
        [Values(false, true)] bool isFinalized)
    {
        (Block block, TxReceipt[] receipts) = InsertBlock(isFinalized: isFinalized);
        _storage.FindBlockHash(receipts[0].TxHash!).Should().Be(block.Hash!);

        Block anotherBlock = Build.A.Block
            .WithTransactions(block.Transactions)
            .WithReceiptsRoot(TestItem.KeccakA)
            .WithExtraData(new byte[] { 1 })
            .TestObject;

        anotherBlock.Hash.Should().NotBe(block.Hash!);
        _storage.Insert(anotherBlock, new[] { Build.A.Receipt.TestObject }, ensureCanonical);
        _blockTree.FindBlockHash(anotherBlock.Number).Returns(anotherBlock.Hash);

        Hash256 findBlockHash = _storage.FindBlockHash(receipts[0].TxHash!);
        if (ensureCanonical)
        {
            findBlockHash.Should().Be(anotherBlock.Hash!);
        }
        else
        {
            findBlockHash.Should().NotBe(anotherBlock.Hash!);
        }
    }

    [Test]
    public void EnsureCanonical_should_use_blockNumber_if_finalized()
    {
        (Block block, TxReceipt[] receipts) = InsertBlock(isFinalized: true);
        Span<byte> txHashBytes = receipts[0].TxHash!.Bytes;
        if (_receiptConfig.CompactTxIndex)
        {
            _receiptsDb.GetColumnDb(ReceiptsColumns.Transactions)[txHashBytes].Should().BeEquivalentTo(Rlp.Encode(block.Number).Bytes);
        }
        else
        {
            _receiptsDb.GetColumnDb(ReceiptsColumns.Transactions)[txHashBytes].Should().NotBeNull();
        }
    }

    [Test]
    public void When_TxLookupLimitIs_NegativeOne_DoNotIndexTxHash()
    {
        _receiptConfig.TxLookupLimit = -1;
        CreateStorage();
        (Block block, TxReceipt[] receipts) = InsertBlock(isFinalized: true);
        _blockTree.BlockAddedToMain += Raise.EventWith(new BlockReplacementEventArgs(block));
        Thread.Sleep(100);
        _receiptsDb.GetColumnDb(ReceiptsColumns.Transactions)[receipts[0].TxHash!.Bytes].Should().BeNull();
    }

    [TestCase(1L, false)]
    [TestCase(10L, false)]
    [TestCase(11L, true)]
    public void Should_only_prune_index_tx_hashes_if_blockNumber_is_bigger_than_lookupLimit(long blockNumber, bool WillPruneOldIndicies)
    {
        _receiptConfig.TxLookupLimit = 10;
        CreateStorage();
        _blockTree.BlockAddedToMain +=
            Raise.EventWith(new BlockReplacementEventArgs(Build.A.Block.WithNumber(blockNumber).TestObject));
        Thread.Sleep(100);
        IEnumerable<ICall> calls = _blockTree.ReceivedCalls()
            .Where(static call => call.GetMethodInfo().Name.EndsWith(nameof(_blockTree.FindBlock)));
        if (WillPruneOldIndicies)
            calls.Should().NotBeEmpty();
        else
            calls.Should().BeEmpty();
    }

    [Test]
    public void When_HeadBlockIsFarAhead_DoNotIndexTxHash()
    {
        _receiptConfig.TxLookupLimit = 1000;
        CreateStorage();
        (Block block, TxReceipt[] receipts) = InsertBlock(isFinalized: true, headNumber: 1001);
        _blockTree.BlockAddedToMain += Raise.EventWith(new BlockReplacementEventArgs(block));
        Thread.Sleep(100);
        _receiptsDb.GetColumnDb(ReceiptsColumns.Transactions)[receipts[0].TxHash!.Bytes].Should().BeNull();
    }

    [Test]
    public void When_NewHeadBlock_DoNotRemove_TxIndex_WhenTxIsInOtherBlockNumber()
    {
        CreateStorage();

        Transaction tx = Build.A.Transaction.SignedAndResolved().TestObject;

        Block b1a = Build.A.Block.WithNumber(1).TestObject;
        Block b1b = Build.A.Block.WithNumber(1).WithTransactions(tx).TestObject;
        Block b2a = Build.A.Block.WithNumber(2).WithParent(b1a).WithTransactions(tx).TestObject;
        Block b2b = Build.A.Block.WithNumber(2).WithParent(b1b).TestObject;

        InsertBlock(b1a);
        InsertBlock(b1b);
        InsertBlock(b2a);
        InsertBlock(b2b);

        // b1a
        _blockTree.BlockAddedToMain += Raise.EventWith(new BlockReplacementEventArgs(b1a, null));

        // b1b
        _blockTree.BlockAddedToMain += Raise.EventWith(new BlockReplacementEventArgs(b1b, b1a));

        // b2a
        _blockTree.BlockAddedToMain += Raise.EventWith(new BlockReplacementEventArgs(b1a, b1b));
        _blockTree.BlockAddedToMain += Raise.EventWith(new BlockReplacementEventArgs(b2a, null));

        // b2b
        _blockTree.BlockAddedToMain += Raise.EventWith(new BlockReplacementEventArgs(b1b, b1a));
        _blockTree.BlockAddedToMain += Raise.EventWith(new BlockReplacementEventArgs(b2b, b2a));

        _storage.FindBlockHash(tx.Hash!).Should().Be(b1b.Hash!);
    }

    [Test]
    public async Task When_NewHeadBlock_Remove_TxIndex_OfRemovedBlock_Unless_ItsAlsoInNewBlock()
    {
        _receiptConfig.CompactTxIndex = _useCompactReceipts;
        CreateStorage();
        (Block block, _) = InsertBlock();
        Block block2 = Build.A.Block
            .WithParent(block)
            .WithNumber(2)
            .WithTransactions(Build.A.Transaction.SignedAndResolved(TestItem.PrivateKeyC).TestObject)
            .TestObject;
        _blockTree.FindBestSuggestedHeader().Returns(block2.Header);
        _blockTree.BlockAddedToMain += Raise.EventWith(new BlockReplacementEventArgs(block2));

        if (_receiptConfig.CompactTxIndex)
        {
            _receiptsDb.GetColumnDb(ReceiptsColumns.Transactions)[block.Transactions[0].Hash!.Bytes].Should().BeEquivalentTo(Rlp.Encode(block.Number).Bytes);
        }
        else
        {
            _receiptsDb.GetColumnDb(ReceiptsColumns.Transactions)[block.Transactions[0].Hash!.Bytes].Should().BeEquivalentTo(block.Hash!.Bytes.ToArray());
        }

        Block block3 = Build.A.Block
            .WithNumber(1)
            .WithTransactions(block2.Transactions)
            .WithExtraData(new byte[1])
            .TestObject;
        Block block4 = Build.A.Block
            .WithNumber(2)
            .WithTransactions(block.Transactions)
            .WithExtraData(new byte[1])
            .TestObject;
        _blockTree.FindBestSuggestedHeader().Returns(block4.Header);
        _blockTree.BlockAddedToMain += Raise.EventWith(new BlockReplacementEventArgs(block3, block));
        _blockTree.BlockAddedToMain += Raise.EventWith(new BlockReplacementEventArgs(block4, block2));

        await Task.Delay(100);
        if (_receiptConfig.CompactTxIndex)
        {
            _receiptsDb.GetColumnDb(ReceiptsColumns.Transactions)[block4.Transactions[0].Hash!.Bytes].Should().BeEquivalentTo(Rlp.Encode(block4.Number).Bytes);
        }
        else
        {
            _receiptsDb.GetColumnDb(ReceiptsColumns.Transactions)[block4.Transactions[0].Hash!.Bytes].Should().BeEquivalentTo(block4.Hash!.Bytes.ToArray());
        }
    }

    [Test]
    public void When_NewHeadBlock_ClearOldTxIndex()
    {
        _receiptConfig.TxLookupLimit = 1000;
        CreateStorage();
        (Block block, TxReceipt[] receipts) = InsertBlock();

        Span<byte> txHashBytes = receipts[0].TxHash!.Bytes;
        if (_receiptConfig.CompactTxIndex)
        {
            _receiptsDb.GetColumnDb(ReceiptsColumns.Transactions)[txHashBytes].Should().BeEquivalentTo(Rlp.Encode(block.Number).Bytes);
        }
        else
        {
            _receiptsDb.GetColumnDb(ReceiptsColumns.Transactions)[txHashBytes].Should().NotBeNull();
        }

        Block newHead = Build.A.Block.WithNumber(_receiptConfig.TxLookupLimit.Value + 1).TestObject;
        _blockTree.FindBestSuggestedHeader().Returns(newHead.Header);
        _blockTree.BlockAddedToMain += Raise.EventWith(new BlockReplacementEventArgs(newHead));

        Assert.That(
            () => _receiptsDb.GetColumnDb(ReceiptsColumns.Transactions)[receipts[0].TxHash!.Bytes],
            Is.Null.After(1000, 100)
            );
    }

    private (Block block, TxReceipt[] receipts) PrepareBlock(Block? block = null, bool isFinalized = false, long? headNumber = null)
    {
        block ??= Build.A.Block
            .WithNumber(1)
            .WithTransactions(Build.A.Transaction.SignedAndResolved().TestObject)
            .WithReceiptsRoot(TestItem.KeccakA)
            .TestObject;

        _blockTree.FindBlock(block.Hash!).Returns(block);
        _blockTree.FindBlock(block.Number).Returns(block);
        _blockTree.FindHeader(block.Number).Returns(block.Header);
        _blockTree.FindBlockHash(block.Number).Returns(block.Hash);
        if (isFinalized)
        {
            BlockHeader farHead = Build.A.BlockHeader
                .WithNumber(Reorganization.MaxDepth + 5)
                .TestObject;
            _blockTree.FindBestSuggestedHeader().Returns(farHead);
        }

        if (headNumber is not null)
        {
            BlockHeader farHead = Build.A.BlockHeader
                .WithNumber(headNumber.Value)
                .TestObject;
            _blockTree.FindBestSuggestedHeader().Returns(farHead);
        }

        TxReceipt[] receipts = Array.Empty<TxReceipt>();
        if (block.Transactions.Length == 1)
        {
            receipts = [Build.A.Receipt.WithCalculatedBloom().TestObject];
        }
        return (block, receipts);
    }

    private (Block block, TxReceipt[] receipts) InsertBlock(Block? block = null, bool isFinalized = false, long? headNumber = null, WriteFlags writeFlags = WriteFlags.None)
    {
        (block, TxReceipt[] receipts) = PrepareBlock(block, isFinalized, headNumber);
        _storage.Insert(block, receipts, writeFlags: writeFlags);
        _receiptsRecovery.TryRecover(new ReceiptRecoveryBlock(block), receipts);

        return (block, receipts);
    }

    private EquivalencyAssertionOptions<TxReceipt> ReceiptCompareOpt(EquivalencyAssertionOptions<TxReceipt> opts)
    {
        return opts
            .Excluding(static su => su.Error);
    }
}
