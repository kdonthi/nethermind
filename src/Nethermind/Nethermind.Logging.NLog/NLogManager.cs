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
using System.Collections.Concurrent;
using NLog;

namespace Nethermind.Logging.NLog
{
    public class NLogManager : ILogManager
    {
        private readonly string _logFileName;
        private readonly string _logDirectory;
        private readonly string _logConfig;

        public NLogManager(string logFileName, string logDirectory, string logConfig = null)
        {
            _logFileName = logFileName;
            _logDirectory = logDirectory;
            _logConfig = logConfig;
        }

        private ConcurrentDictionary<Type, NLogLogger> _loggers = new();

        private NLogLogger BuildLogger(Type type)
        {
            return new(type, _logFileName, _logDirectory, loggerConfig: _logConfig);
        }

        public ILogger GetClassLogger(Type type)
        {
            return _loggers.GetOrAdd(type, BuildLogger);
        }

        public ILogger GetClassLogger<T>()
        {
            return GetClassLogger(typeof(T));
        }

        public ILogger GetClassLogger()
        {
            return new NLogLogger(_logFileName, _logDirectory, loggerConfig: _logConfig);
        }

        public ILogger GetLogger(string loggerName)
        {
            return new NLogLogger(_logFileName, _logDirectory, loggerName, _logConfig);
        }

        public void SetGlobalVariable(string name, object value)
        {
            GlobalDiagnosticsContext.Set(name, value);
        }
    }
}
