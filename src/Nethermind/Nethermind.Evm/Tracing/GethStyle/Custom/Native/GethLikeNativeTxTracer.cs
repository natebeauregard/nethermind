// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using Nethermind.Core;
using Nethermind.Int256;
namespace Nethermind.Evm.Tracing.GethStyle.Custom.Native;

public abstract class GethLikeNativeTxTracer : GethLikeTxTracer
{
    protected int Depth { get; private set; }

    protected GethLikeNativeTxTracer(GethTraceOptions options) : base(options)
    {
        Depth = -1;
    }

    public override void ReportAction(in ExecutionEnvironment env, long gas, Address from, Address to, ReadOnlyMemory<byte> input, ExecutionType callType, bool isPrecompileCall = false)
    {
        base.ReportAction(env, gas, from, to, input, callType, isPrecompileCall);
        Depth++;
    }

    public override void ReportActionEnd(long gas, Address deploymentAddress, ReadOnlyMemory<byte> deployedCode)
    {
        base.ReportActionEnd(gas, deploymentAddress, deployedCode);
        Depth--;
    }

    public override void ReportActionEnd(long gas, ReadOnlyMemory<byte> output)
    {
        base.ReportActionEnd(gas, output);
        Depth--;
    }

    public override void ReportActionError(EvmExceptionType evmExceptionType)
    {
        base.ReportActionError(evmExceptionType);
        Depth--;
    }
}
