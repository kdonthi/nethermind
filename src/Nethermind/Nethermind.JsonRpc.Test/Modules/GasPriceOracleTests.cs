#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Nethermind.Blockchain.Find;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Core.Test.Builders;
using Nethermind.Int256;
using Nethermind.JsonRpc.Modules.Eth;
using NSubstitute;
using NUnit.Framework;

namespace Nethermind.JsonRpc.Test.Modules
{
    [TestFixture]
    class GasPriceOracleTests
    {
        [Test]
        public void GasPriceEstimate_NoChangeInHeadBlock_ReturnsPreviousGasPrice()
        {
            ShouldReturnSameGasPriceGasPriceOracle testableGasPriceOracle = GetShouldReturnSameGasPriceGasPriceOracle(lastGasPrice: 7);
            IBlockFinder blockFinder = Substitute.For<IBlockFinder>();
            Block testBlock = Build.A.Block.Genesis.TestObject;
            
            ResultWrapper<UInt256?> resultWrapper = testableGasPriceOracle.GasPriceEstimate(testBlock, blockFinder);
            
            resultWrapper.Data.Should().Be((UInt256?) 7);
        }

        class ShouldReturnSameGasPriceGasPriceOracle : TestableGasPriceOracle
        {
            public ShouldReturnSameGasPriceGasPriceOracle(
                ISpecProvider? specProvider = null,
                UInt256? ignoreUnder = null, 
                int? blockLimit = null, 
                ITxInsertionManager? txInsertionManager = null,
                UInt256? lastGasPrice = null):
                base(
                    specProvider ?? Substitute.For<ISpecProvider>(),
                    ignoreUnder,
                    blockLimit,
                    txInsertionManager,
                    lastGasPrice)
            {
            }

            public override bool ShouldReturnSameGasPrice(Block? lastHead, Block? currentHead, UInt256? lastGasPrice)
            {
                return true;
            }
        }
        
        private ShouldReturnSameGasPriceGasPriceOracle GetShouldReturnSameGasPriceGasPriceOracle(
            ISpecProvider? specProvider = null, 
            UInt256? ignoreUnder = null, 
            int? blockLimit = null, 
            ITxInsertionManager? txInsertionManager = null,
            UInt256? lastGasPrice = null)
        {
            return new(
                specProvider ?? Substitute.For<ISpecProvider>(),
                ignoreUnder,
                blockLimit,
                txInsertionManager ?? Substitute.For<ITxInsertionManager>(),
                lastGasPrice);
        }
        
        [Test]
        public void GasPriceEstimate_IfPreviousGasPriceDoesNotExist_FallbackGasPriceSetToDefaultGasPrice()
        {
            TestableGasPriceOracle testableGasPriceOracle = GetTestableGasPriceOracle();
            IBlockFinder blockFinder = Substitute.For<IBlockFinder>();
            Block testBlock = Build.A.Block.Genesis.TestObject;
            
            testableGasPriceOracle.GasPriceEstimate(testBlock, blockFinder);
            
            testableGasPriceOracle.FallbackGasPrice.Should().BeEquivalentTo((UInt256?) EthGasPriceConstants.DefaultGasPrice);
        }

        [TestCase(3)]
        [TestCase(10)]
        public void GasPriceEstimate_IfPreviousGasPriceExists_FallbackGasPriceIsSetToPreviousGasPrice(int lastGasPrice)
        {
            TestableGasPriceOracle testableGasPriceOracle = GetTestableGasPriceOracle(lastGasPrice: (UInt256) lastGasPrice);
            IBlockFinder blockFinder = Substitute.For<IBlockFinder>();
            Block testBlock = Build.A.Block.Genesis.TestObject;
            
            testableGasPriceOracle.GasPriceEstimate(testBlock, blockFinder);
            
            testableGasPriceOracle.FallbackGasPrice.Should().BeEquivalentTo((UInt256?) lastGasPrice);
        }

        [TestCase(new[]{1,3,5,7,8,9}, 7)] //Last index: 6 - 1 = 5, 60th percentile: 5 * 3/5 = 3, Value: 7
        [TestCase(new[]{0,0,7,9,10,27,83,101}, 10)] //Last index: 8 - 1 = 7, 60th percentile: 7 * 3/5 rounds to 4, Value: 10
        public void GasPriceEstimate_BlockcountEqualToBlocksToCheck_SixtiethPercentileOfMaxIndexReturned(int[] gasPrice, int expected)
        {
            List<UInt256> listOfGasPrices = gasPrice.Select(n => (UInt256) n).ToList();
            TestableGasPriceOracle testableGasPriceOracle = GetTestableGasPriceOracle(sortedTxList: listOfGasPrices);
            IBlockFinder blockFinder = Substitute.For<IBlockFinder>();
            Block testBlock = Build.A.Block.Genesis.TestObject;

            ResultWrapper<UInt256?> resultWrapper = testableGasPriceOracle.GasPriceEstimate(testBlock, blockFinder);
            
            resultWrapper.Data.Should().BeEquivalentTo((UInt256?) expected);
        }

