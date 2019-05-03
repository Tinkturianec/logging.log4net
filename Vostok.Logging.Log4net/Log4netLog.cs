using System;
using JetBrains.Annotations;
using log4net.Core;
using log4net.Repository;
using Vostok.Logging.Abstractions;
using Vostok.Logging.Abstractions.Values;

namespace Vostok.Logging.Log4net
{
    /// <summary>
    /// <para>Represents an adapter between Vostok logging interfaces and log4net.</para>
    /// <para>It implements Vostok <see cref="ILog"/> interface using an externally provided instance of log4net <see cref="ILogger"/>.</para>
    /// <para>It does this by following these rules:</para>
    /// <list type="number">
    ///     <item><description>Vostok <see cref="LogLevel"/>s are directly translated to log4net <see cref="Level"/>s.<para/></description></item>
    ///     <item><description>Messages are prerendered into text as Vostok <see cref="ILog"/>'s formatting syntax differs from log4net.<para/></description></item>
    ///     <item><description>Properties are forwarded into log4net event's <see cref="LoggingEvent.Properties"/>.<para/></description></item>
    ///     <item><description><see cref="ForContext"/> returns a <see cref="Log4netLog"/> with source context augmented with given value and underlying logger obtained from current logger's repository by name obtained by calling <see cref="LoggerNameFactory"/> on new source context.<para/></description></item>
    /// </list>
    /// </summary>
    public class Log4netLog : ILog
    {
        private readonly ILogger logger;
        private readonly SourceContextValue sourceContext;

        public Log4netLog([NotNull] log4net.ILog log)
            : this(log.Logger)
        {
        }

        public Log4netLog([NotNull] ILogger logger)
            : this(logger, IsTrivialLoggerName(logger.Name) ? null : new SourceContextValue(logger.Name))
        {
        }

        private Log4netLog([NotNull] ILogger logger, SourceContextValue sourceContext)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.sourceContext = sourceContext;
        }

        /// <summary>
        /// <para>Gets or sets the factory used to obtain logger names from <see cref="SourceContextValue"/>s created with <see cref="ForContext"/> calls.</para>
        /// <para>Default factory joins source context parts using a dot as separator to obtain logger name.</para>
        /// </summary>
        [NotNull]
        public Func<SourceContextValue, string> LoggerNameFactory { get; set; }
            = ctx => string.Join(".", ctx);

        /// <inheritdoc />
        public void Log(LogEvent @event)
        {
            if (@event == null)
                return;

            if (!IsEnabledFor(@event.Level))
                return;

            logger.Log(Log4netHelpers.TranslateEvent(logger, @event));
        }

        /// <inheritdoc />
        public bool IsEnabledFor(LogLevel level)
        {
            return logger.IsEnabledFor(Log4netHelpers.TranslateLevel(level));
        }

        /// <inheritdoc />
        public ILog ForContext(string context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var newSourceContext = sourceContext + context;

            if (ReferenceEquals(newSourceContext, sourceContext))
                return this;

            var newLogger = logger.Repository.GetLogger(LoggerNameFactory(newSourceContext));
            if (newLogger.Name == logger.Name)
                return this;

            return new Log4netLog(newLogger, newSourceContext)
            {
                LoggerNameFactory = LoggerNameFactory
            };
        }

        private static bool IsTrivialLoggerName([CanBeNull] string loggerName)
            => string.IsNullOrEmpty(loggerName) || loggerName == "root";
    }
}
