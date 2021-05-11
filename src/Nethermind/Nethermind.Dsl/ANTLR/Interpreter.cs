using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime.Tree;
using Nethermind.Api;
using Nethermind.Core;
using Nethermind.Dsl.Pipeline;
using Nethermind.Int256;
using Nethermind.Pipeline;
using Nethermind.Pipeline.Publishers;

namespace Nethermind.Dsl.ANTLR
{
    public class Interpreter
    {
        public List<IPipeline> Pipelines = new List<IPipeline>();
        private readonly INethermindApi _api;
        private readonly IParseTree _tree;
        private readonly ParseTreeListener _treeListener;
        private IPipelineBuilder<Block, Block> _blockPipelineBuilder;
        private IPipelineBuilder<Transaction, Transaction> _transactionPipelineBuilder;
        private bool _blockSource;

        public Interpreter(INethermindApi api, IParseTree tree, ParseTreeListener treeListener)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _tree = tree ?? throw new ArgumentNullException(nameof(tree));
            _treeListener = treeListener ?? throw new ArgumentNullException(nameof(treeListener));

            _treeListener.OnExit = BuildPipeline;
        }

        private void AddSource(string value)
        {
            if(value.Equals("BlockProcessor", StringComparison.InvariantCultureIgnoreCase))
            {
                var sourceElement = new BlockProcessorSource<Block>(_api.MainBlockProcessor);
                _blockPipelineBuilder = new PipelineBuilder<Block, Block>(sourceElement);

                _blockSource = true;

                return;
            }
            else if(value.Equals("TxPool", StringComparison.InvariantCultureIgnoreCase))
            {
                var sourceElement = new TxPoolSource<Transaction>(_api.TxPool);
                _transactionPipelineBuilder = new PipelineBuilder<Transaction, Transaction>(sourceElement);

                _blockSource = false;

                return;
            }
        }

        private void AddWatch(string value)
        {
            switch (value.ToLowerInvariant())
            {
                case "blocks":
                    _blockPipelineBuilder = _blockPipelineBuilder.AddElement(
                        new PipelineElement<Block, Block>(
                            condition: (block => true),
                            transformData: (b => b)
                            )
                    );
                    break;
                case "transactions":
                // with watch on transactions we need to change for transactions pipeline, hence new source
                    var sourceElement = new TxPoolSource<Transaction>(_api.TxPool);
                    _transactionPipelineBuilder = new PipelineBuilder<Transaction, Transaction>(sourceElement);

                    _blockSource = false;
                break;
            }
        }

        private void OnCondition(string key, string symbol, string value)
        {
            if(_blockSource)
            {
                var blockElement = GetNextBlockElement(key, symbol, value);
                _blockPipelineBuilder = _blockPipelineBuilder.AddElement(blockElement);
                return;
            }

            var txElement = GetNextTransactionElement(key, symbol, value);
            _transactionPipelineBuilder = _transactionPipelineBuilder.AddElement(txElement);
        }

        private void OnAndCondition(string key, string symbol, string value)
        {
            OnCondition(key, symbol, value); // AND condition is just adding another element to the pipeline
        }

        private void OnOrCondition(string key, string symbol, string value)
        {
            if(_blockSource)
            {
                var blockElement = (PipelineElement<Block, Block>)GetNextBlockElement(key, symbol, value);
                var blockCondition = blockElement.Conditions.Last();
                var lastBlockElement = (PipelineElement<Block, Block>)_blockPipelineBuilder.LastElement;
                lastBlockElement.AddCondition(blockCondition);
            }

            var txElement = (PipelineElement<Block, Block>)GetNextBlockElement(key, symbol, value);
            var txCondition = txElement.Conditions.Last();
            var lastTxElement = (PipelineElement<Block, Block>)_blockPipelineBuilder.LastElement;
            lastTxElement.AddCondition(txCondition);
        }

