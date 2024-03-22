// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Evm.Precompiles;
using Nethermind.Int256;
namespace Nethermind.Evm.Tracing.GethStyle.Custom.Native.Tracers;

// 4byteTracer searches for 4byte-identifiers, and collects them for post-processing.
// It collects the methods identifiers along with the size of the supplied data, so
// a reversed signature can be matched against the size of the data.
//
// Example:
//   > debug.traceTransaction( "0x214e597e35da083692f5386141e69f47e973b2c56e7a8073b1ea08fd7571e9de", {tracer: "4byteTracer"})
//   {
//     0x27dc297e-128: 1,
//     0x38cc4831-0: 2,
//     0x524f3889-96: 1,
//     0xadf59f99-288: 1,
//     0xc281d19e-0: 1
//   }
public sealed class JSPortNative4ByteTracer : GethLikeTxTracer
{
    private readonly Log _log = new();
    private readonly IReleaseSpec _spec;
    private int _depth = -1;
    private Dictionary<string, int> _4ByteIds = new();

    // Context is updated only of first ReportAction call.
    private readonly Context _ctx;

    public JSPortNative4ByteTracer(
        Context ctx,
        IReleaseSpec spec,
        GethTraceOptions options) : base(options)
    {
        IsTracingActions = true;
        IsTracingMemory = true;
        IsTracingStack = true;

        _ctx = ctx;
        _spec = spec;
    }

    protected override GethLikeTxTrace CreateTrace() => new();

    public override GethLikeTxTrace BuildResult()
    {
        GethLikeTxTrace result = base.BuildResult();

        int txDataLength = _ctx.Input.Length;
        if (txDataLength >= 4)
        {
            string _4byteTxData = _ctx.Input.Span[..4].ToHexString();
            Store4ByteIds(_4byteTxData, txDataLength-4);
        }

        result.CustomTracerResult = new GethLikeCustomTrace() { Value = _4ByteIds };
        return result;
    }

    public override void ReportAction(long gas, UInt256 value, Address from, Address to, ReadOnlyMemory<byte> input, ExecutionType callType, bool isPrecompileCall = false)
    {
        _depth++;

        base.ReportAction(gas, value, from, to, input, callType, isPrecompileCall);

        bool isAnyCreate = callType.IsAnyCreate();
        if (_depth == 0)
        {
            _ctx.type = isAnyCreate ? "CREATE" : "CALL";
            _ctx.From = from;
            _ctx.To = to;
            _ctx.Input = input;
            _ctx.Value = value;
        }

        _log.contract = callType == ExecutionType.DELEGATECALL
            ? new Log.Contract(_log.contract.Caller, from, value, input)
            : new Log.Contract(from, to, value, isAnyCreate ? null : input);
    }

    public override void StartOperation(int depth, long gas, Instruction opcode, int pc, bool isPostMerge = false)
    {
        _log.pc = pc;
        _log.op = opcode;
        _log.gas = gas;
        _log.depth = depth;
        _log.error = null;
        _log.gasCost = null;
    }

    public override void ReportOperationRemainingGas(long gas)
    {
        _log.gasCost ??= _log.gas - gas;
    }

    public override void ReportOperationError(EvmExceptionType error)
    {
        base.ReportOperationError(error);
        _log.error = error.GetCustomErrorDescription();
    }

    public override void ReportActionEnd(long gas, Address deploymentAddress, ReadOnlyMemory<byte> deployedCode)
    {
        base.ReportActionEnd(gas, deploymentAddress, deployedCode);

        _ctx.To ??= deploymentAddress;
        _depth--;
    }

    public override void ReportActionEnd(long gas, ReadOnlyMemory<byte> output)
    {
        base.ReportActionEnd(gas, output);
        _depth--;
    }

    public override void ReportActionError(EvmExceptionType evmExceptionType)
    {
        base.ReportActionError(evmExceptionType);
        _depth--;
    }

    public override void SetOperationMemory(TraceMemory memoryTrace)
    {
        base.SetOperationMemory(memoryTrace);
        _log.memory.MemoryTrace = memoryTrace;
    }

    public override void SetOperationStack(TraceStack stack)
    {
        base.SetOperationStack(stack);
        _log.stack = new Log.Stack(stack);

        // Skip any opcodes that are not internal calls
        //TODO: investigate nullable warning
        Instruction? op = _log.op;
        int callType = op switch
        {
            Instruction.CALL or Instruction.DELEGATECALL =>
                // gas, addr, val, memin, meminsz, memout, memoutsz
                3 // stack ptr to memin
            ,
            Instruction.STATICCALL or Instruction.CALLCODE =>
                // gas, addr, memin, meminsz, memout, memoutsz
                2 // stack ptr to memin
            ,
            _ => 0
        };
        if (callType == 0)
            return;

        // Skip any pre-compile invocations, those are just fancy opcodes
        var address = _log.stack.peek(1).ToHexString().ToAddress();
        if (address.IsPrecompile(_spec))
            return;

        // Gather internal call details
        ReadOnlySpan<byte> inputSizeSpan = _log.stack.peek(callType + 1);
        int inputSize = inputSizeSpan.ReadEthInt32();
        if (inputSize < 4)
            return;

        ReadOnlySpan<byte> inputOffestSpan = _log.stack.peek(callType);
        long inputOffset = inputOffestSpan.ReadEthUInt32();
        string _4byteTxData = _log.memory.slice(inputOffset, inputOffset+4).ToHexString();
        Store4ByteIds(_4byteTxData, inputSize-4);

        // TODO: investigate if this is necessary here
        // if (_log.op?.Value == Instruction.REVERT)
        // {
        //     ReportOperationError(EvmExceptionType.Revert);
        // }
    }

    private void Store4ByteIds(string _4byteTxData, int size)
    {
        string _4byteId = _4byteTxData + '-' + size;
        _4ByteIds[_4byteId] = _4ByteIds.TryGetValue(_4byteId, out int count) ? count + 1 : 1;
    }
}
