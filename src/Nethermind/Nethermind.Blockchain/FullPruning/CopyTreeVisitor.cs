// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Diagnostics;
using System.Threading;
using Nethermind.Core.Crypto;
using Nethermind.Db.FullPruning;
using Nethermind.Logging;
using Nethermind.Trie;

namespace Nethermind.Blockchain.FullPruning
{
    /// <summary>
    /// Visits the state trie and copies the nodes to pruning context.
    /// </summary>
    /// <remarks>
    /// During visiting of the state trie at specified state root it copies the existing trie into <see cref="IPruningContext"/>.
    /// </remarks>
    public class CopyTreeVisitor : ITreeVisitor, IDisposable
    {
        private readonly IPruningContext _pruningContext;
        private readonly long? _estimatedNumberOfNodes;
        private readonly ILogger _logger;
        private readonly Stopwatch _totalStopwatch;
        private readonly Stopwatch _loggingStopwatch;
        private long _persistedNodes = 0;
        private bool _finished = false;
        private readonly CancellationToken _cancellationToken;
        private const int Million = 1_000_000;

        public CopyTreeVisitor(
            IPruningContext pruningContext,
            IChainEstimations chainEstimations,
            ILogManager logManager)
        {
            _pruningContext = pruningContext;
            _estimatedNumberOfNodes = chainEstimations.NumberOfNodesInStateTree;
            _cancellationToken = pruningContext.CancellationTokenSource.Token;
            _logger = logManager.GetClassLogger();
            _totalStopwatch = new Stopwatch();
            _loggingStopwatch = new Stopwatch();
        }

        public bool IsFullDbScan => true;
        public bool ShouldVisit(Keccak nextNode) => !_cancellationToken.IsCancellationRequested;

        public void VisitTree(Keccak rootHash, TrieVisitContext trieVisitContext)
        {
            _totalStopwatch.Start();
            _loggingStopwatch.Start();
            if (_logger.IsWarn) _logger.Warn($"Full Pruning Started on root hash {rootHash}: do not close the node until finished or progress will be lost.");
        }

        public void VisitMissingNode(Keccak nodeHash, TrieVisitContext trieVisitContext)
        {
            if (_logger.IsWarn)
            {
                _logger.Warn($"Full Pruning Failed: Missing node {nodeHash} at level {trieVisitContext.Level}.");
            }

            // if nodes are missing then state trie is not valid and we need to stop copying it
            _pruningContext.CancellationTokenSource.Cancel();
        }

        public void VisitBranch(TrieNode node, TrieVisitContext trieVisitContext) => PersistNode(node);

        public void VisitExtension(TrieNode node, TrieVisitContext trieVisitContext) => PersistNode(node);

        public void VisitLeaf(TrieNode node, TrieVisitContext trieVisitContext, byte[]? value = null) => PersistNode(node);

        public void VisitCode(Keccak codeHash, TrieVisitContext trieVisitContext) { }

        private void PersistNode(TrieNode node)
        {
            if (node.Keccak is not null)
            {
                // simple copy of nodes RLP
                _pruningContext[node.Keccak.Bytes] = node.FullRlp;
                Interlocked.Increment(ref _persistedNodes);

                // log message every 1 mln nodes
                if (_persistedNodes % Million == 0)
                {
                    LogProgress();
                }
            }
        }

        private void LogProgress()
        {
            if (_estimatedNumberOfNodes is not null)
            {
                TimeSpan timeSinceLastLog = _loggingStopwatch.Elapsed;
                _loggingStopwatch.Reset();
                double nodesPerSecond = Million / timeSinceLastLog.TotalSeconds;
                long nodesToGo = long.Max(_estimatedNumberOfNodes.Value - _persistedNodes, 0);
                TimeSpan timeToGo = TimeSpan.FromSeconds(nodesToGo / nodesPerSecond);

                // Don't show more then 99% of progress as estimation might be incorrect
                double percentProgress = 100 * double.Min(_persistedNodes / (double)_estimatedNumberOfNodes.Value, 0.99);

                if (_logger.IsInfo)
                    _logger.Info($"Full Pruning ~{percentProgress:F2}: Approximately {timeToGo} to go. Elapsed {_totalStopwatch.Elapsed}.");
            }
            else
            {
                LogProgress("In Progress");
            }
        }

        private void LogProgress(string state)
        {
            if (_logger.IsInfo)
                _logger.Info($"Full Pruning {state}: {_totalStopwatch.Elapsed} {_persistedNodes / (double)Million:N} mln nodes mirrored.");
        }

        public void Dispose()
        {
            if (_logger.IsWarn && !_finished)
            {
                _logger.Warn($"Full Pruning Cancelled: Full pruning didn't finish, progress is lost.");
            }
        }

        public void Finish()
        {
            _finished = true;
            LogProgress("Finished");
        }
    }
}
