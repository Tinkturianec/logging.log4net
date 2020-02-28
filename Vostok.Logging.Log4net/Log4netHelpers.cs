using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using JetBrains.Annotations;
using log4net.Core;
using log4net.Repository.Hierarchy;
using log4net.Util;
using Vostok.Logging.Abstractions;
using Vostok.Logging.Formatting;

namespace Vostok.Logging.Log4net
{
    internal static class Log4netHelpers
    {
        public static readonly OutputTemplate Template = OutputTemplate.Parse(
            $"{{{WellKnownProperties.TraceContext}:w}}" +
            $"{{{WellKnownProperties.OperationContext}:w}}" +
            $"{{{WellKnownProperties.SourceContext}:w}}" +
            $"{{{WellKnownTokens.Message}}}");

        [CanBeNull]
        private static readonly Action<LoggingEvent, DateTime> timestampSetter;

        static Log4netHelpers()
        {
            timestampSetter = CompileTimestampSetter();
        }

        [NotNull]
        public static LoggingEvent TranslateEvent([NotNull] ILogger logger, [NotNull] LogEvent @event, bool useVostokTemplate = false)
        {
            var level = TranslateLevel(@event.Level);
            var message = useVostokTemplate
                ? LogEventFormatter.Format(@event, Template)
                : LogMessageFormatter.Format(@event);
            var timestamp = @event.Timestamp.UtcDateTime;

            var loggingEvent = new LoggingEvent(typeof(Logger), logger.Repository, logger.Name, level, message, @event.Exception);

            FillProperties(loggingEvent, @event.Properties);

            // (iloktionov): Unfortunately, log4net's LoggingEvent does not have a constructor that allows to pass both structured exception and timestamp.
            // (iloktionov): Constructor with Exception parameter just uses DateTime.UtcNow for timestamp.
            // (iloktionov): Constructor with LoggingEventData only allows to pass exception string instead of exception object.
            // (iloktionov): So we take the first ctor and use some dirty expressions to set timestamp in private LoggingEventData instance.

            timestampSetter?.Invoke(loggingEvent, timestamp);

            return loggingEvent;
        }

        [NotNull]
        public static Level TranslateLevel(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    return Level.Debug;

                case LogLevel.Info:
                    return Level.Info;

                case LogLevel.Warn:
                    return Level.Warn;

                case LogLevel.Error:
                    return Level.Error;

                case LogLevel.Fatal:
                    return Level.Fatal;

                default:
                    throw new ArgumentOutOfRangeException(nameof(level), level, $"Unexpected value of {nameof(LogLevel)}.");
            }
        }

        private static void FillProperties([NotNull] LoggingEvent log4netEvent, [CanBeNull] IReadOnlyDictionary<string, object> properties)
        {
            if (properties == null || properties.Count == 0)
                return;

            foreach (var pair in properties)
            {
                log4netEvent.Properties[pair.Key] = pair.Value;
            }
        }

        [CanBeNull]
        private static Action<LoggingEvent, DateTime> CompileTimestampSetter()
        {
            try
            {
                var dataFieldInfo = typeof(LoggingEvent).GetField("m_data", BindingFlags.Instance | BindingFlags.NonPublic);
                if (dataFieldInfo == null)
                    return null;

                var timestampPropertyInfo = typeof(LoggingEventData).GetProperty(nameof(LoggingEventData.TimeStampUtc), BindingFlags.Instance | BindingFlags.Public);
                if (timestampPropertyInfo == null)
                    return null;

                var timestampPropertySetter = timestampPropertyInfo.GetSetMethod();
                if (timestampPropertySetter == null)
                    return null;

                var eventParameter = Expression.Parameter(typeof(LoggingEvent));
                var timestampParameter = Expression.Parameter(typeof(DateTime));

                var dataFieldAccess = Expression.MakeMemberAccess(eventParameter, dataFieldInfo);
                var setterExpression = Expression.Call(dataFieldAccess, timestampPropertySetter, timestampParameter);

                return Expression.Lambda<Action<LoggingEvent, DateTime>>(setterExpression, eventParameter, timestampParameter).Compile();
            }
            catch (Exception error)
            {
                LogLog.Error(typeof(Log4netHelpers), $"Failed to compile timestamp setter delegate for log4net's {typeof(LoggingEvent).Name}.", error);

                return null;
            }
        }
    }
}
