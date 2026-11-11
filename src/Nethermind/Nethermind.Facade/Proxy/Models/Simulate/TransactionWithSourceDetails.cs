// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;

namespace Nethermind.Facade.Proxy.Models.Simulate;

public class TransactionWithSourceDetails
{
    public bool HadGasLimitInRequest;
    public bool HadNonceInRequest;

    /// <summary>The gas limit the block must still be able to include, or <c>null</c> to skip that check.</summary>
    public ulong? GasLimitToFitBlock;

    /// <summary>
    /// Whether the call draws on the request budget raised to the gas limit of the block the simulation builds on,
    /// rather than on the RPC gas cap. Set for a gas the call sets; a call that omits it keeps the cap as its budget.
    /// </summary>
    public bool MayExceedGasCap;
    public Transaction Transaction { get; set; }
}
