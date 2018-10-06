﻿/*
 * Copyright (c) 2018 Demerzel Solutions Limited
 * This file is part of the Nethermind library.
 *
 * The Nethermind library is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * The Nethermind library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with the Nethermind. If not, see <http://www.gnu.org/licenses/>.
 */

namespace Nethermind.Store
{
    public class ReadOnlyDb : IDb
    {
        private IDb _memDb = new MemDb();
        
        private readonly IDb _wrappedDb;

        public ReadOnlyDb(IDb wrappedDb)
        {
            _wrappedDb = wrappedDb;
        }
        
        public void Dispose()
        {
            _wrappedDb.Dispose(); // this is not a right approach but all right for the current use cases
        }

        public byte[] this[byte[] key]
        {
            get
            {
                
                return _memDb[key] ?? _wrappedDb[key];
            }
            set => _memDb[key] = value;
        }

        public void StartBatch()
        {
        }

        public void CommitBatch()
        {
        }
    }
}