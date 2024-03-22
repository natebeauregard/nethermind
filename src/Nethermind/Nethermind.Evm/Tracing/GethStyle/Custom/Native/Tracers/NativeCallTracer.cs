// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using Nethermind.Core;
using Nethermind.Int256;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Evm.Precompiles;

namespace Nethermind.Evm.Tracing.GethStyle.Custom.Native.Tracers;

public sealed class NativeCallTracer : GethLikeTxTracer
{
     private readonly Log _log = new();
     private readonly Db _db;
     private readonly IReleaseSpec _spec;
     private int _depth = -1;

     // Context is updated only of first ReportAction call.
     private readonly Context _ctx;

     // TODO: see what the correct data structure is here
     private List<CallFrame> _callstack = new();
     private bool _descended;

     public NativeCallTracer(
         Db db,
         IReleaseSpec spec,
         Context ctx,
         GethTraceOptions options) : base(options)
     {
         IsTracingRefunds = true;
         IsTracingActions = true;
         IsTracingMemory = true;
         IsTracingStack = true;

         _db = db;
         _spec = spec;
         _ctx = ctx;

     }

     protected override GethLikeTxTrace CreateTrace() => new();

     public override GethLikeTxTrace BuildResult()
     {
         CallFrame callFrame = new CallFrame()
         {
             Type = _ctx.type,
             From = _ctx.From,
             To = _ctx.To,
             Value = _ctx.Value.ToHexString(true),
             Gas = _ctx.gas.ToHexString(true),
             GasUsed = _ctx.gasUsed.ToHexString(true),
             Input = _ctx.Input.ToString(),
             Output = _ctx.Output?.ToHexString(),
             Calls = _callstack
         };
         GethLikeTxTrace result = base.BuildResult();
         result.CustomTracerResult = new GethLikeCustomTrace() { Value = callFrame };
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
         _log.error = GethLikeCustomErrorDescription.GetCustomErrorDescription(error);

         // _tracer.fault(_log, _db);
         if (_callstack[^1].Error is null)
         {
             return;
         }

         CallFrame? call = _callstack[^1];
         _callstack.RemoveAt(_callstack.Count - 1);

         if (call.Gas is not null)
            call.GasUsed = call.Gas;

         if (_callstack.Count > 0)
         {
             _callstack[^1].Calls?.Add(call);
         }

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

     public void ReportActionRevert(long gasLeft, byte[] output)
     {
         base.ReportActionError(EvmExceptionType.Revert);
         _depth--;
     }

     public override void ReportActionError(EvmExceptionType evmExceptionType)
     {
         base.ReportActionError(evmExceptionType);
         _depth--;
     }

     public override void MarkAsFailed(Address recipient, long gasSpent, byte[]? output, string error, Hash256? stateRoot = null)
     {
         base.MarkAsFailed(recipient, gasSpent, output, error, stateRoot);
         _ctx.gasUsed = gasSpent;
         _ctx.Output = output;
         _ctx.error = error;
     }

     public override void MarkAsSuccess(Address recipient, long gasSpent, byte[] output, LogEntry[] logs, Hash256? stateRoot = null)
     {
         base.MarkAsSuccess(recipient, gasSpent, output, logs, stateRoot);
         _ctx.gasUsed = gasSpent;
         _ctx.Output = output;
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

         // if (_functions.HasFlag(TracerFunctions.step))
         // {
         //     _tracer.step(_log, _db);
         // }
         string error = _log.error;
         if (error != null)
         {
             // _tracer.fault(_log, _db);
             return;
         }

         // We only care about system opcodes, faster if we pre-check once
         // TODO: see if this works as expected, if not do `(log.op.toNumber() & 0xf0) == 0xf0;`
         Instruction? op = _log.op;
         bool syscall = op >= Instruction.CREATE;
         if (syscall)
         {
             if (op is Instruction.CREATE or Instruction.CREATE2)
             {
                 long inOffset = _log.stack.peek(1).ReadEthUInt32();
                 long inEnd = inOffset + _log.stack.peek(2).ReadEthUInt32();
                 var call = new CallFrame
                 {
                     Type = op.ToString(),
                     From = _log.contract.Address,
                     Input = _log.memory.slice(inOffset, inEnd).ToHexString(),
                     GasIn = _log.getGas().ToString(),
                     GasUsed = _log.getCost().ToString(),
                     Value = "0x" + _log.stack.peek(0).ToHexString()
                 };
                 _callstack.Add(call);
                 _descended = true;
                 return;
             }

             if (op is Instruction.SELFDESTRUCT)
             {
                 _callstack[^1].Calls ??= new List<CallFrame>();
                 _callstack[^1].Calls?.Add(new CallFrame
                 {
                     Type = op.ToString(),
                     From = _log.contract.Address,
                     To = _log.stack.peek(0).ToHexString().ToAddress(),
                     Gas = _log.getGas().ToString(),
                     GasUsed = _log.getCost().ToString(),
                     Value = "0x" + _db.GetBalance(_log.contract.Address).ToHexString(true)
                 });
                 return;
             }

             if (op is Instruction.CALL or Instruction.CALLCODE or Instruction.DELEGATECALL or Instruction.STATICCALL)
             {
                 var to = _log.stack.peek(1).ToHexString().ToAddress();
                 if (to.IsPrecompile(_spec))
                 {
                     return;
                 }
                 int offset = 0;
                 if (op is Instruction.CALL or Instruction.CALLCODE)
                     offset = 1;
                 long inOffset = _log.stack.peek(offset + 2).ReadEthUInt32();
                 long inEnd = _log.stack.peek(offset + 3).ReadEthUInt32();

                 var call = new CallFrame
                 {
                     Type = op.ToString(),
                     From = _log.contract.Address,
                     To = to,
                     Input = _log.memory.slice(inOffset, inEnd).ToHexString(),
                     Gas = _log.getGas().ToString(),
                     GasUsed = _log.getCost().ToString(),
                     OutOffset = _log.stack.peek(4 + offset).ReadEthInt32(),
                     OutLength = _log.stack.peek(5 + offset).ReadEthInt32()
                 };

                 if (op is Instruction.CALL or Instruction.CALLCODE)
                     call.Value = _log.stack.peek(2).ToHexString();

                 _callstack.Add(call);
                 _descended = true;
                 return;
             }
         }
         if (_descended)
         {
             // TODO: investigate if this should be _log.getDepth
             if (_depth >= _callstack.Count)
                _callstack[^1].GasUsed = _log.getCost().ToString();
             _descended = false;
         }

         if (syscall && op == Instruction.REVERT)
         {
             _callstack[^1].Error = "execution reverted";
             return;
         }

         if (_depth == _callstack.Count - 1)
         {
             // TODO: implement a stack pop here instead
             CallFrame call = _callstack[^1];
             _callstack.RemoveAt(_callstack.Count - 1);

             if (op is Instruction.CREATE or Instruction.CREATE2)
             {
                 call.GasUsed = (UInt256.Parse(call.GasIn) - UInt256.Parse(call.GasCost) + UInt256.Parse(call.Gas) - _log.getGas()).ToHexString(true);
             }
             ulong ret = _log.stack.peek(0).ReadEthUInt64();
             if (ret != 0)
             {
                 var retAddress = ret.ToHexString(false).ToAddress();
                 call.To = retAddress;
                 call.Output = _db.GetCode(retAddress);
             }
             else if (call.Error is null)
             {
                 call.Error = "internal failure";
             }

             // if (call.Gas is not null)
             // {
             //     call.gas = '0x' + bigInt(call.gas).toString(16);
             // }

             _callstack[^1].Calls?.Add(call);
         }

         if (_log.op == Instruction.REVERT)
         {
             ReportOperationError(EvmExceptionType.Revert);
         }
     }

     public override void ReportRefund(long refund)
     {
         base.ReportRefund(refund);
         _log.refund += refund;
     }
}
