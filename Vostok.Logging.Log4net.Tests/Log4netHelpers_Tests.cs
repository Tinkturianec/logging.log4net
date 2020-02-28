using System;
using FluentAssertions;
using FluentAssertions.Extensions;
using log4net.Core;
using log4net.Repository;
using NSubstitute;
using NUnit.Framework;
using Vostok.Logging.Abstractions;

namespace Vostok.Logging.Log4net.Tests
{
    [TestFixture]
    internal class Log4netHelpers_Tests
    {
        private LogEvent vostokEvent;
        private LoggingEvent log4netEvent;
        private ILogger log4netLogger;

        [SetUp]
        public void TestSetup()
        {
            vostokEvent = new LogEvent(LogLevel.Warn, DateTimeOffset.UtcNow - 5.Hours(), "Hello, {User}! You have {Count} messages.", new Exception("I failed."))
                .WithProperty("User", "Vostok")
                .WithProperty("Count", 150);

            log4netEvent = null;

            log4netLogger = Substitute.For<ILogger>();
            log4netLogger.Name.Returns("MyLogger");
            log4netLogger.Repository.Returns(Substitute.For<ILoggerRepository>());
        }

        [Test]
        public void TranslateLevel_should_correctly_translate_all_log_levels()
        {
            Log4netHelpers.TranslateLevel(LogLevel.Debug).Should().BeSameAs(Level.Debug);
            Log4netHelpers.TranslateLevel(LogLevel.Info).Should().BeSameAs(Level.Info);
            Log4netHelpers.TranslateLevel(LogLevel.Warn).Should().BeSameAs(Level.Warn);
            Log4netHelpers.TranslateLevel(LogLevel.Error).Should().BeSameAs(Level.Error);
            Log4netHelpers.TranslateLevel(LogLevel.Fatal).Should().BeSameAs(Level.Fatal);
        }

        [Test]
        public void TranslateEvent_should_correctly_translate_level()
        {
            log4netEvent = Log4netHelpers.TranslateEvent(log4netLogger, vostokEvent);

            log4netEvent.Level.Should().BeSameAs(Level.Warn);
        }

        [Test]
        public void TranslateEvent_should_prerender_message()
        {
            log4netEvent = Log4netHelpers.TranslateEvent(log4netLogger, vostokEvent);

            log4netEvent.MessageObject.Should().Be("Hello, Vostok! You have 150 messages.");
            log4netEvent.RenderedMessage.Should().Be("Hello, Vostok! You have 150 messages.");
        }

        [Test]
        public void TranslateEvent_should_not_prerender_message_with_trace_id()
        {
            log4netEvent = Log4netHelpers.TranslateEvent(log4netLogger, vostokEvent.WithProperty("traceContext", "guid"));

            log4netEvent.MessageObject.Should().Be("Hello, Vostok! You have 150 messages.");
            log4netEvent.RenderedMessage.Should().Be("Hello, Vostok! You have 150 messages.");
        }

        [Test]
        public void TranslateEvent_should_prerender_message_with_trace_id()
        {
            log4netEvent = Log4netHelpers.TranslateEvent(log4netLogger, vostokEvent.WithProperty("traceContext", "guid"), true);

            log4netEvent.MessageObject.Should().Be("guid Hello, Vostok! You have 150 messages.");
            log4netEvent.RenderedMessage.Should().Be("guid Hello, Vostok! You have 150 messages.");
        }

        [Test]
        public void TranslateEvent_should_keep_original_event_timestamp()
        {
            log4netEvent = Log4netHelpers.TranslateEvent(log4netLogger, vostokEvent);

            log4netEvent.TimeStampUtc.Should().Be(vostokEvent.Timestamp.UtcDateTime);
            log4netEvent.TimeStamp.Should().Be(vostokEvent.Timestamp.LocalDateTime);
        }

        [Test]
        public void TranslateEvent_should_set_correct_logger_name_for_log4net_event()
        {
            log4netEvent = Log4netHelpers.TranslateEvent(log4netLogger, vostokEvent);

            log4netEvent.LoggerName.Should().Be("MyLogger");
        }

        [Test]
        public void TranslateEvent_should_set_correct_logger_repository_for_log4net_event()
        {
            log4netEvent = Log4netHelpers.TranslateEvent(log4netLogger, vostokEvent);

            log4netEvent.Repository.Should().BeSameAs(log4netLogger.Repository);
        }

        [Test]
        public void TranslateEvent_should_populate_log4net_event_properties()
        {
            log4netEvent = Log4netHelpers.TranslateEvent(log4netLogger, vostokEvent);

            log4netEvent.Properties.Should().HaveCount(2);
            log4netEvent.Properties["User"].Should().Be("Vostok");
            log4netEvent.Properties["Count"].Should().Be(150);
        }

        [Test]
        public void TranslateEvent_should_not_fix_log4net_event()
        {
            log4netEvent = Log4netHelpers.TranslateEvent(log4netLogger, vostokEvent);

            log4netEvent.Fix.Should().Be(FixFlags.None);
        }

        [Test]
        public void TranslateEvent_should_set_log4net_event_exception()
        {
            log4netEvent = Log4netHelpers.TranslateEvent(log4netLogger, vostokEvent);

            log4netEvent.ExceptionObject.Should().BeSameAs(vostokEvent.Exception);
        }
    }
}