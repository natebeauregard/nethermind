// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using Nethermind.Core;
using Nethermind.Int256;
using System.Numerics;
using System.Runtime.InteropServices;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;

// ReSharper disable InconsistentNaming

namespace Nethermind.Evm.Tracing.GethStyle.Custom.Native
{
    public class Log
    {
        public Instruction? op { get; set; }
        public Stack stack { get; set; }
        public Memory memory { get; set; } = new();
        public Contract contract { get; set; }
        public long pc { get; set; }

        public long gas { get; set; }
        public long? gasCost { get; set; }
        public int depth { get; set; }
        public long refund { get; set; }
        public string? error { get; set; }
        public ulong getGas() => (ulong)gas;
        public ulong getCost() => (ulong)(gasCost ?? 0);

        public readonly struct Stack
        {
            private readonly TraceStack _items;
            public Stack(TraceStack items) => _items = items;
            public ReadOnlySpan<byte> peek(int index) => _items[^(index + 1)].Span;
        }

        public class Memory
        {
            public TraceMemory MemoryTrace;

            public int length() => (int)MemoryTrace.Size;

            public ReadOnlySpan<byte> slice(long start, long end)
            {
                if (start < 0 || end < start || end > Array.MaxLength)
                {
                    throw new ArgumentOutOfRangeException(nameof(start), $"tracer accessed out of bound memory: offset {start}, end {end}");
                }

                int length = (int)(end - start);
                return MemoryTrace.Slice((int)start, length);
            }

            public IJavaScriptObject getUint(int offset) => MemoryTrace.GetUint(offset).ToBigInteger();
        }

        public struct Contract
        {
            public Address Address { get; }
            public Address Caller { get; }


            public Contract(Address caller, Address address, UInt256 value, ReadOnlyMemory<byte>? input)
            {
                Caller = caller;
                Address = address;
            }
        }
    }
}
