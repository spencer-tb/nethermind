// SPDX-FileCopyrightText: 2026 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;

namespace Nethermind.Core;

/// <summary>
/// Helpers for JSON-RPC <c>GasCap</c>: <see langword="null"/> or <c>0</c> means no cap.
/// </summary>
public static class GasCapExtensions
{
    /// <summary>Whether <paramref name="gasCap"/> applies a finite RPC gas limit.</summary>
    public static bool IsGasCapped(this ulong? gasCap) => gasCap is not null and not 0;

    /// <summary>Returns <paramref name="gasCap"/> when capped, otherwise <see cref="ulong.MaxValue"/>.</summary>
    public static ulong EffectiveGasCap(this ulong? gasCap) => gasCap.IsGasCapped() ? gasCap!.Value : ulong.MaxValue;

    /// <summary>Returns <paramref name="gasCap"/> raised to <paramref name="blockGasLimit"/> when capped.</summary>
    /// <remarks>
    /// A transaction that fits in a block can be included in it, so a call that sets its gas is never capped below
    /// the gas limit of the block it runs on. That must be the block's own gas limit, not one a block override sets,
    /// so that a request cannot lift the cap.
    /// </remarks>
    public static ulong? RaisedToBlockGasLimit(this ulong? gasCap, ulong blockGasLimit) =>
        gasCap.IsGasCapped() ? Math.Max(gasCap!.Value, blockGasLimit) : gasCap;
}
