using System;
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Core;
using log4net.Layout;
using log4net.Repository;
using log4net.Repository.Hierarchy;
using NUnit.Framework;
using Vostok.Logging.Abstractions;
using ILog = Vostok.Logging.Abstractions.ILog;

namespace Vostok.Logging.Log4net.Tests
{
    [TestFixture]
    internal class Log4netLog_Tests
    {
        private StringBuilder outputBuilder;
        private StringWriter outputWriter;

        private MemoryAppender memoryAppender;
        private TextWriterAppender textAppender;
        private ILoggerRepository log4netRepository;
        private ILogger log4netLogger;
        private ILog adapter;

        private LoggingEvent ObservedEvent => memoryAppender.GetEvents().Last();

        private string Output => outputBuilder.ToString();

        [SetUp]
        public void TestSetup()
        {
            outputBuilder = new StringBuilder();
            outputWriter = new StringWriter(outputBuilder);

            memoryAppender = new MemoryAppender();
            textAppender = new TextWriterAppender {Writer = outputWriter, Layout = new PatternLayout("%m")};

            log4netRepository = LogManager.GetAllRepositories().SingleOrDefault(x => x.Name == "test") ?? LogManager.CreateRepository("test");
            log4netRepository.ResetConfiguration();

            BasicConfigurator.Configure(log4netRepository, memoryAppender, textAppender);

            log4netLogger = LogManager.GetLogger("test", "root").Logger;

            adapter = new Log4netLog(log4netLogger);
        }

        [Test]
        public void IsEnabledFor_should_return_true_for_enabled_levels()
        {
            SetRootLevel(Level.Verbose);

            adapter.IsEnabledFor(LogLevel.Debug).Should().BeTrue();
            adapter.IsEnabledFor(LogLevel.Info).Should().BeTrue();
            adapter.IsEnabledFor(LogLevel.Warn).Should().BeTrue();
            adapter.IsEnabledFor(LogLevel.Error).Should().BeTrue();
            adapter.IsEnabledFor(LogLevel.Fatal).Should().BeTrue();

            SetRootLevel(Level.Warn);

            adapter.IsEnabledFor(LogLevel.Warn).Should().BeTrue();
            adapter.IsEnabledFor(LogLevel.Error).Should().BeTrue();
            adapter.IsEnabledFor(LogLevel.Fatal).Should().BeTrue();
        }

        [Test]
        public void IsEnabledFor_should_return_false_for_disabled_levels()
        {
            SetRootLevel(Level.Fatal);

            adapter.IsEnabledFor(LogLevel.Debug).Should().BeFalse();
            adapter.IsEnabledFor(LogLevel.Info).Should().BeFalse();
            adapter.IsEnabledFor(LogLevel.Warn).Should().BeFalse();
            adapter.IsEnabledFor(LogLevel.Error).Should().BeFalse();

            SetRootLevel(Level.Warn);

            adapter.IsEnabledFor(LogLevel.Debug).Should().BeFalse();
            adapter.IsEnabledFor(LogLevel.Info).Should().BeFalse();
        }

        [Test]
        public void Log_method_should_support_null_events()
        {
            adapter.Log(null);

            Output.Should().BeEmpty();
        }

        [Test]
        public void Log_method_should_prerender_message()
        {
            adapter.Info("P1 = {0}, P2 = {1}", 1, 2);

            ObservedEvent.MessageObject.Should().Be(Output);
        }

        [Test]
        public void Log_method_should_support_syntax_with_index_based_parameters_in_template()
        {
            adapter.Info("P1 = {0}, P2 = {1}", 1, 2);

            Output.Should().Be("P1 = 1, P2 = 2");

            ObservedEvent.Properties["0"].Should().Be(1);
            ObservedEvent.Properties["1"].Should().Be(2);
        }

        [Test]
        public void Log_method_should_support_syntax_with_named_properties_in_anonymous_object()
        {
            adapter.Info("P1 = {Param1}, P2 = {Param2}", new { Param1 = 1, Param2 = 2 });

            Output.Should().Be("P1 = 1, P2 = 2");

            ObservedEvent.Properties["Param1"].Should().Be(1);
            ObservedEvent.Properties["Param2"].Should().Be(2);
        }

