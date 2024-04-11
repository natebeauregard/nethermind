// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Text.Json;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm.Tracing.GethStyle;
using Nethermind.Evm.Tracing.GethStyle.Custom.Native.Prestate;
using Nethermind.Serialization.Json;
using Nethermind.Specs;
using Nethermind.State;
using NUnit.Framework;

namespace Nethermind.Evm.Test.Tracing;

[TestFixture]
public class GethLikePrestateTracerTests : VirtualMachineTestsBase
{
    private static readonly JsonSerializerOptions Options = EthereumJsonSerializer.JsonOptionsIndented;
    private static readonly SenderRecipientAndMiner TxAddresses = new();

    [Test]
    public void Test_PrestateTrace_SStore()
    {
        StorageCell storageCell = new StorageCell(TestItem.AddressB, 32);
        byte[] storageData = Bytes.FromHexString("123456789abcdef");
        TestState.Set(storageCell, storageData);
        NativePrestateTracer tracer = new(TestState, GethTraceOptions.Default with { Tracer = NativePrestateTracer.PrestateTracer });

        byte[] sstoreCode = Prepare.EvmCode
            .PushData(SampleHexData1.PadLeft(64, '0'))
            .PushData(0)
            .Op(Instruction.SSTORE)
            .PushData(SampleHexData2.PadLeft(64, '0'))
            .PushData(32)
            .Op(Instruction.SSTORE)
            .Op(Instruction.STOP)
            .Done;

        GethLikeTxTrace prestateTrace = Execute(
                tracer,
                sstoreCode,
                MainnetSpecProvider.CancunActivation,
                TxAddresses)
            .BuildResult();
        const string expectedPrestateTrace = """
{
  "0xb7705ae4c6f81b66cdb323c65f4e8133690fc099": {
    "balance": "0x56bc75e2d630e795f",
    "nonce": 1
  },
  "0x942921b14f1b1c385cd7e0cc2ef7abe5598c8358": {
    "balance": "0x56bc75e2d63100000",
    "code": "0x7f0000000000000000000000000000000000000000000000000000000000a012346000557f0000000000000000000000000000000000000000000000000000000000b1567860205500",
    "storage": {
      "0x0000000000000000000000000000000000000000000000000000000000000000": "0x0000000000000000000000000000000000000000000000000000000000000000",
      "0x0000000000000000000000000000000000000000000000000000000000000020": "0x0000000000000000000000000000000000000000000000000123456789abcdef"
    }
  },
  "0x475674cb523a0a2736b7f7534390288fce16982c": {
    "balance": "0x0"
  }
}
""";
        Assert.That(JsonSerializer.Serialize(prestateTrace.CustomTracerResult?.Value, Options), Is.EqualTo(expectedPrestateTrace));
    }

    [Test]
    public void Test_PrestateTrace_NestedCalls()
    {
        NativePrestateTracer tracer = new(TestState, GethTraceOptions.Default with { Tracer = NativePrestateTracer.PrestateTracer });

        byte[] deployedCode = new byte[3];

        byte[] initCode = Prepare.EvmCode
            .ForInitOf(deployedCode)
            .Done;

        byte[] createCode = Prepare.EvmCode
            .Create(initCode, 0)
            .Op(Instruction.STOP)
            .Done;

        TestState.CreateAccount(TestItem.AddressC, 1.Ether());
        TestState.InsertCode(TestItem.AddressC, createCode, Spec);
        byte[] code = Prepare.EvmCode
            .DelegateCall(TestItem.AddressC, 50000)
            .Op(Instruction.STOP)
            .Done;

        GethLikeTxTrace prestateTrace = Execute(
                tracer,
                code,
                MainnetSpecProvider.CancunActivation,
                TxAddresses)
            .BuildResult();
        const string expectedPrestateTrace = """
{
  "0xb7705ae4c6f81b66cdb323c65f4e8133690fc099": {
    "balance": "0x56bc75e2d630e795f",
    "nonce": 1
  },
  "0x942921b14f1b1c385cd7e0cc2ef7abe5598c8358": {
    "balance": "0x56bc75e2d63100000",
    "code": "0x60006000600060007376e68a8696537e4141926f3e528733af9e237d6961c350f400"
  },
  "0x475674cb523a0a2736b7f7534390288fce16982c": {
    "balance": "0x0"
  },
  "0x76e68a8696537e4141926f3e528733af9e237d69": {
    "balance": "0xde0b6b3a7640000",
    "code": "0x7f7f000000000000000000000000000000000000000000000000000000000000006000527f0060005260036000f30000000000000000000000000000000000000000000000602052602960006000f000"
  },
  "0x89aa9b2ce05aaef815f25b237238c0b4ffff6ae3": {
    "balance": "0x0"
  }
}
""";
        Assert.That(JsonSerializer.Serialize(prestateTrace.CustomTracerResult?.Value, Options), Is.EqualTo(expectedPrestateTrace));
    }