        [Test]
        public void GasPriceEstimate_IfCalculatedGasPriceGreaterThanMax_MaxGasPriceReturned()
        {
            
            List<UInt256> listOfGasPrices = new List<UInt256>
            {
                501
            }; 
            TestableGasPriceOracle testableGasPriceOracle = GetTestableGasPriceOracle(sortedTxList: listOfGasPrices);
            IBlockFinder blockFinder = Substitute.For<IBlockFinder>();
            Block testBlock = Build.A.Block.Genesis.TestObject;

            ResultWrapper<UInt256?> resultWrapper = testableGasPriceOracle.GasPriceEstimate(testBlock, blockFinder);
            
            resultWrapper.Result.Should().Be(Result.Success);
            resultWrapper.Data.Should().BeEquivalentTo((UInt256?) EthGasPriceConstants._maxGasPrice);
        }
        
        [Test]
        public void GasPriceEstimate_IfEightBlocksWithTwoTransactions_CheckEightBlocks()
        {
            ITxInsertionManager txInsertionManager = Substitute.For<ITxInsertionManager>();
            txInsertionManager.AddValidTxFromBlockAndReturnCount(Arg.Any<Block>()).Returns(2);
            TestableGasPriceOracle testableGasPriceOracle = GetTestableGasPriceOracle(txInsertionManager: txInsertionManager, blockLimit: 8);
            Block headBlock = Build.A.Block.WithNumber(8).TestObject;
            IBlockFinder blockFinder = Substitute.For<IBlockFinder>();
            
            testableGasPriceOracle.GasPriceEstimate(headBlock, blockFinder);
            
            txInsertionManager.Received(8).AddValidTxFromBlockAndReturnCount(Arg.Any<Block>());
        }

        [Test]
        public void GasPriceEstimate_IfLastFiveBlocksWithThreeTxAndFirstFourWithOne_CheckSixBlocks()
        {
            ITxInsertionManager txInsertionManager = Substitute.For<ITxInsertionManager>();
            TestableGasPriceOracle testableGasPriceOracle = GetTestableGasPriceOracle(txInsertionManager: txInsertionManager, blockLimit: 8);
            SetUpTxInsertionManagerForSpecificReturns(txInsertionManager, testableGasPriceOracle);
            Block headBlock = Build.A.Block.WithNumber(8).TestObject;
            IBlockFinder blockFinder = BlockFinderForNineEmptyBlocks();
            
            testableGasPriceOracle.GasPriceEstimate(headBlock, blockFinder);
            
            txInsertionManager.Received(8).AddValidTxFromBlockAndReturnCount(Arg.Any<Block>());

            static IBlockFinder BlockFinderForNineEmptyBlocks()
            {
                IBlockFinder blockFinder = Substitute.For<IBlockFinder>();
                Block[] blocks = {
                    Build.A.Block.Genesis.TestObject,
                    Build.A.Block.WithNumber(1).TestObject,
                    Build.A.Block.WithNumber(2).TestObject,
                    Build.A.Block.WithNumber(3).TestObject,
                    Build.A.Block.WithNumber(4).TestObject,
                    Build.A.Block.WithNumber(5).TestObject,
                    Build.A.Block.WithNumber(6).TestObject,
                    Build.A.Block.WithNumber(7).TestObject,
                    Build.A.Block.WithNumber(8).TestObject,
                };
            
                blockFinder.FindBlock(0).Returns(blocks[0]);
                blockFinder.FindBlock(1).Returns(blocks[1]);
                blockFinder.FindBlock(2).Returns(blocks[2]);
                blockFinder.FindBlock(3).Returns(blocks[3]);
                blockFinder.FindBlock(4).Returns(blocks[4]);
                blockFinder.FindBlock(5).Returns(blocks[5]);
                blockFinder.FindBlock(6).Returns(blocks[6]);
                blockFinder.FindBlock(7).Returns(blocks[7]);
                blockFinder.FindBlock(8).Returns(blocks[8]);
            
                return blockFinder;
            }
        }

