// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections;
using System.Linq;
using System.Runtime.Intrinsics;
using FluentAssertions;
using Nethermind.Evm.CodeAnalysis;
using NUnit.Framework;

namespace Nethermind.Evm.Test.CodeAnalysis
{
    [TestFixture]
    public class CodeInfoTests
    {
        [TestCase(-1, false)]
        [TestCase(0, true)]
        [TestCase(1, false)]
        public void Validates_when_only_jump_dest_present(int destination, bool isValid)
        {
            byte[] code =
            {
                (byte)Instruction.JUMPDEST
            };

            CodeInfo codeInfo = new(code);

            codeInfo.ValidateJump(destination).Should().Be(isValid);
        }

        [Test]
        public void Validates_when_push_with_data_like_jump_dest()
        {
            byte[] code =
            {
                (byte)Instruction.PUSH1,
                (byte)Instruction.JUMPDEST
            };

            CodeInfo codeInfo = new(code);

            codeInfo.ValidateJump(1).Should().BeFalse();
        }

        [Test]
        public void Validate_CodeBitmap_With_Push10()
        {
            byte[] code =
            {
                (byte)Instruction.PUSH10,
                1,2,3,4,5,6,7,8,9,10,
                (byte)Instruction.JUMPDEST
            };

            CodeInfo codeInfo = new(code);

            codeInfo.ValidateJump(11).Should().BeTrue();
        }

        [Test]
        public void Validate_CodeBitmap_With_Push30()
        {
            byte[] code =
            {
                (byte)Instruction.PUSH30,
                1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25,26,27,28,29,30,
                (byte)Instruction.JUMPDEST
            };

            CodeInfo codeInfo = new(code);

            codeInfo.ValidateJump(31).Should().BeTrue();
        }

        [Test]
        public void Small_Jumpdest()
        {
            byte[] code =
            {
                0x5b,0x5b,0x5b,0x5b,0x5b,0x5b,0x5b,0x5b,0x5b,0x5b,0x5b,0x5b,0x5b,0x5b,0x5b,0x5b,0x5b,0x5b,0x5b,0x5b
            };

            CodeInfo codeInfo = new(code);

            codeInfo.ValidateJump(10).Should().BeTrue();
        }

        [Test]
        public void Small_Push1()
        {
            byte[] code =
            {
                0x60,0x60,0x60,0x60,0x60,0x60,0x60,0x60,0x60,0x60,0x60,0x60,0x60,0x60,0x60,0x60,0x60,0x60,0x60,0x60,0x60,0x60,
            };

            CodeInfo codeInfo = new(code);

            codeInfo.ValidateJump(10).Should().BeFalse();
        }

        [Test]
        public void Jumpdest_Over10k()
        {
            var code = Enumerable.Repeat((byte)0x5b, 10_001).ToArray();

            CodeInfo codeInfo = new(code);

            codeInfo.ValidateJump(10).Should().BeTrue();
        }

        [Test]
        public void Push1_Over10k()
        {
            var code = Enumerable.Repeat((byte)0x60, 10_001).ToArray();

            CodeInfo codeInfo = new(code);

            codeInfo.ValidateJump(10).Should().BeFalse();
        }

        [Test]
        public void Push1Jumpdest_Over10k()
        {
            byte[] code = new byte[10_001];
            for (int i = 0; i < code.Length; i++)
            {
                code[i] = i % 2 == 0 ? (byte)0x60 : (byte)0x5b;
            }

            CodeInfo codeInfo = new(code);

            codeInfo.ValidateJump(10).Should().BeFalse();
            codeInfo.ValidateJump(11).Should().BeFalse(); // 0x5b but not JUMPDEST but data
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        [TestCase(11)]
        [TestCase(12)]
        [TestCase(13)]
        [TestCase(14)]
        [TestCase(15)]
        [TestCase(16)]
        [TestCase(17)]
        [TestCase(18)]
        [TestCase(19)]
        [TestCase(20)]
        [TestCase(21)]
        [TestCase(22)]
        [TestCase(23)]
        [TestCase(24)]
        [TestCase(25)]
        [TestCase(26)]
        [TestCase(27)]
        [TestCase(28)]
        [TestCase(29)]
        [TestCase(30)]
        [TestCase(31)]
        [TestCase(32)]
        public void PushNJumpdest_Over10k(int n)
        {
            byte[] code = new byte[10_001];

            // One vector (aligned), half vector to unalign
            int i;
            for (i = 0; i < Vector256<byte>.Count * 2 + Vector128<byte>.Count; i++)
            {
                code[i] = (byte)0x5b;
            }
            for (; i < Vector256<byte>.Count * 3; i++)
            {
                //
            }
            var triggerPushes = false;
            for (; i < code.Length; i++)
            {
                if (i % (n + 1) == 0)
                {
                    triggerPushes = true;
                }
                if (triggerPushes)
                {
                    code[i] = i % (n + 1) == 0 ? (byte)(0x60 + n - 1) : (byte)0x5b;
                }
            }

            CodeInfo codeInfo = new(code);

            for (i = 0; i < Vector256<byte>.Count * 2 + Vector128<byte>.Count; i++)
            {
                codeInfo.ValidateJump(i).Should().BeTrue();
            }
            for (; i < Vector256<byte>.Count * 3; i++)
            {
                codeInfo.ValidateJump(i).Should().BeFalse();
            }
            for (; i < code.Length; i++)
            {
                codeInfo.ValidateJump(i).Should().BeFalse(); // Are 0x5b but not JUMPDEST but data
            }
        }

        [TestCaseSource(nameof(Codes))]
        public void JumpDestinationAnalyzer_are_equivalent(byte[] codeInput)
        {
            for (int i = 1; i < codeInput.Length; i++)
            {
                ReadOnlySpan<byte> code = codeInput.AsSpan(0, i);

                long[] scalar = JumpDestinationAnalyzer.PopulateJumpDestinationBitmap_Scalar(JumpDestinationAnalyzer.CreateBitmap(code.Length), code);
                long[] vector512 = JumpDestinationAnalyzer.PopulateJumpDestinationBitmap_Vector512(JumpDestinationAnalyzer.CreateBitmap(code.Length), code);
                long[] vector128 = JumpDestinationAnalyzer.PopulateJumpDestinationBitmap_Vector128(JumpDestinationAnalyzer.CreateBitmap(code.Length), code);

                Assert.That(vector512, Is.EquivalentTo(scalar));
                Assert.That(vector128, Is.EquivalentTo(scalar));
            }
        }

        private static IEnumerable Codes
        {
            get
            {
                byte[] code = new byte[1024];
                TestCaseData test = new TestCaseData(code);
                test.TestName = "Code_All_0x00";
                yield return test;

                code = new byte[1024];
                code.AsSpan().Fill((byte)0x5b);
                test = new TestCaseData(code);
                test.TestName = "Code_All_JUMPDEST";
                yield return test;

                code = new byte[1024];

                for (var start = 0; start <= 1; start++)
                {
                    for (var push = 0x60; push <= 0x7f; push++)
                    {
                        for (int i = 0; i < code.Length; i++)
                        {
                            code[i] = (i + start) % 2 == 0 ? (byte)push : (byte)0x5b;
                        }
                        test = new TestCaseData(code);
                        test.TestName = start == 0 ?
                            $"Code_All_PUSH{push - 0x5f:00}JUMPDEST" :
                            $"Code_All_JUMPDESTPUSH{push - 0x5f:00}";
                        yield return test;
                    }
                }
            }
        }
    }
}
