//  Copyright (c) 2021 Demerzel Solutions Limited
//  This file is part of the Nethermind library.
// 
//  The Nethermind library is free software: you can redistribute it and/or modify
//  it under the terms of the GNU Lesser General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
// 
//  The Nethermind library is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//  GNU Lesser General Public License for more details.
// 
//  You should have received a copy of the GNU Lesser General Public License
//  along with the Nethermind. If not, see <http://www.gnu.org/licenses/>.
// 

using System;
using System.Linq;
using FluentAssertions;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Find;
using Nethermind.Blockchain.Receipts;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Core.Test.Builders;
using Nethermind.Facade;
using Nethermind.Int256;
using Nethermind.JsonRpc.Modules.Eth;
using Nethermind.JsonRpc.Modules.Eth.FeeHistory;
using Nethermind.Logging;
using NSubstitute;
using NUnit.Framework;
using static Nethermind.JsonRpc.Test.Modules.GasPriceOracleTests;

namespace Nethermind.JsonRpc.Test.Modules
{
    public class FeeHistoryOracleTests
    {
        //Todo A test to check if blocksToCheck is greater than 1024 blocks
        //Todo A test to check if EffectiveGasTip is calculated correctly
        //Todo A test to check if PendingBlock's BlockNumber is less than blockNumber
        
        [Test]
        public void GetFeeHistory_NewestBlockIsNull_ReturnsFailingWrapper()
        {
            IBlockFinder blockFinder = Substitute.For<IBlockFinder>();
            blockFinder.FindBlock(Arg.Any<long>()).Returns((Block?) null);
            FeeHistoryOracle feeHistoryOracle = GetSubstitutedFeeHistoryOracle(blockFinder: blockFinder);
            ResultWrapper<FeeHistoryResults> expected = 
                    ResultWrapper<FeeHistoryResults>.Fail("newestBlock: Block is not available", 
                        ErrorCodes.ResourceUnavailable);

            ResultWrapper<FeeHistoryResults> resultWrapper = feeHistoryOracle.GetFeeHistory(1, 
                new BlockParameter((long) 0), null);
            
            resultWrapper.Should().BeEquivalentTo(expected);
        }
        
        
        [TestCase(3,5)]
        [TestCase(4,10)]
        [TestCase(0,1)]
        public void GetFeeHistory_IfPendingBlockDoesNotExistAndLastBlockNumberGreaterThanHeadNumber_ReturnsError(long pendingBlockNumber, long lastBlockNumber)
        {
            IBlockFinder blockFinder = Substitute.For<IBlockFinder>();
            blockFinder.FindPendingBlock().Returns(Build.A.Block.WithNumber(pendingBlockNumber).TestObject);
            FeeHistoryOracle feeHistoryOracle = GetSubstitutedFeeHistoryOracle(blockFinder: blockFinder);
            ResultWrapper<FeeHistoryResults> expected = 
                    ResultWrapper<FeeHistoryResults>.Fail("newestBlock: Block is not available", 
                        ErrorCodes.ResourceUnavailable);
            
            ResultWrapper<FeeHistoryResults> resultWrapper =
                feeHistoryOracle.GetFeeHistory(1, new BlockParameter(lastBlockNumber), null);
    
            resultWrapper.Should().BeEquivalentTo(expected);
        }
        [Test]
        public void GetFeeHistory_BlockCountIsLessThanOne_ReturnsFailingWrapper()
        {
            IBlockFinder blockFinder = Substitute.For<IBlockFinder>();
            blockFinder.FindBlock(Arg.Any<long>()).Returns(Build.A.Block.TestObject);
            FeeHistoryOracle feeHistoryOracle = GetSubstitutedFeeHistoryOracle(blockFinder: blockFinder);
            ResultWrapper<FeeHistoryResults> expected = 
                    ResultWrapper<FeeHistoryResults>.Fail("newestBlock: Block is not available", 
                        ErrorCodes.ResourceUnavailable);

            ResultWrapper<FeeHistoryResults> resultWrapper = feeHistoryOracle.GetFeeHistory(0, 
                BlockParameter.Latest, null);
            
            resultWrapper.Should().BeEquivalentTo(expected);
        }
        