        [Test]
        public void Log_method_should_support_syntax_with_positional_properties_with_names_inferred_from_template()
        {
            adapter.Info("P1 = {Param1}, P2 = {Param2}", 1, 2);

            Output.Should().Be("P1 = 1, P2 = 2");

            ObservedEvent.Properties["Param1"].Should().Be(1);
            ObservedEvent.Properties["Param2"].Should().Be(2);
        }

        [Test]
        public void Log_method_should_support_syntax_without_any_properties()
        {
            adapter.Info("Hello!");

            Output.Should().Be("Hello!");
        }

        [Test]
        public void Log_method_should_render_trace_id()
        {
            adapter = new Log4netLog(log4netLogger, new Log4netLogSettings {UseVostokTemplate = true});

            adapter.WithProperty("traceContext", "guid").Info("Hello!");

            Output.Should().Be("guid Hello!");
        }

        [Test]
        public void Log_method_should_translate_all_properties_not_present_in_template()
        {
            var @event = new LogEvent(LogLevel.Info, DateTimeOffset.Now, null)
                .WithProperty("Param1", 1)
                .WithProperty("Param2", 2);

            adapter.Log(@event);

            ObservedEvent.Properties["Param1"].Should().Be(1);
            ObservedEvent.Properties["Param2"].Should().Be(2);
        }

        [Test]
        public void ForContext_with_non_null_argument_should_produce_a_wrapper_around_named_logger()
        {
            adapter = adapter.ForContext("CustomLogger");

            adapter.Info("Hello!");

            ObservedEvent.LoggerName.Should().Be("CustomLogger");
        }

        [Test]
        public void ForContext_should_support_accumulating_logger_name_with_a_chain_of_calls()
        {
            adapter = adapter
                .ForContext("CustomLogger1")
                .ForContext("CustomLogger2")
                .ForContext("CustomLogger3");

            adapter.Info("Hello!");

            ObservedEvent.LoggerName.Should().Be("CustomLogger1.CustomLogger2.CustomLogger3");
        }

        [Test]
        public void ForContext_should_preserve_configured_logger_name_factory()
        {
            ((Log4netLog)adapter).LoggerNameFactory = ctx => string.Join(".", ctx.Reverse());

            adapter = adapter
                .ForContext("ctx1")
                .ForContext("ctx2")
                .ForContext("ctx3");

            adapter.Info("Hello!");

            ObservedEvent.LoggerName.Should().Be("ctx3.ctx2.ctx1");
        }

        [Test]
        public void ForContext_with_non_null_argument_should_render_context()
        {
            adapter = new Log4netLog(log4netLogger, new Log4netLogSettings {UseVostokTemplate = true})
                .ForContext("CustomLogger");

            adapter.Info("Hello!");

            Output.Should().Be("[CustomLogger] Hello!");
        }

        [Test]
        public void ForContext_should_support_accumulating_context_with_a_chain_of_calls()
        {
            adapter = new Log4netLog(log4netLogger, new Log4netLogSettings {UseVostokTemplate = true})
                .ForContext("CustomLogger1")
                .ForContext("CustomLogger2")
                .ForContext("CustomLogger3");

            adapter.Info("Hello!");

            Output.Should().Be("[CustomLogger1 => CustomLogger2 => CustomLogger3] Hello!");
        }

        [Test]
        public void ForContext_should_ignore_configured_logger_name_factory_on_render()
        {
            ((Log4netLog)adapter).LoggerNameFactory = ctx => string.Join(".", ctx.Reverse());

            adapter = new Log4netLog(log4netLogger, new Log4netLogSettings {UseVostokTemplate = true})
                .ForContext("ctx1")
                .ForContext("ctx2")
                .ForContext("ctx3");

            adapter.Info("Hello!");

            Output.Should().Be("[ctx1 => ctx2 => ctx3] Hello!");
        }

        private void SetRootLevel(Level level)
        {
            var hierarchy = (Hierarchy) log4netRepository;

            hierarchy.Root.Level = level;
            hierarchy.RaiseConfigurationChanged(EventArgs.Empty);
        }
    }
}