        private PipelineElement<Transaction, Transaction> GetNextTransactionElement(string key, string operation, string value)
        {
            return operation switch
            {
                "==" => new PipelineElement<Transaction, Transaction>(
                            condition: (t => t.GetType().GetProperty(key).GetValue(t).ToString() == value),
                            transformData: (t => t)),
                "!=" => new PipelineElement<Transaction, Transaction>(
                            condition: (t => t.GetType().GetProperty(key).GetValue(t).ToString() != value),
                            transformData: (t => t)),
                ">" => new PipelineElement<Transaction, Transaction>(
                            condition: (t => (UInt256)t.GetType().GetProperty(key).GetValue(t) > UInt256.Parse(value)),
                            transformData: (t => t)),
                "<" => new PipelineElement<Transaction, Transaction>(
                            condition: (t => (UInt256)t.GetType().GetProperty(key).GetValue(t) < UInt256.Parse(value)),
                            transformData: (t => t)),
                ">=" => new PipelineElement<Transaction, Transaction>(
                            condition: (t => (UInt256)t.GetType().GetProperty(key).GetValue(t) >= UInt256.Parse(value)),
                            transformData: (t => t)),
                "<=" => new PipelineElement<Transaction, Transaction>(
                            condition: (t => (UInt256)t.GetType().GetProperty(key).GetValue(t) <= UInt256.Parse(value)),
                            transformData: (t => t)),
                _ => null
            };
        }

        private PipelineElement<Block, Block> GetNextBlockElement(string key, string operation, string value)
        {
            return operation switch
            {
                "==" => new PipelineElement<Block, Block>(
                            condition: (b => b.GetType().GetProperty(key).GetValue(b).ToString() == value),
                            transformData: (b => b)),
                "!=" => new PipelineElement<Block, Block>(
                            condition: (b => b.GetType().GetProperty(key).GetValue(b).ToString() != value),
                            transformData: (b => b)),
                ">" => new PipelineElement<Block, Block>(
                            condition: (b => (UInt256)b.GetType().GetProperty(key).GetValue(b) > UInt256.Parse(value)),
                            transformData: (b => b)),
                "<" => new PipelineElement<Block, Block>(
                            condition: (b => (UInt256)b.GetType().GetProperty(key).GetValue(b) < UInt256.Parse(value)),
                            transformData: (b => b)),
                ">=" => new PipelineElement<Block, Block>(
                            condition: (b => (UInt256)b.GetType().GetProperty(key).GetValue(b) >= UInt256.Parse(value)),
                            transformData: (b => b)),
                "<=" => new PipelineElement<Block, Block>(
                            condition: (b => (UInt256)b.GetType().GetProperty(key).GetValue(b) <= UInt256.Parse(value)),
                            transformData: (b => b)),
                _ => null
            };
        }

        private void OnPublish(string publisher)
        {
            if(_blockSource)
            {
                AddBlockPublisher(publisher);
                return;
            }

            AddTransactionPublisher(publisher);
        }

        private void AddBlockPublisher(string publisher)
        {
            if (publisher.Equals("WebSockets", StringComparison.InvariantCultureIgnoreCase))
            {
                if (_blockPipelineBuilder != null)
                {
                    _blockPipelineBuilder =_blockPipelineBuilder.AddElement(new WebSocketsPublisher<Block, Block>("dsl", _api.EthereumJsonSerializer));
                }
            }
        }

        private void AddTransactionPublisher(string publisher)
        {
            if (publisher.Equals("WebSockets", StringComparison.InvariantCultureIgnoreCase))
            {
                if (_blockPipelineBuilder != null)
                {
                    _transactionPipelineBuilder = _transactionPipelineBuilder.AddElement(new WebSocketsPublisher<Transaction, Transaction>("dsl", _api.EthereumJsonSerializer));
                }
            }
        }

        private void BuildPipeline()
        {
            if(_blockSource)
            {
                Pipelines.Add(_blockPipelineBuilder.Build());
            }
            else
            {
                Pipelines.Add(_transactionPipelineBuilder.Build());
            }
        }
    }
}