        [Test]
        public void GetFeeHistory_IfRewardPercentilesNotInAscendingOrder_ResultsInFailure()
        {
            int blockCount = 10;
            double[] rewardPercentiles = {0, 2, 3, 5, 1};
            IBlockFinder blockFinder = Substitute.For<IBlockFinder>();
            blockFinder.FindBlock(BlockParameter.Latest).Returns(Build.A.Block.TestObject);
            FeeHistoryOracle feeHistoryOracle = GetSubstitutedFeeHistoryOracle(blockFinder: blockFinder);
            
            ResultWrapper<FeeHistoryResults> resultWrapper =
                feeHistoryOracle.GetFeeHistory(blockCount, BlockParameter.Latest, rewardPercentiles);

            resultWrapper.Result.Error.Should().Be("rewardPercentiles: Value at index 4: 1 is less than or equal to the value at previous index 3: 5.");
            resultWrapper.Result.ResultType.Should().Be(ResultType.Failure);
        }
        
        [TestCase(new double[] {-1, 1, 2})]
        [TestCase(new[] {1, 2.2, 101, 102})]
        public void GetFeeHistory_IfRewardPercentilesContainInvalidNumber_ResultsInFailure(double[] rewardPercentiles)
        {
            int blockCount = 10;
            IBlockFinder blockFinder = Substitute.For<IBlockFinder>();
            blockFinder.FindBlock(BlockParameter.Latest).Returns(Build.A.Block.TestObject);
            FeeHistoryOracle feeHistoryOracle = GetSubstitutedFeeHistoryOracle(blockFinder: blockFinder);
            
            ResultWrapper<FeeHistoryResults> resultWrapper = feeHistoryOracle.GetFeeHistory(blockCount, BlockParameter.Latest, rewardPercentiles);

            resultWrapper.Result.Error.Should().Be("rewardPercentiles: Some values are below 0 or greater than 100.");
            resultWrapper.Result.ResultType.Should().Be(ResultType.Failure);
        }
        
        [TestCase(new double[] {1, 2, 3})]
        [TestCase(new[] {1, 1.5, 2, 66, 67.5, 100})]
        public void GetFeeHistory_IfNewestBlockAndBlockCountAndRewardPercentilesAreValid_ResultIsSuccessful(double[] rewardPercentiles)
        {
            int blockCount = 10;
            IBlockFinder blockFinder = Substitute.For<IBlockFinder>();
            Block headBlock = Build.A.Block.TestObject; 
            blockFinder.FindBlock(BlockParameter.Latest).Returns(headBlock);
            IReceiptFinder receiptFinder = Substitute.For<IReceiptFinder>();
            receiptFinder.Get(headBlock).Returns(new TxReceipt[] { });
            FeeHistoryOracle feeHistoryOracle = GetSubstitutedFeeHistoryOracle(blockFinder: blockFinder);
            
            ResultWrapper<FeeHistoryResults> resultWrapper = feeHistoryOracle.GetFeeHistory(blockCount, BlockParameter.Latest, rewardPercentiles);

            resultWrapper.Result.Should().BeEquivalentTo(ResultWrapper<FeeHistoryResults>.Success(null));
        }

