// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using Nethermind.Core;
using Nethermind.Int256;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;

namespace Nethermind.Evm.Tracing.GethStyle.Custom.Native.Tracers;

public sealed class NativePrestateTracer : GethLikeTxTracer
{
    private readonly Log _log = new();
    private readonly Db _db;
    // TODO: check if this should use ArrayPoolList instead
    private Dictionary<Address, AccountState> _prestate;
    private int _depth = -1;

    // Context is updated only of first ReportAction call.
    private readonly Context _ctx;

    public NativePrestateTracer(
        Db db,
        Context ctx,
        GethTraceOptions options) : base(options)
    {
        IsTracingActions = true;
        IsTracingMemory = true;
        IsTracingStack = true;
        IsTracingStorage = true;

        _db = db;
        _ctx = ctx;
        _prestate = new Dictionary<Address, AccountState>();
    }

    protected override GethLikeTxTrace CreateTrace() => new();

    public override GethLikeTxTrace BuildResult()
    {
        Address toAddr = _ctx.To;
        Address fromAddr = _ctx.From;

        if (_prestate.Count == 0)
        {
            LookupAccount(_ctx.To);
        }

        LookupAccount(_ctx.From);

        UInt256 outerTransactionValue = _ctx.Value;
        //TODO: convert balance to hex string before building result
        UInt256 toBalance = UInt256.Parse(_prestate[toAddr].Balance);
        UInt256 fromBalance = UInt256.Parse(_prestate[fromAddr].Balance);
        toBalance -= outerTransactionValue;
        fromBalance += outerTransactionValue + ((UInt256)_ctx.gasUsed * _ctx.GasPrice);
        _prestate[toAddr].Balance = toBalance.ToHexString(true);
        _prestate[fromAddr].Balance = fromBalance.ToHexString(true);

        _prestate[fromAddr].Nonce -= 1;

        if (_ctx.type == "CREATE")
        {
            _prestate.Remove(toAddr);
        }

        GethLikeTxTrace result = base.BuildResult();
        result.CustomTracerResult = new GethLikeCustomTrace() { Value = _prestate };
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

        if (_prestate.Count == 0)
        {
            LookupAccount(_log.contract.Address);
        }

        Instruction? op = _log.op;
        switch(op)
        {
            case Instruction.EXTCODECOPY:
            case Instruction.EXTCODESIZE:
            case Instruction.EXTCODEHASH:
            case Instruction.BALANCE:
                LookupAccount(_log.stack.peek(0).ToHexString().ToAddress());
                break;
            case Instruction.CREATE:
                LookupAccount(_log.contract.Address);
                break;
            case Instruction.CREATE2:
                Address from = _log.contract.Address;
                int offset = _log.stack.peek(1).ReadEthInt32();
                int size = _log.stack.peek(2).ReadEthInt32();
                int end = offset + size;
                ReadOnlySpan<byte> saltBytes = Bytes.FromHexString(_log.stack.peek(3).ToHexString(), EvmStack.WordSize);
                ReadOnlySpan<byte> initcode = _log.memory.slice(offset, end);
                var address = ContractAddress.From(from.ToAddress(), saltBytes, initcode).Bytes.ToAddress();
                LookupAccount(address);
                break;
            case Instruction.CALL:
            case Instruction.CALLCODE:
            case Instruction.DELEGATECALL:
            case Instruction.STATICCALL:
                LookupAccount(_log.stack.peek(1).ToHexString().ToAddress());
                break;
            case Instruction.SSTORE:
            case Instruction.SLOAD:
                //TODO: ensure that ToHexString is correct here
                LookupStorage(_log.contract.Address, _log.stack.peek(0).ToHexString());
                break;
        }

        if (op == Instruction.REVERT)
        {
            ReportOperationError(EvmExceptionType.Revert);
        }
    }

    private void LookupAccount(Address? address)
    {
        if (address is null || _prestate.TryGetValue(address, out AccountState? _))
            return;

        string balance = _db.GetBalance(address).ToHexString(true);
        ulong nonce = _db.GetNonce(address);
        string code = _db.GetCode(address);
        // TODO: uncomment and readd `code` after testing
        // _prestate[address] = new AccountState(balance, nonce, code, new Dictionary<string, string>());
        _prestate[address] = new AccountState(balance, nonce, null, new Dictionary<string, string>());

    }

    private void LookupStorage(Address address, string key)
    {
        AccountState account = _prestate[address];
        if (account.Storage.TryGetValue(key, out _))
            return;

        string state = _db.GetState(address, key[..EvmPooledMemory.WordSize]);
        account.Storage[key] = state;
    }
}
