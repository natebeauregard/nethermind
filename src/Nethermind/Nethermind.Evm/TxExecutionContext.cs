// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;
using Nethermind.Int256;

namespace Nethermind.Evm
{
    public readonly struct TxExecutionContext
    {
        public readonly BlockExecutionContext BlockExecutionContext;
        public Address Origin { get; }
        public Address? Destination { get; }
        public UInt256 GasPrice { get; }
        public byte[][]? BlobVersionedHashes { get; }

        public TxExecutionContext(in BlockExecutionContext blockExecutionContext, Address origin, Address? destination, in UInt256 gasPrice, byte[][] blobVersionedHashes)
        {
            BlockExecutionContext = blockExecutionContext;
            Origin = origin;
            Destination = destination;
            GasPrice = gasPrice;
            BlobVersionedHashes = blobVersionedHashes;
        }
    }
}
