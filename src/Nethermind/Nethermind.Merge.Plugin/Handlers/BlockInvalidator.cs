// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Linq;
using DotNetty.Common.Utilities;
using MathNet.Numerics.Random;
using Nethermind.Core;
using Nethermind.Crypto;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.State;

namespace Nethermind.Merge.Plugin.Handlers;

public class BlockInvalidator
{
    private readonly ILogger _logger;
    private readonly IStateProvider _stateProvider;

    public BlockInvalidator(IStateProvider stateProvider, ILogManager logManager)
    {
        ArgumentNullException.ThrowIfNull(nameof(logManager));

        _stateProvider = stateProvider ?? throw new ArgumentNullException(nameof(stateProvider));
        _logger = logManager.GetClassLogger();
    }

    public void Invalidate(ref Block? block)
    {
        if (block is null || !Random.Shared.NextBoolean())
            return;

        block.Header.GasUsed = Random.Shared.NextLong();
        block.Header.Author = Random.Shared.NextBoolean() ? block.Header.Beneficiary : block.Header.Author;
        block.Header.Beneficiary = Random.Shared.NextBoolean() ? block.Header.Author : block.Header.Beneficiary;
        block.Header.TotalDifficulty = UInt256.Parse(
            Enumerable.Range(0, 77)
                .Select(_ => Random.Shared.Next(0, 10))
                .Aggregate(string.Empty, (acc, val) => $"{acc}{val}")
            );
        _stateProvider.RecalculateStateRoot();
        block.Header.StateRoot = _stateProvider.StateRoot;
        block.Header.Hash = block.Header.CalculateHash();

        _logger.Warn($"Block {block.Number} has been intentionally invalidated!");
    }
}