        private static void SetUpTxInsertionManagerForSpecificReturns(ITxInsertionManager txInsertionManager,
            TestableGasPriceOracle testableGasPriceOracle)
        {
            txInsertionManager.AddValidTxFromBlockAndReturnCount(Arg.Is<Block>(b => b.Number >= 4)).Returns(3);
            txInsertionManager
                .When(t => t.AddValidTxFromBlockAndReturnCount(Arg.Is<Block>(b => b.Number >= 4)))
                .Do(t => testableGasPriceOracle.AddToSortedTxList(1,2,3));
            
            txInsertionManager.AddValidTxFromBlockAndReturnCount(Arg.Is<Block>(b => b.Number < 4)).Returns(1);
            txInsertionManager
                .When(t => t.AddValidTxFromBlockAndReturnCount(Arg.Is<Block>(b => b.Number < 4)))
                .Do(t => testableGasPriceOracle.AddToSortedTxList(4));
        }
        
        [Test]
        public void ShouldReturnSameGasPrice_IfLastHeadAndCurrentHeadAreSame_WillReturnTrue()
        {
            Block testBlock = Build.A.Block.Genesis.TestObject;
            TestableGasPriceOracle testableGasPriceOracle = GetTestableGasPriceOracle();
            bool result = testableGasPriceOracle.ShouldReturnSameGasPrice(testBlock, testBlock, 10);

            result.Should().BeTrue();
        }

        [Test]
        public void ShouldReturnSameGasPrice_IfLastHeadAndCurrentHeadAreNotSame_WillReturnFalse()
        {
            Block testBlock = Build.A.Block.Genesis.TestObject;
            Block differentTestBlock = Build.A.Block.WithNumber(1).TestObject;
            TestableGasPriceOracle testableGasPriceOracle = GetTestableGasPriceOracle();
            
            bool result = testableGasPriceOracle.ShouldReturnSameGasPrice(testBlock, differentTestBlock, 10);

            result.Should().BeFalse();
        }

        [Test]
        public void ShouldReturnSameGasPrice_IfLastHeadIsNull_WillReturnFalse()
        {
            Block testBlock = Build.A.Block.Genesis.TestObject;
            TestableGasPriceOracle testableGasPriceOracle = GetTestableGasPriceOracle();
            
            bool result = testableGasPriceOracle.ShouldReturnSameGasPrice(null, testBlock, 10);

            result.Should().BeFalse();
        }
        
        [Test]
        public void ShouldReturnSameGasPrice_IfLastGasPriceIsNull_WillReturnFalse()
        {
            Block testBlock = Build.A.Block.Genesis.TestObject;
            TestableGasPriceOracle testableGasPriceOracle = GetTestableGasPriceOracle();
            
            bool result = testableGasPriceOracle.ShouldReturnSameGasPrice(testBlock, testBlock, null);

            result.Should().BeFalse();
        }

        private class TestableGasPriceOracle : GasPriceOracle
        {
            private readonly UInt256? _lastGasPrice;
            private readonly List<UInt256>? _sortedTxList;
            public TestableGasPriceOracle(
                ISpecProvider? specProvider = null,
                UInt256? ignoreUnder = null, 
                int? blockLimit = null, 
                ITxInsertionManager? txInsertionManager = null,
                UInt256? lastGasPrice = null,
                List<UInt256>? sortedTxList = null) : 
                base(
                    specProvider ?? Substitute.For<ISpecProvider>(),
                    ignoreUnder,
                    blockLimit,
                    txInsertionManager)
            {
                _lastGasPrice = lastGasPrice;
                _sortedTxList = sortedTxList;
            }

            protected override UInt256? GetLastGasPrice()
            {
                return _lastGasPrice ?? base.LastGasPrice;
            }

            protected override List<UInt256> GetSortedTxGasPriceList(Block? headBlock, IBlockFinder blockFinder)
            {
                return _sortedTxList ?? base.GetSortedTxGasPriceList(headBlock, blockFinder);
            }
            
            public void AddToSortedTxList(params UInt256[] numbers)
            {
                TxGasPriceList.AddRange(numbers.ToList());
            }
        }
        
        private TestableGasPriceOracle GetTestableGasPriceOracle(
            ISpecProvider? specProvider = null, 
            UInt256? ignoreUnder = null, 
            int? blockLimit = null, 
            ITxInsertionManager? txInsertionManager = null,
            UInt256? lastGasPrice = null,
            List<UInt256>? sortedTxList = null)
        {
            return new(
                specProvider ?? Substitute.For<ISpecProvider>(),
                ignoreUnder,
                blockLimit,
                txInsertionManager ?? Substitute.For<ITxInsertionManager>(),
                lastGasPrice,
                sortedTxList);
        }
    }
}
