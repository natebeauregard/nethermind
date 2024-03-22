// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Specs;
namespace Nethermind.Evm.Tracing.GethStyle.Custom.Native;

public abstract class GethLikeNativeTxTracer : GethLikeTxTracer
{
    protected readonly IReleaseSpec _spec;

    protected GethLikeNativeTxTracer(
        IReleaseSpec spec,
        GethTraceOptions options) : base(options)
    {
        _spec = spec;
    }
}
