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

namespace Nethermind.Network.P2P.Subprotocols.Eth.V66
{
    public class PooledTransactionsMessage : Eth66Message<Eth.V65.PooledTransactionsMessage>
    {
        public PooledTransactionsMessage() 
        {
        }
        
        public PooledTransactionsMessage(ulong requestId, Eth.V65.PooledTransactionsMessage ethMessage) : base(requestId, ethMessage)
        {
        }
    }
}
