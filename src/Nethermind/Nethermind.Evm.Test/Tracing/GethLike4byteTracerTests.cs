// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Generic;
using FluentAssertions;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm.Precompiles;
using Nethermind.Evm.Tracing.GethStyle;
using Nethermind.Evm.Tracing.GethStyle.Custom.Native.Tracers;
using NUnit.Framework;
namespace Nethermind.Evm.Test.Tracing;

[TestFixture]
public class GethLike4byteTracerTests : GethLikeNativeTracerTestsBase
{
    [Test]
    public void Trace_call_input()
    {
        byte[] callInput = Prepare.EvmCode
            .PushData(SampleHexData2)
            .STOP()
            .Done;

        byte[] empty6ByteInput = new byte[6];

        byte[] code = Prepare.EvmCode
            .CallWithInput(TestItem.AddressA, 50000, callInput)
            .CallWithInput(TestItem.AddressA, 50000, callInput)
            .CallWithInput(TestItem.AddressA, 50000, empty6ByteInput)
            .STOP()
            .Done;

        Dictionary<string, int> expected4ByteIds = new()
        {
            { "62b15678-1", 2 },
            { "00000000-2", 1 }
        };

        GethLikeTxTrace trace = ExecuteAndTrace(Native4ByteTracer._4byteTracer, code);
        trace.CustomTracerResult?.Value.Should().BeEquivalentTo(expected4ByteIds);
    }

    [Test]
    public void Trace_call_input_ignore_data_less_than_4_bytes()
    {
        byte[] callInput = Prepare.EvmCode
            .STOP()
            .Done;

        byte[] empty3ByteInput = new byte[3];

        byte[] code = Prepare.EvmCode
            .CallWithInput(TestItem.AddressA, 50000, callInput)
            .CallWithInput(TestItem.AddressA, 50000, empty3ByteInput)
            .STOP()
            .Done;

        Dictionary<string, int> expected4ByteIds = new();

        GethLikeTxTrace trace = ExecuteAndTrace(Native4ByteTracer._4byteTracer, code);
        trace.CustomTracerResult?.Value.Should().BeEquivalentTo(expected4ByteIds);
    }

    [Test]
    public void Trace_call_input_ignore_precompile()
    {
        byte[] callInput = Prepare.EvmCode
            .PushData(SampleHexData2)
            .STOP()
            .Done;

        byte[] code = Prepare.EvmCode
            .CallWithInput(IdentityPrecompile.Address, 50000, callInput)
            .STOP()
            .Done;

        Dictionary<string, int> expected4ByteIds = new();

        GethLikeTxTrace trace = ExecuteAndTrace(Native4ByteTracer._4byteTracer, code);
        trace.CustomTracerResult?.Value.Should().BeEquivalentTo(expected4ByteIds);
    }
}
