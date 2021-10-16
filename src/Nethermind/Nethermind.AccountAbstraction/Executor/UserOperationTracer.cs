//  Copyright (c) 2021 Demerzel Solutions Limited
//  This file is part of the Nethermind library.
// 
//  The Nethermind library is free software: you can redistribute it and/or modify
//  it under the terms of the GNU Lesser General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
// 
//  The Nethermind library is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//  GNU Lesser General Public License for more details.
// 
//  You should have received a copy of the GNU Lesser General Public License
//  along with the Nethermind. If not, see <http://www.gnu.org/licenses/>.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using Nethermind.Abi;
using Nethermind.AccountAbstraction.Data;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm;
using Nethermind.Evm.Tracing;
using Nethermind.Int256;
using Nethermind.State;

namespace Nethermind.AccountAbstraction.Executor
{
    public class UserOperationBlockTracer : IBlockTracer
    {
        private readonly long _gasLimit;
        private readonly Address _beneficiary;
        private readonly IStateProvider _stateProvider;
        private readonly AbiDefinition _abi;
        private readonly AbiEncoder _abiEncoder = new();


        private UserOperationTxTracer? _tracer;

        private UInt256? _beneficiaryBalanceBefore;
        private UInt256? _beneficiaryBalanceAfter;
        
        public UserOperationBlockTracer(long gasLimit, Address beneficiary, IStateProvider stateProvider, AbiDefinition abi)
        {
            _gasLimit = gasLimit;
            _beneficiary = beneficiary;
            _stateProvider = stateProvider;
            _abi = abi;
            AccessedStorage = new Dictionary<Address, HashSet<UInt256>>();
        }

        public bool Success { get; private set; } = true;
        public long GasUsed { get; private set; }
        public byte[] Output { get; private set; }
        public FailedOp? Error
        {
            get
            {
                try
                {
                    object[] decoded = _abiEncoder.Decode(AbiEncodingStyle.IncludeSignature,
                        _abi.Errors["FailedOp"].GetCallInfo().Signature, Output);
                    return new FailedOp()
                    {
                        OpIndex = (UInt256)decoded[0],
                        Paymaster = (Address)decoded[1],
                        Reason = (string)decoded[2]
                    };
                }
                catch (Exception e)
                {
                    return null;
                }
            }
        }

        public IDictionary<Address, HashSet<UInt256>> AccessedStorage { get; private set; }
        public bool IsTracingRewards => true;

        public void ReportReward(Address author, string rewardType, UInt256 rewardValue)
        {
        }

        public void StartNewBlockTrace(Block block)
        {
        }

        public ITxTracer StartNewTxTrace(Transaction? tx)
        {
            return tx is null
                ? new UserOperationTxTracer(_beneficiary, null, _stateProvider)
                : _tracer = new UserOperationTxTracer(_beneficiary, tx, _stateProvider);
        }

        public void EndTxTrace()
        {
            Output = _tracer.Output;

            if (!_tracer!.Success)
            {
                Success = false;
                return;
            }
            
            GasUsed += _tracer!.GasSpent;

            if (GasUsed > _gasLimit)
            {
                Success = false;
                return;
            }

            AccessedStorage = _tracer.AccessedStorage;
        }

        public void EndBlockTrace()
        {
        }
    }

    public class UserOperationTxTracer : ITxTracer
    {
        public UserOperationTxTracer(Address beneficiary, Transaction? transaction, IStateProvider stateProvider)
        {
            _beneficiary = beneficiary;
            Transaction = transaction;
            Success = true;
            AccessedStorage = new Dictionary<Address, HashSet<UInt256>>();
            _stateProvider = stateProvider;
        }

        public Transaction? Transaction { get; }
        public IDictionary<Address, HashSet<UInt256>> AccessedStorage { get; private set; }
        public bool Success { get; private set; }
        public string? Error { get; private set; }
        public long GasSpent { get; set; }
        public byte[] Output { get; private set; }
        public UInt256? BeneficiaryBalanceBefore { get; private set; }
        public UInt256? BeneficiaryBalanceAfter { get; private set; }

        private static readonly Instruction[] _bannedOpcodes = 
        {
            Instruction.GASPRICE,
            Instruction.GASLIMIT,
            Instruction.DIFFICULTY,
            Instruction.TIMESTAMP,
            Instruction.BASEFEE,
            Instruction.BLOCKHASH,
            Instruction.NUMBER,
            Instruction.SELFBALANCE,
            Instruction.BALANCE,
            Instruction.ORIGIN,
        };
        private readonly Address _beneficiary;
        private List<int> _codeAccessedAtDepth = new();
        private IStateProvider _stateProvider;
        


        public bool IsTracingReceipt => true;
        public bool IsTracingActions => false;
        public bool IsTracingOpLevelStorage => false;
        public bool IsTracingMemory => false;
        public bool IsTracingInstructions => true;
        public bool IsTracingRefunds => false;
        public bool IsTracingCode => false;
        public bool IsTracingStack => false;
        public bool IsTracingState => true;
        public bool IsTracingStorage => true;
        public bool IsTracingBlockHash => false;
        public bool IsTracingAccess => true;

        public void MarkAsSuccess(Address recipient, long gasSpent, byte[] output, LogEntry[] logs,
            Keccak? stateRoot = null)
        {
            GasSpent = gasSpent;
            Output = output;
        }

        public void MarkAsFailed(Address recipient, long gasSpent, byte[] output, string error,
            Keccak? stateRoot = null)
        {
            GasSpent = gasSpent;
            Success = false;
            Error = error;
            Output = output;
        }