        [TestCase(5,  6)] //Target gas used: 3/2 = 1.5 | Actual Gas used = 3 | Base Fee Delta = Max((((3-1.5) * 5)/1 / 8, 1) = 1 | Next Base Fee = 5 + 1 = 6 
        [TestCase(11, 12)] //Target gas used: 3/2 = 1.5 | Actual Gas used = 3 | Base Fee Delta = Max((((3-1.5)/1) * 11) / 8, 1) = 1 | Next Base Fee = 11 + 1 = 12 
        [TestCase(20, 22)] //Target gas used: 100/2 = 50 | Actual Gas used = 95 | Base Fee Delta = Max((((95-50)/50) * 20) / 8, 1) = 2 | Next Base Fee = 20 + 2 = 22
        [TestCase(20, 20)] //Target gas used: 100/2 = 50 | Actual Gas used = 40 | Base Fee Delta = (((50-40)/50) * 20) / 8 = 0 | Next Base Fee = 20 - 0 = 20 
        [TestCase(50,  49)] //Target gas used: 100/2 = 50 | Actual Gas used = 40 | Base Fee Delta = (((50-40)/50) * 50) / 8 = 1 | Next Base Fee = 50 - 1 = 49
        public void GetFeeHistory_IfLondonEnabled_NextBaseFeePerGasCalculatedCorrectly(long baseFee, long expectedNextBaseFee)
        {
            int blockCount = 1;
            IBlockFinder blockFinder = Substitute.For<IBlockFinder>();
            BlockHeader blockHeader = Build.A.BlockHeader.WithBaseFee((UInt256) baseFee).TestObject;
            BlockParameter newestBlock = new((long) 0); 
            Block headBlock = Build.A.Block.Genesis.WithHeader(blockHeader).TestObject;
            blockFinder.FindBlock(newestBlock).Returns(headBlock);
            ISpecProvider specProvider = GetSpecProviderWithEip1559EnabledAs(true);
            
            FeeHistoryOracle feeHistoryOracle = GetSubstitutedFeeHistoryOracle(blockFinder: blockFinder, specProvider: specProvider);

            ResultWrapper<FeeHistoryResults> resultWrapper = feeHistoryOracle.GetFeeHistory(blockCount, newestBlock, null);

            resultWrapper.Data.BaseFeePerGas![1].Should().Be((UInt256) expectedNextBaseFee);
            resultWrapper.Data.BaseFeePerGas![0].Should().Be((UInt256) baseFee);
        }

        [TestCase(3, 3, 1)] 
        [TestCase(100, 95,0.95)]  
        [TestCase(12, 3, 0.25)] 
        [TestCase(100, 40,  0.4)]  
        [TestCase(3, 1, 0.33)] 
        public void GetFeeHistory_GasUsedRatioCalculatedCorrectly(long gasLimit, long gasUsed, double expectedGasUsedRatio)
        {
            IBlockFinder blockFinder = Substitute.For<IBlockFinder>();
            BlockHeader blockHeader = Build.A.BlockHeader.WithGasLimit(gasLimit).WithGasUsed(gasUsed).TestObject;
            Block headBlock = Build.A.Block.Genesis.WithHeader(blockHeader).TestObject;
            BlockParameter newestBlock = new((long) 0); 
            blockFinder.FindBlock(newestBlock).Returns(headBlock);
            ISpecProvider specProvider = GetSpecProviderWithEip1559EnabledAs(true);
            FeeHistoryOracle feeHistoryOracle = GetSubstitutedFeeHistoryOracle(blockFinder: blockFinder, specProvider: specProvider);

            ResultWrapper<FeeHistoryResults> resultWrapper = feeHistoryOracle.GetFeeHistory(1, newestBlock, null);

            resultWrapper.Data.GasUsedRatio![0].Should().Be(expectedGasUsedRatio);
        }
        
