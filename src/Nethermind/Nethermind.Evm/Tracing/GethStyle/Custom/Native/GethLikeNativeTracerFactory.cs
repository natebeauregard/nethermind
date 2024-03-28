// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using Nethermind.Evm.Tracing.GethStyle.Custom.Native.Tracers;
using Nethermind.State;
namespace Nethermind.Evm.Tracing.GethStyle.Custom.Native;

public static class GethLikeNativeTracerFactory
{
    static GethLikeNativeTracerFactory() => RegisterNativeTracers();

    private static readonly Dictionary<string, Func<IWorldState, GethLikeBlockNativeTracer.Context, GethTraceOptions, GethLikeNativeTxTracer>> _tracers = new();

    // TODO: move native context into a separate file
    public static GethLikeNativeTxTracer CreateTracer(IWorldState worldState, GethLikeBlockNativeTracer.Context context, GethTraceOptions options) =>
        _tracers.TryGetValue(options.Tracer, out Func<IWorldState, GethLikeBlockNativeTracer.Context, GethTraceOptions, GethLikeNativeTxTracer> tracerFunc)
        ? tracerFunc(worldState, context, options)
        : throw new ArgumentException($"Unknown tracer: {options.Tracer}");

    public static bool IsNativeTracer(string tracerName)
    {
        return _tracers.ContainsKey(tracerName);
    }

    private static void RegisterNativeTracers()
    {
        RegisterTracer(Native4ByteTracer.FourByteTracer, (_, _, options) => new Native4ByteTracer(options));
        RegisterTracer(NativePrestateTracer.PrestateTracer, (worldState, context, options) => new NativePrestateTracer(worldState, context, options));
    }

    private static void RegisterTracer(string tracerName, Func<IWorldState, GethLikeBlockNativeTracer.Context, GethTraceOptions, GethLikeNativeTxTracer> tracerFunc)
    {
        _tracers.Add(tracerName, tracerFunc);
    }
}
