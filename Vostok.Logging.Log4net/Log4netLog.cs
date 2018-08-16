﻿using System;
using JetBrains.Annotations;
using log4net.Core;
using log4net.Repository.Hierarchy;
using Vostok.Logging.Abstractions;
using ILog = Vostok.Logging.Abstractions.ILog;

namespace Vostok.Logging.Log4net
{
    // TODO(iloktionov): 1. xml-docs
    // TODO(iloktionov): 2. better unit test coverage (ForContext)
    // TODO(iloktionov): 3. do something about global properties (log4net:HostName, log4net:UserName, log4net:Identity)

    public class Log4netLog : ILog
    {
        private readonly ILogger logger;

        public Log4netLog([NotNull] log4net.ILog log)
            : this(log.Logger)
        {
        }

        public Log4netLog([NotNull] ILogger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Log(LogEvent @event)
        {
            if (@event == null)
                return;

            if (!IsEnabledFor(@event.Level))
                return;

            logger.Log(Log4netHelpers.TranslateEvent(logger, @event));
        }

        public bool IsEnabledFor(LogLevel level)
        {
            return logger.IsEnabledFor(Log4netHelpers.TranslateLevel(level));
        }

        public ILog ForContext(string context)
        {
            ILogger newLogger;

            if (context == null)
            {
                newLogger = (logger.Repository as Hierarchy)?.Root ?? logger;
            }
            else
            {
                newLogger = logger.Repository.GetLogger(context);
            }

            if (newLogger.Name == context)
                return this;

            return new Log4netLog(newLogger);
        }
    }
}