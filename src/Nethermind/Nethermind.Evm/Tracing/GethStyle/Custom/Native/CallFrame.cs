// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Generic;
using Nethermind.Core;
using System.Text.Json.Serialization;

namespace Nethermind.Evm.Tracing.GethStyle.Custom.Native;

public class CallFrame
{
    public string? Type { get; set; }

    public Address? From { get; set; }

    public Address? To { get; set; }

    public string? Value { get; set; }

    public string? Gas { get; set; }

    public string? GasCost { get; set; }

    public string? Input { get; set; }

    public string? Output { get; set; }

    public string? Error { get; set; }

    public string? RevertReason { get; set; }

    //TODO: find a more performant data structure here
    public List<CallFrame?>? Calls { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public string? GasUsed { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public string? GasIn { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public long? OutOffset { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public long? OutLength { get; set; }
}
