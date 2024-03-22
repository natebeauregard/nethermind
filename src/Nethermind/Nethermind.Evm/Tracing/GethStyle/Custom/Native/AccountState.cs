// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Generic;
namespace Nethermind.Evm.Tracing.GethStyle.Custom.Native;

public class AccountState(string balance, ulong nonce, string code, Dictionary<string, string> storage)
{
    // TODO: lazily convert to JSON types
    public string Balance { get; set; } = balance;

    public ulong Nonce { get; set; } = nonce;

    public string Code { get; set; } = code;

    public Dictionary<string, string> Storage { get; set; } = storage;
}
