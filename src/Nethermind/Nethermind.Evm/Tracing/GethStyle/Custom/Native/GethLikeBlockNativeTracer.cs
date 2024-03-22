// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only


using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Int256;
using Nethermind.State;
namespace Nethermind.Evm.Tracing.GethStyle.Custom.Native;

public class GethLikeBlockNativeTracer : BlockTracerBase<GethLikeTxTrace, GethLikeTxTracer>
{
    private readonly IReleaseSpec _spec;
    private readonly GethTraceOptions _options;
    private readonly Context _ctx;
    private readonly Db _db;
    private int _index;
    private UInt256 _baseFee;

    public GethLikeBlockNativeTracer(IWorldState worldState, IReleaseSpec spec, GethTraceOptions options) : base(options.TxHash)
    {
        _spec = spec;
        _options = options;
        _ctx = new Context();
        _db = new Db(worldState);
    }

    public override void StartNewBlockTrace(Block block)
    {
        _ctx.block = block.Number;
        _ctx.BlockHash = block.Hash;
        _baseFee = block.BaseFeePerGas;
        base.StartNewBlockTrace(block);
    }

    protected override GethLikeTxTracer OnStart(Transaction? tx)
    {
        SetTransactionCtx(tx);
        return GethLikeNativeTracerFactory.CreateNativeTracer(_db, _ctx, _spec, _options);
    }

    private void SetTransactionCtx(Transaction? tx)
    {
        _ctx.GasPrice = tx!.CalculateEffectiveGasPrice(_spec.IsEip1559Enabled, _baseFee);
        _ctx.TxHash = tx.Hash;
        _ctx.txIndex = tx.Hash is not null ? _index++ : null;
        _ctx.gas = tx.GasLimit;
        _ctx.type = "CALL";
        _ctx.From = tx.SenderAddress;
        _ctx.To = tx.To;
        _ctx.Value = tx.Value;
        if (tx.Data is not null)
        {
            _ctx.Input = tx.Data.Value;
        }
    }

    protected override bool ShouldTraceTx(Transaction? tx) => base.ShouldTraceTx(tx) && tx is not null;

    protected override GethLikeTxTrace OnEnd(GethLikeTxTracer txTracer) => txTracer.BuildResult();
}
