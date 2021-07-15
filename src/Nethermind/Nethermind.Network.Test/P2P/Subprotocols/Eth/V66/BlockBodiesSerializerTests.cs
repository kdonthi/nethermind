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

using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test.Builders;
using Nethermind.Network.P2P.Subprotocols.Eth.V66;
using Nethermind.Network.Test.P2P.Subprotocols.Eth.V62;
using NUnit.Framework;
using BlockBodiesMessage = Nethermind.Network.P2P.Subprotocols.Eth.V66.BlockBodiesMessage;

namespace Nethermind.Network.Test.P2P.Subprotocols.Eth.V66
{
    [TestFixture]
    public class BlockBodiesSerializerTests
    {
        //test from https://github.com/ethereum/EIPs/blob/master/EIPS/eip-2481.md
        [Test]
        public void RoundTrip()
        {
            BlockHeader header = Build.A.BlockHeader.TestObject;
            header.ParentHash = Keccak.Zero;
            header.OmmersHash = Keccak.Zero;
            header.Beneficiary = Address.Zero;
            header.StateRoot = Keccak.Zero;
            header.TxRoot = Keccak.Zero;
            header.ReceiptsRoot = Keccak.Zero;
            header.Bloom = Bloom.Empty;
            header.Difficulty = 0x8ae;
            header.Number = 0xd05;
            header.GasLimit = 0x115c;
            header.GasUsed = 0x15b3;
            header.Timestamp = 0x1a0a;
            header.ExtraData = new byte[]{0x77, 0x88};
            header.MixHash = Keccak.Zero;
            header.Nonce = 0;
            header.Hash = new Keccak("0x8c2f2af15b7b563b6ab1e09bed0e9caade7ed730aec98b70a993597a797579a9");

            Transaction tx1 = new Transaction();
            tx1.Nonce = 8;
            tx1.GasPrice = 0x4a817c808;
            tx1.GasLimit = 0x2e248;
            tx1.To = new Address("0x3535353535353535353535353535353535353535");
            tx1.Value = 0x200;
            tx1.Data = new byte[]{};
            tx1.Signature = new Signature(new Keccak("0x64b1702d9298fee62dfeccc57d322a463ad55ca201256d01f62b45b2e1c21c12").Bytes, new Keccak("0x64b1702d9298fee62dfeccc57d322a463ad55ca201256d01f62b45b2e1c21c10").Bytes, 0x25);
            tx1.Hash = new Keccak("0x588df025c4c2d757d3e314bd3dfbfe352687324e6b8557ad1731585e96928aed");
            
            Transaction tx2 = new Transaction();
            tx2.Nonce = 9;
            tx2.GasPrice = 0x4a817c809;
            tx2.GasLimit = 0x33450;
            tx2.To = new Address("0x3535353535353535353535353535353535353535");
            tx2.Value = 0x2d9;
            tx2.Data = new byte[]{};
            tx2.Signature = new Signature(new Keccak("0x52f8f61201b2b11a78d6e866abc9c3db2ae8631fa656bfe5cb53668255367afb").Bytes, new Keccak("0x52f8f61201b2b11a78d6e866abc9c3db2ae8631fa656bfe5cb53668255367afb").Bytes, 0x25);
            tx2.Hash = new Keccak("0xf39c7dac06a9f3abf09faf5e30439a349d3717611b3ed337cd52b0d192bc72da");

            var ethMessage = new Network.P2P.Subprotocols.Eth.V62.BlockBodiesMessage();
            ethMessage.Bodies = new[] {new BlockBody(new[] {tx1, tx2}, new[] {header})};

            BlockBodiesMessage message = new BlockBodiesMessage(1111, ethMessage);

            BlockBodiesMessageSerializer serializer = new BlockBodiesMessageSerializer();
            
            SerializerTester.TestZero(serializer, message, "f902dc820457f902d6f902d3f8d2f867088504a817c8088302e2489435353535353535353535353535353535353535358202008025a064b1702d9298fee62dfeccc57d322a463ad55ca201256d01f62b45b2e1c21c12a064b1702d9298fee62dfeccc57d322a463ad55ca201256d01f62b45b2e1c21c10f867098504a817c809830334509435353535353535353535353535353535353535358202d98025a052f8f61201b2b11a78d6e866abc9c3db2ae8631fa656bfe5cb53668255367afba052f8f61201b2b11a78d6e866abc9c3db2ae8631fa656bfe5cb53668255367afbf901fcf901f9a00000000000000000000000000000000000000000000000000000000000000000a00000000000000000000000000000000000000000000000000000000000000000940000000000000000000000000000000000000000a00000000000000000000000000000000000000000000000000000000000000000a00000000000000000000000000000000000000000000000000000000000000000a00000000000000000000000000000000000000000000000000000000000000000b90100000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000008208ae820d0582115c8215b3821a0a827788a00000000000000000000000000000000000000000000000000000000000000000880000000000000000");
        }
    }
}