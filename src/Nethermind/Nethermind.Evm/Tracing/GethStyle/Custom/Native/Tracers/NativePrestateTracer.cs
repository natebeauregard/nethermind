// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Evm.Tracing.GethStyle.Custom.JavaScript;
using Nethermind.Int256;
using Nethermind.State;

namespace Nethermind.Evm.Tracing.GethStyle.Custom.Native.Tracers;

public sealed class NativePrestateTracer : GethLikeNativeTxTracer
{
    public const string PrestateTracer = "prestateTracer";
    private readonly UInt256 _gasPrice;
    private readonly UInt256 _gasLimit;
    private readonly Address _contractAddress;
    private TraceMemory _memoryTrace;
    private Instruction _op;
    private readonly Dictionary<Address, NativePrestateTracerAccount> _prestate;

    public NativePrestateTracer(
        IWorldState worldState,
        GethLikeBlockNativeTracer.Context context,
        GethTraceOptions options) : base(worldState, options)
    {
        IsTracingActions = true;
        // IsTracingMemory = true;
        // IsTracingStack = true;
        // IsTracingStorage = true;

        _prestate = new Dictionary<Address, NativePrestateTracerAccount>();
        _gasPrice = context.GasPrice;
        _gasLimit = new UInt256(context.GasLimit.ToBigEndianByteArray(), true);
        _contractAddress = context.ContractAddress;
    }

    protected override GethLikeTxTrace CreateTrace() => new();

    public override GethLikeTxTrace BuildResult()
    {
        GethLikeTxTrace result = base.BuildResult();

        result.CustomTracerResult = new GethLikeCustomTrace() { Value = _prestate };
        return result;
    }

    public override void ReportAction(long gas, UInt256 value, Address from, Address to, ReadOnlyMemory<byte> input, ExecutionType callType, bool isPrecompileCall = false)
    {
        base.ReportAction(gas, value, from, to, input, callType, isPrecompileCall);

        if (Depth == 0)
        {
            CaptureStart(value, from, to);
        }
    }

    public override void StartOperation(int depth, long gas, Instruction opcode, int pc, bool isPostMerge = false)
    {
        base.StartOperation(depth, gas, opcode, pc, isPostMerge);

        _op = opcode;
    }

    // public override void SetOperationStorage(Address address, UInt256 storageIndex, ReadOnlySpan<byte> newValue, ReadOnlySpan<byte> currentValue)
    // {
    //     base.SetOperationStorage(address, storageIndex, newValue, currentValue);
    //
    //     // TODO: see if stacklen needs to be checked
    //     LookupStorage(address, storageIndex);
    // }

    // public override void LoadOperationStorage(Address address, UInt256 storageIndex, ReadOnlySpan<byte> value)
    // {
    //     base.LoadOperationStorage(address, storageIndex, value);
    //
    //     LookupStorage(address, storageIndex);
    // }

    public override void SetOperationMemory(TraceMemory memoryTrace)
    {
        base.SetOperationMemory(memoryTrace);
        _memoryTrace = memoryTrace;
    }

    public override void SetOperationStack(TraceStack stack)
    {
        base.SetOperationStack(stack);

        int stackLen = stack.Count;
        Address address;

        // TODO: refactor switch to be more readable
        switch (stackLen)
        {
            case >= 1 when _op is Instruction.EXTCODECOPY or Instruction.EXTCODEHASH or Instruction.EXTCODESIZE or Instruction.BALANCE or Instruction.SELFDESTRUCT:
                address = stack.Peek(0).ToHexString(true).ToAddress();
                LookupAccount(address);
                break;
            case >= 5 when _op is Instruction.DELEGATECALL or Instruction.CALL or Instruction.STATICCALL or Instruction.CALLCODE:
                address = stack.Peek(1).ToHexString(true).ToAddress();
                LookupAccount(address);
                break;
            case >= 4 when _op is Instruction.CREATE2:
                int offset = stack.Peek(1).ToInt32(null);
                int length = stack.Peek(2).ToInt32(null);
                ReadOnlySpan<byte> initCode = _memoryTrace.Slice(offset, length);
                string salt = stack.Peek(3).ToHexString(true);
                address = ContractAddress.From(_contractAddress, Bytes.FromHexString(salt, EvmStack.WordSize), initCode);
                LookupAccount(address);
                break;
            default:
                {
                    if (_op is Instruction.CREATE)
                    {
                        LookupAccount(_contractAddress);
                    }
                    break;
                }
        }
    }

    private void CaptureStart(UInt256 value, Address from, Address to)
    {
        LookupAccount(from);
        LookupAccount(to);

        UInt256 toBal = _prestate[to].Balance - value;
        _prestate[to].Balance = toBal;

        UInt256 consumedGas = _gasPrice * _gasLimit;
        UInt256 fromBal = _prestate[from].Balance - consumedGas;
        _prestate[from].Balance = fromBal;
        _prestate[from].Nonce -= 1;
    }

    private void LookupAccount(Address addr)
    {
        if (_prestate.ContainsKey(addr)) return;

        if (_worldState!.TryGetAccount(addr, out AccountStruct account))
        {
            _prestate.Add(addr, new NativePrestateTracerAccount
            {
                Balance = account.Balance,
                Nonce = account.Nonce,
                Code = _worldState.GetCode(addr)
            });
        }
    }

    private void LookupStorage(Address addr, UInt256 index)
    {
        // ValueHash256 hash = new(index.ToBigEndian());
        string key = index.ToHexString(false);
        if (_prestate[addr].Storage.ContainsKey(key)) return;

        // TODO: check if hash or index should be used
        ReadOnlySpan<byte> storage = _worldState!.Get(new StorageCell(addr, index));
        // _prestate[addr].Storage.Add(key, storage.ToArray());
        _prestate[addr].Storage.Add(key, storage.ToHexString(true, false));
    }
}

public class NativePrestateTracerAccount
{
    public UInt256 Balance { get; set; }
    public UInt256 Nonce { get; set; }
    public byte[]? Code { get; set; }
    // public Dictionary<ValueHash256, byte[]?> Storage { get; set; } = new();
    public Dictionary<string, string> Storage { get; set; } = new();
}