        public void ReportBalanceChange(Address address, UInt256? before, UInt256? after)
        {
            if (address == _beneficiary)
            {
                BeneficiaryBalanceBefore ??= before;
                BeneficiaryBalanceAfter = after;
            }
        }

        public void ReportCodeChange(Address address, byte[]? before, byte[]? after)
        {
        }

        public void ReportNonceChange(Address address, UInt256? before, UInt256? after)
        {
        }

        public void ReportAccountRead(Address address)
        {
        }

        public void ReportStorageChange(StorageCell storageCell, byte[] before, byte[] after)
        {
            throw new NotImplementedException();
        }

        public void ReportStorageRead(StorageCell storageCell)
        {
            throw new NotImplementedException();
        }

        public void StartOperation(int depth, long gas, Instruction opcode, int pc)
        {
            if (depth > 1 && _bannedOpcodes.Contains(opcode))
            {
                Success = false;
            }

            if (depth > 2 && opcode == Instruction.CODECOPY)
            {
                _codeAccessedAtDepth.Add(depth);
            }
        }

        public void ReportOperationError(EvmExceptionType error)
        {
        }

        public void ReportOperationRemainingGas(long gas)
        {
        }

        public void SetOperationStack(List<string> stackTrace)
        {
            throw new NotImplementedException();
        }

        public void ReportStackPush(in ReadOnlySpan<byte> stackItem)
        {
        }

        public void SetOperationMemory(List<string> memoryTrace)
        {
            throw new NotImplementedException();
        }

        public void SetOperationMemorySize(ulong newSize)
        {
            throw new NotImplementedException();
        }

        public void ReportMemoryChange(long offset, in ReadOnlySpan<byte> data)
        {
        }

        public void ReportStorageChange(in ReadOnlySpan<byte> key, in ReadOnlySpan<byte> value)
        {
        }

        public void SetOperationStorage(Address address, UInt256 storageIndex, ReadOnlySpan<byte> newValue,
            ReadOnlySpan<byte> currentValue)
        {
            throw new NotImplementedException();
        }

        public void ReportSelfDestruct(Address address, UInt256 balance, Address refundAddress)
        {
            throw new NotImplementedException();
        }

        public void ReportAction(long gas, UInt256 value, Address @from, Address to, ReadOnlyMemory<byte> input,
            ExecutionType callType,
            bool isPrecompileCall = false)
        {
            throw new NotImplementedException();
        }

        public void ReportActionEnd(long gas, ReadOnlyMemory<byte> output)
        {
            throw new NotImplementedException();
        }

        public void ReportActionError(EvmExceptionType evmExceptionType)
        {
            throw new NotImplementedException();
        }

        public void ReportActionEnd(long gas, Address deploymentAddress, ReadOnlyMemory<byte> deployedCode)
        {
            throw new NotImplementedException();
        }

        public void ReportBlockHash(Keccak blockHash)
        {
            throw new NotImplementedException();
        }

        public void ReportByteCode(byte[] byteCode)
        {
            throw new NotImplementedException();
        }

        public void ReportGasUpdateForVmTrace(long refund, long gasAvailable)
        {
        }

        public void ReportRefund(long refund)
        {
            throw new NotImplementedException();
        }

        public void ReportExtraGasPressure(long extraGasPressure)
        {
            throw new NotImplementedException();
        }

        public void ReportAccess(IReadOnlySet<Address> accessedAddresses,
            IReadOnlySet<StorageCell> accessedStorageCells)
        {
            void AddToAccessedStorage(StorageCell storageCell)
            {
                if (AccessedStorage.ContainsKey(storageCell.Address))
                {
                    AccessedStorage[storageCell.Address].Add(storageCell.Index);
                    return;
                }
                AccessedStorage.Add(storageCell.Address, new HashSet<UInt256>{storageCell.Index});
            }

            bool ContainsSelfDestructOrDelegateCall(Address address)
            {
                // simple static analysis
                byte[] code = _stateProvider.GetCode(address);
                
                int i = 0;
                while (i < code.Length)
                {
                    byte currentInstruction = code[i];
                    
                    if (currentInstruction == (byte)Instruction.SELFDESTRUCT
                        || currentInstruction == (byte)Instruction.DELEGATECALL)
                    {
                        return true;
                    }

                    // push opcodes
                    else if (currentInstruction >= 0x60 || currentInstruction <= 0x7f)
                    {
                        i += currentInstruction - 0x5f;
                    }

                    i++;
                }

                return false;
            }

            Address[] accessedAddressesArray = accessedAddresses.ToArray();
            Address? walletOrPaymasterAddress = accessedAddressesArray.Length > 2 ? accessedAddressesArray[2] : null;
            if (walletOrPaymasterAddress is null)
            {
                Success = false;
                return;
            }

            List<Address>? furtherAddresses = accessedAddressesArray.Length > 3 ? accessedAddressesArray.Skip(3).ToList() : null;
            
            foreach (StorageCell accessedStorageCell in accessedStorageCells)
            {
                if (accessedStorageCell.Address == walletOrPaymasterAddress)
                {
                    AddToAccessedStorage(accessedStorageCell);
                }

                if (furtherAddresses is not null)
                {
                    if (furtherAddresses.Contains(accessedStorageCell.Address))
                    {
                        Success = false;
                    }
                }
            }


            foreach (int depth in _codeAccessedAtDepth)
            {
                Address codeAccessedAddress = accessedAddressesArray[depth];
                if (!_stateProvider.HasCode(codeAccessedAddress)
                    || ContainsSelfDestructOrDelegateCall(codeAccessedAddress))
                {
                    Success = false;
                }
            }
            
            
            
        }
    }
}