        [TestCase(3,3)]
        [TestCase(5,5)]
        public void GetFeeHistory_IfLondonNotEnabled_NextBaseFeeIsParentBaseFee(long baseFee, long expectedNextBaseFee)
        {
            ISpecProvider specProvider = GetSpecProviderWithEip1559EnabledAs(false);
            BlockHeader blockHeader = Build.A.BlockHeader.WithBaseFee((UInt256) baseFee).TestObject;
            Block headBlock = Build.A.Block.Genesis.WithHeader(blockHeader).TestObject;
            BlockParameter newestBlock = new((long) 0);
            IBlockFinder blockFinder = Substitute.For<IBlockFinder>();
            blockFinder.FindBlock(newestBlock).Returns(headBlock);
            FeeHistoryOracle feeHistoryOracle = GetSubstitutedFeeHistoryOracle(blockFinder: blockFinder, specProvider: specProvider);
            
            ResultWrapper<FeeHistoryResults> resultWrapper = feeHistoryOracle.GetFeeHistory(1, newestBlock, null);
            
            resultWrapper.Data.BaseFeePerGas![1].Should().Be((UInt256) expectedNextBaseFee);
            resultWrapper.Data.BaseFeePerGas![0].Should().Be((UInt256) baseFee);
        }
        
        [TestCase(null)]
        [TestCase(new double[]{})]
        public void GetFeeHistory_IfRewardPercentilesIsNullOrEmpty_RewardsIsNull(double[]? rewardPercentiles)
        {
            IBlockFinder blockFinder = Substitute.For<IBlockFinder>();
            blockFinder.FindBlock(BlockParameter.Latest).Returns(Build.A.Block.TestObject);
            FeeHistoryOracle feeHistoryOracle = GetSubstitutedFeeHistoryOracle(blockFinder: blockFinder);

            ResultWrapper<FeeHistoryResults> resultWrapper = feeHistoryOracle.GetFeeHistory(1, BlockParameter.Latest, rewardPercentiles);

            resultWrapper.Data.Reward.Should().BeNull();
        }
        
        [TestCase(5)]
        [TestCase(7)]
        public void GetFeeHistory_NoTxsInBlock_ReturnsArrayOfZerosAsBigAsRewardPercentiles(int sizeOfRewardPercentiles)
        {
            IBlockFinder blockFinder = Substitute.For<IBlockFinder>();
            Block noTxBlock = Build.A.Block.TestObject;
            BlockParameter newestBlock = new BlockParameter((long) 0);
            blockFinder.FindBlock(newestBlock).Returns(noTxBlock);
            FeeHistoryOracle feeHistoryOracle = GetSubstitutedFeeHistoryOracle(blockFinder: blockFinder);
            double[] rewardPercentiles = Enumerable.Range(1, sizeOfRewardPercentiles).Select(x => (double) x).ToArray();

            ResultWrapper<FeeHistoryResults> resultWrapper = feeHistoryOracle.GetFeeHistory(1, newestBlock, rewardPercentiles);
            
            resultWrapper.Data.Reward.Should().BeEquivalentTo(Enumerable.Repeat(0, sizeOfRewardPercentiles));
        }
        
        
        [TestCase(5,10,6)]
        [TestCase(5, 3, 0)]
        public void GetFeeHistory_GivenValidInputs_FirstBlockNumberCalculatedCorrectly(int blockCount, long newestBlockNumber, long expectedOldestBlockNumber)
        {
            BlockParameter lastBlockNumber = new(newestBlockNumber);
            IBlockFinder blockFinder = Substitute.For<IBlockFinder>();
            Block headBlock = Build.A.Block.WithNumber(10).TestObject;
            Block blockToSetParentOf = headBlock;

            blockFinder.FindBlock(lastBlockNumber).Returns(headBlock);
            Block parentBlock;
            for (int i = 1; i < blockCount && newestBlockNumber - i >= 0; i++)
            {
                parentBlock = Build.A.Block.WithNumber(newestBlockNumber - i).TestObject;
                blockFinder.FindParent(blockToSetParentOf, BlockTreeLookupOptions.RequireCanonical).Returns(parentBlock);
                blockToSetParentOf = parentBlock;
            }
            FeeHistoryOracle feeHistoryOracle = GetSubstitutedFeeHistoryOracle(blockFinder: blockFinder);
            
            ResultWrapper<FeeHistoryResults> resultWrapper = feeHistoryOracle.GetFeeHistory(blockCount, lastBlockNumber, null);

            resultWrapper.Data.OldestBlock.Should().Be(expectedOldestBlockNumber);
        }

