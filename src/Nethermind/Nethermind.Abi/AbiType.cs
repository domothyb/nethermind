﻿//  Copyright (c) 2021 Demerzel Solutions Limited
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

using System;
using System.Collections.Generic;
using System.Linq;
using Nethermind.Core.Extensions;

namespace Nethermind.Abi
{
    public abstract class AbiType
    {
        public static AbiDynamicBytes DynamicBytes { get; } = AbiDynamicBytes.Instance;
        public static AbiAddress Address { get; } = AbiAddress.Instance;
        public static AbiFunction Function { get; } = AbiFunction.Instance;
        public static AbiBool Bool { get; } = AbiBool.Instance;
        public static AbiInt Int8 { get; } = new(8);
        public static AbiInt Int16 { get; } = new(16);
        public static AbiInt Int32 { get; } = new(32);
        public static AbiInt Int64 { get; } = new(64);
        public static AbiInt Int96 { get; } = new(96);
        public static AbiInt Int256 { get; } = new(256);
        
        public static AbiUInt UInt8 { get; } = new(8);
        public static AbiUInt UInt16 { get; } = new(16);
        public static AbiUInt UInt32 { get; } = new(32);
        public static AbiUInt UInt64 { get; } = new(64);
        public static AbiUInt UInt96 { get; } = new(96);
        public static AbiUInt UInt256 { get; } = new(256);
        public static AbiString String { get; } = AbiString.Instance;
        public static AbiFixed Fixed { get; } = new(128, 18);
        public static AbiUFixed UFixed { get; } = new(128, 18);

        public virtual bool IsDynamic => false;

        public abstract string Name { get; }

        public abstract (object, int) Decode(byte[] data, int position, bool packed);

        public abstract byte[] Encode(object? arg, bool packed);

        public override string ToString() => Name;

        public override int GetHashCode() => Name.GetHashCode();

        public override bool Equals(object? obj) => obj is AbiType type && Name == type.Name;

        protected string AbiEncodingExceptionMessage => $"Argument cannot be encoded by {GetType().Name}";

        public abstract Type CSharpType { get; }

        private static readonly IDictionary<Type, AbiType> _typeMappings = new Dictionary<Type, AbiType>();
        
        public static AbiType GetForCSharpType(Type type)
        {
            if (type.IsArray)
            {
                Type elementType = type.GetElementType()!;
                return new AbiArray(GetForCSharpType(elementType));
            }
            else if (type.IsValueTuple())
            {
                Type[] subTypes = type.GetGenericArguments();
                Dictionary<string, AbiType> elements = new();
                for (int i = 0; i < subTypes.Length; i++)
                {
                    elements[$"item{i}"] = GetForCSharpType(subTypes[i]);
                }

                return new AbiTuple(elements);
            }
            
            return _typeMappings[type];
        }

        protected static void RegisterMapping<T>(AbiType abiType)
        {
            _typeMappings[typeof(T)] = abiType;
        }
    }
}
