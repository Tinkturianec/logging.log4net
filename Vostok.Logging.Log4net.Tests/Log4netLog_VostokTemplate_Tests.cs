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
using NUnit.Framework;
using Vostok.Context;
using Vostok.Logging.Abstractions;
using Vostok.Logging.Abstractions.Values;
using Vostok.Logging.Context;
using Vostok.Logging.Tracing;
using Vostok.Tracing;
using Vostok.Tracing.Abstractions;
using ILog = Vostok.Logging.Abstractions.ILog;

namespace Vostok.Logging.Log4net.Tests
{
    [TestFixture]
    public class Log4netLog_VostokTemplate_Tests
    {
        private StringBuilder outputBuilder;
        private StringWriter outputWriter;

        private TextWriterAppender textAppender;
        private ILoggerRepository log4netRepository;
        private ILogger log4netLogger;
        private ILog adapter;

        private string Output => outputBuilder.ToString();

        [SetUp]
        public void TestSetup()
        {
            outputBuilder = new StringBuilder();
            outputWriter = new StringWriter(outputBuilder);

            textAppender = new TextWriterAppender {Writer = outputWriter, Layout = new PatternLayout("%m")};

            log4netRepository = LogManager.GetAllRepositories().SingleOrDefault(x => x.Name == "test") ?? LogManager.CreateRepository("test");
            log4netRepository.ResetConfiguration();

            BasicConfigurator.Configure(log4netRepository, textAppender);

            log4netLogger = LogManager.GetLogger("test", "root").Logger;

            adapter = new Log4netLog(log4netLogger, new Log4netLogSettings {UseVostokTemplate = true});

            FlowingContext.Globals.Set(null as TraceContext);
        }

        [Test]
        public void Log_method_should_render_trace_id()
        {
            adapter.WithProperty("traceContext", "guid").Info("Hello!");
            Output.Should().Be("guid Hello!");
        }

        [Test]
        public void Log_method_should_render_source_context()
        {
            adapter.WithProperty("sourceContext", new SourceContextValue("guid")).Info("Hello!");
            Output.Should().Be("[guid] Hello!");
        }

        [Test]
        public void Log_method_should_render_operation_context()
        {
            adapter.WithProperty("operationContext", new OperationContextValue("guid")).Info("Hello!");
            Output.Should().Be("[guid] Hello!");
        }

        [Test]
        public void TraceId_should_be_rendered()
        {
            var traceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var spanId = Guid.Parse("22222222-2222-2222-2222-222222222222");
            var context = new TraceContext(traceId, spanId);
            var tracer = new Tracer(new TracerSettings(new DevNullSpanSender()))
            {
                CurrentContext = context
            };

            adapter = adapter.WithTracingProperties(tracer);

            adapter.Info("Hello!");

            Output.Should().Be($"[T-{traceId:N}] Hello!");
        }

        [Test]
        public void ForContext_with_non_null_argument_should_render_context()
        {
            adapter.ForContext("CustomLogger").Info("Hello!");

            Output.Should().Be("[CustomLogger] Hello!");
        }

        [Test]
        public void ForContext_should_support_accumulating_context_with_a_chain_of_calls()
        {
            adapter = adapter
                .ForContext("ctx1")
                .ForContext("ctx2")
                .ForContext("ctx3");

            adapter.Info("Hello!");

            Output.Should().Be("[ctx1 => ctx2 => ctx3] Hello!");
        }

        [Test]
        public void ForContext_should_ignore_configured_logger_name_factory_on_render()
        {
            ((Log4netLog)adapter).LoggerNameFactory = ctx => string.Join(".", ctx.Reverse());

            adapter = adapter
                .ForContext("ctx1")
                .ForContext("ctx2")
                .ForContext("ctx3");

            adapter.Info("Hello!");

            Output.Should().Be("[ctx1 => ctx2 => ctx3] Hello!");
        }

        [Test]
        public void Operation_should_be_rendered()
        {
            using (new OperationContextToken("op1"))
            {
                adapter.WithOperationContext().Info("Hello!");
            }

            Output.Should().Be("[op1] Hello!");
        }

        [Test]
        public void Operation_should_support_accumulating_with_a_chain_of_calls()
        {
            using (new OperationContextToken("op1"))
            {
                using (new OperationContextToken("op2"))
                {
                    adapter.WithOperationContext().Info("Hello!");
                }
            }

            Output.Should().Be("[op1] [op2] Hello!");
        }
    }
}