    [Test]
    public void Test_PrestateTrace_Create2()
    {
        NativePrestateTracer tracer = new(TestState, GethTraceOptions.Default with { Tracer = NativePrestateTracer.PrestateTracer });

        byte[] salt = { 4, 5, 6 };

        byte[] deployedCode = { 1, 2, 3 };

        byte[] initCode = Prepare.EvmCode
            .ForInitOf(deployedCode).Done;

        byte[] createCode = Prepare.EvmCode
            .Create2(initCode, salt, 0).Done;

        TestState.CreateAccount(TestItem.AddressC, 1.Ether());
        TestState.InsertCode(TestItem.AddressC, createCode, Spec);

        byte[] code = Prepare.EvmCode
            .Call(TestItem.AddressC, 50000)
            .Done;

        GethLikeTxTrace prestateTrace = Execute(
                tracer,
                code,
                MainnetSpecProvider.CancunActivation,
                TxAddresses)
            .BuildResult();
        const string expectedPrestateTrace = """
{
  "0xb7705ae4c6f81b66cdb323c65f4e8133690fc099": {
    "balance": "0x56bc75e2d630e795f",
    "nonce": 1
  },
  "0x942921b14f1b1c385cd7e0cc2ef7abe5598c8358": {
    "balance": "0x56bc75e2d63100000",
    "code": "0x600060006000600060007376e68a8696537e4141926f3e528733af9e237d6961c350f1"
  },
  "0x475674cb523a0a2736b7f7534390288fce16982c": {
    "balance": "0x0"
  },
  "0x76e68a8696537e4141926f3e528733af9e237d69": {
    "balance": "0xde0b6b3a7640000",
    "code": "0x7f7f010203000000000000000000000000000000000000000000000000000000006000527f0060005260036000f3000000000000000000000000000000000000000000000060205262040506602960006000f5"
  },
  "0x02caaf71b895896a4d9159943eae74efb6a58238": {
    "balance": "0x0"
  }
}
""";
        Assert.That(JsonSerializer.Serialize(prestateTrace.CustomTracerResult?.Value, Options), Is.EqualTo(expectedPrestateTrace));
    }

    [Test]
    public void Test_PrestateTrace_ExistingAccount()
    {
        TestState.CreateAccount(TestItem.AddressC, 5.Ether());
        TestState.IncrementNonce(TestItem.AddressC);
        NativePrestateTracer tracer = new(TestState, GethTraceOptions.Default with { Tracer = NativePrestateTracer.PrestateTracer });

        GethLikeTxTrace prestateTrace = Execute(
                tracer,
                Balance(),
                MainnetSpecProvider.CancunActivation,
                TxAddresses)
            .BuildResult();
        const string expectedPrestateTrace = """
{
  "0xb7705ae4c6f81b66cdb323c65f4e8133690fc099": {
    "balance": "0x56bc75e2d630e795f",
    "nonce": 1
  },
  "0x942921b14f1b1c385cd7e0cc2ef7abe5598c8358": {
    "balance": "0x56bc75e2d63100000",
    "code": "0x7f00000000000000000000000076e68a8696537e4141926f3e528733af9e237d693100"
  },
  "0x475674cb523a0a2736b7f7534390288fce16982c": {
    "balance": "0x0"
  },
  "0x76e68a8696537e4141926f3e528733af9e237d69": {
    "balance": "0x4563918244f40000",
    "nonce": 1
  }
}
""";
        Assert.That(JsonSerializer.Serialize(prestateTrace.CustomTracerResult?.Value, Options), Is.EqualTo(expectedPrestateTrace));
    }

    [Test]
    public void Test_PrestateTrace_EmptyTo()
    {
        NativePrestateTracer tracer = new(TestState, GethTraceOptions.Default with { Tracer = NativePrestateTracer.PrestateTracer });
        SenderRecipientAndMiner txAddressesNoRecipient = new SenderRecipientAndMiner()
        {
            SenderKey = SenderKey,
            MinerKey = MinerKey
        };
        GethLikeTxTrace prestateTrace = Execute(
                tracer,
                Balance(),
                MainnetSpecProvider.CancunActivation,
                txAddressesNoRecipient)
            .BuildResult();
        const string expectedPrestateTrace = """
{
  "0xb7705ae4c6f81b66cdb323c65f4e8133690fc099": {
    "balance": "0x56bc75e2d630e795f",
    "nonce": 1
  },
  "0x942921b14f1b1c385cd7e0cc2ef7abe5598c8358": {
    "balance": "0x56bc75e2d63100000",
    "code": "0x7f00000000000000000000000076e68a8696537e4141926f3e528733af9e237d693100"
  },
  "0x475674cb523a0a2736b7f7534390288fce16982c": {
    "balance": "0x0"
  },
  "0x76e68a8696537e4141926f3e528733af9e237d69": {
    "balance": "0x0"
  }
}
""";
        Assert.That(JsonSerializer.Serialize(prestateTrace.CustomTracerResult?.Value, Options), Is.EqualTo(expectedPrestateTrace));
    }

    private static byte[] Balance()
    {
        return Prepare.EvmCode
            .PushData(TestItem.AddressC.ToString(false, false).PadLeft(64, '0'))
            .Op(Instruction.BALANCE)
            .Op(Instruction.STOP)
            .Done;
    }
}