        [TestCase(2,2)]
        [TestCase(7,7)]
        [TestCase(32,32)]
        public void GetFeeHistory_IfLastBlockIsPendingBlock_LastBlockNumberSetToPendingBlockNumber(long blockNumber, long lastBlockNumberExpected)
        {
            IBlockFinder blockFinder = Substitute.For<IBlockFinder>();
            Block pendingBlock = Build.A.Block.WithNumber(blockNumber).TestObject;
            blockFinder.FindBlock(BlockParameter.Pending).Returns(pendingBlock);
            FeeHistoryOracle feeHistoryOracle = GetSubstitutedFeeHistoryOracle(blockFinder: blockFinder);

            ResultWrapper<FeeHistoryResults> resultWrapper =
                feeHistoryOracle.GetFeeHistory(1, BlockParameter.Pending, null);
            
            resultWrapper.Data.OldestBlock.Should().Be(lastBlockNumberExpected);
        }
        
        [TestCase(2,2)]
        [TestCase(7,7)]
        [TestCase(32,32)]
        public void GetFeeHistory_IfLastBlockIsLatestBlock_LastBlockNumberSetToHeadBlockNumber(long blockNumber, long lastBlockNumberExpected)
        {
            IBlockFinder blockFinder = Substitute.For<IBlockFinder>();
            Block headBlock = Build.A.Block.WithNumber(blockNumber).TestObject;
            blockFinder.FindBlock(BlockParameter.Latest).Returns(headBlock);
            FeeHistoryOracle feeHistoryOracle = GetSubstitutedFeeHistoryOracle(blockFinder: blockFinder);

            ResultWrapper<FeeHistoryResults> resultWrapper =
                feeHistoryOracle.GetFeeHistory(1, BlockParameter.Latest, null);
            
            resultWrapper.Data.OldestBlock.Should().Be(lastBlockNumberExpected);
        }
        
        [Test]
        public void GetFeeHistory_IfLastBlockIsEarliestBlock_LastBlockNumberSetToGenesisBlockNumber()
        {
            IBlockFinder blockFinder = Substitute.For<IBlockFinder>();
            blockFinder.FindBlock(BlockParameter.Earliest).Returns(Build.A.Block.Genesis.TestObject);
            FeeHistoryOracle feeHistoryOracle = GetSubstitutedFeeHistoryOracle(blockFinder: blockFinder);

            ResultWrapper<FeeHistoryResults> resultWrapper =
                feeHistoryOracle.GetFeeHistory(1, BlockParameter.Earliest, null);
            
            resultWrapper.Data.OldestBlock.Should().Be(0);
        }
        
