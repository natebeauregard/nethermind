// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Buffers;
using System.Collections;
using System.Linq;
using System.Numerics;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Int256;
using Nethermind.State;

namespace Nethermind.Evm.Tracing.GethStyle.Custom.Native;

public class Db(IWorldState worldState)
{
    private IWorldState WorldState { get; } = worldState;

    public UInt256 GetBalance(Address address) => WorldState.GetBalance(address);

    public ulong GetNonce(Address address) => (ulong)WorldState.GetNonce(address);

    public string GetCode(Address address) => WorldState.GetCode(address).ToHexString();

    public string GetState(Address address, string hex)
    {
        ReadOnlySpan<byte> bytes = WorldState.Get(new StorageCell(address, new ValueHash256(hex)));
        return bytes.ToHexString();
    }

    public bool exists(object address) => WorldState.TryGetAccount(address.ToAddress(), out AccountStruct account) && !account.IsTotallyEmpty;
}