        [TestCase(30, new double[] {20,40,60,80.5}, new ulong[]{10,10,13,13})]
        [TestCase(40, new double[] {20,40,60,80.5}, new ulong[]{10,13,13,22})]
        [TestCase(40, new double[] {10,20,30,40}, new ulong[]{7,10,10,13})]
        public void CalculateAndInsertRewards_GivenValidInputs_CalculatesPercentilesCorrectly(long gasUsed, double[] rewardPercentiles, ulong[] expected)
        {
            Transaction[] transactions = new Transaction[]
            {                                                                                                                                         //Rewards: 
                Build.A.Transaction.WithHash(TestItem.KeccakA).WithMaxFeePerGas(20).WithMaxPriorityFeePerGas(13).WithType(TxType.EIP1559).TestObject, //13
                Build.A.Transaction.WithHash(TestItem.KeccakB).WithMaxFeePerGas(10).WithMaxPriorityFeePerGas(7).WithType(TxType.EIP1559).TestObject,  //7
                Build.A.Transaction.WithHash(TestItem.KeccakC).WithMaxFeePerGas(25).WithMaxPriorityFeePerGas(24).WithType(TxType.EIP1559).TestObject, //22
                Build.A.Transaction.WithHash(TestItem.KeccakD).WithMaxFeePerGas(15).WithMaxPriorityFeePerGas(10).WithType(TxType.EIP1559).TestObject  //10
            };
            IBlockFinder blockFinder = Substitute.For<IBlockFinder>();
            BlockParameter newestBlock = new BlockParameter((long) 0);
            Block headBlock = Build.A.Block.Genesis.WithBaseFeePerGas(3).WithTransactions(transactions).TestObject;
            blockFinder.FindBlock(newestBlock).Returns(headBlock);
            FeeHistoryOracle feeHistoryOracle = GetSubstitutedFeeHistoryOracle(blockFinder: blockFinder);

            ResultWrapper<FeeHistoryResults> resultWrapper = feeHistoryOracle.GetFeeHistory(1, newestBlock, rewardPercentiles);

            resultWrapper.Data.Reward!.Length.Should().Be(1);
            resultWrapper.Data.Reward[0].Should().BeEquivalentTo(expected);
        }
        
        [Test]
        public void GetFeeHistory_ResultsSortedInOrderOfAscendingBlockNumber()
        {
            Transaction txFirstBlock = Build.A.Transaction.WithGasPrice(3).WithMaxPriorityFeePerGas(2).TestObject; //Reward: Min (3 - 2, 2) => 1 
            Transaction txSecondBlock = Build.A.Transaction.WithGasPrice(2).WithMaxPriorityFeePerGas(3).TestObject; //BaseFee > FeeCap => 0
            Block firstBlock = Build.A.Block.Genesis.WithBaseFeePerGas(2).WithGasUsed(2).WithGasLimit(5).WithTransactions(txFirstBlock).TestObject;
            Block secondBlock = Build.A.Block.Genesis.WithBaseFeePerGas(3).WithGasUsed(4).WithGasLimit(8).WithTransactions(txSecondBlock).TestObject;
            IBlockFinder blockFinder = Substitute.For<IBlockFinder>();
            BlockParameter lastBlockParameter = new BlockParameter(1);
            blockFinder.FindBlock(lastBlockParameter).Returns(secondBlock);
            blockFinder.FindParent(secondBlock, BlockTreeLookupOptions.RequireCanonical).Returns(firstBlock);

            IReceiptFinder receiptFinder = Substitute.For<IReceiptFinder>();
            receiptFinder.Get(firstBlock).Returns(new TxReceipt[] {new TxReceipt() {GasUsed = 3}});
            receiptFinder.Get(secondBlock).Returns(new TxReceipt[] {new TxReceipt() {GasUsed = 2}});
            FeeHistoryOracle feeHistoryOracle = GetSubstitutedFeeHistoryOracle(blockFinder: blockFinder);
            double[] rewardPercentiles = {0};
            FeeHistoryResults expected = new FeeHistoryResults(0, new UInt256[]{5,6}, new double[]{0.4, 0.5}, new UInt256[][]{new UInt256[]{1}, new UInt256[]{0}});
            
            ResultWrapper<FeeHistoryResults> resultWrapper = feeHistoryOracle.GetFeeHistory(2, lastBlockParameter, rewardPercentiles);
            
            resultWrapper.Data.Should().BeEquivalentTo(expected);
        }
        
        private static FeeHistoryOracle GetSubstitutedFeeHistoryOracle(
            IBlockFinder? blockFinder = null, 
            IReceiptStorage? receiptStorage = null,
            ISpecProvider? specProvider = null)
        {
            return new(
                blockFinder ?? Substitute.For<IBlockFinder>(),
                receiptStorage ?? Substitute.For<IReceiptStorage>(),
                specProvider ?? Substitute.For<ISpecProvider>());
        }
    }
}
