﻿#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using paramore.brighter.commandprocessor.messaginggateway.rmq;
using paramore.brighter.commandprocessor.messaginggateway.rmq.MessagingGatewayConfiguration;
using paramore.brighter.commandprocessor.tests.nunit.CommandProcessors.TestDoubles;
using paramore.brighter.commandprocessor.tests.nunit.MessageDispatch.TestDoubles;
using paramore.brighter.serviceactivator;
using Polly;
using TinyIoC;
using Connection = paramore.brighter.serviceactivator.Connection;

namespace paramore.brighter.commandprocessor.tests.nunit.MessageDispatch
{
    [TestFixture]
    public class DispatchBuilderTests
    {
        private IAmADispatchBuilder _builder;
        private Dispatcher _dispatcher;

        [SetUp]
        public void Establish()
        {
            var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(() => new MyEventMessageMapper()));
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

            var retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetry(new[]
                {
                    TimeSpan.FromMilliseconds(50),
                    TimeSpan.FromMilliseconds(100),
                    TimeSpan.FromMilliseconds(150)
                });

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreaker(1, TimeSpan.FromMilliseconds(500));

            var rmqConnection = new RmqMessagingGatewayConnection
            {
                AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
                Exchange = new Exchange("paramore.brighter.exchange")
            };

            var rmqMessageConsumerFactory = new RmqMessageConsumerFactory(rmqConnection);
            var rmqMessageProducerFactory = new RmqMessageProducerFactory(rmqConnection);

            var connections = new List<Connection>
            {
                new Connection(
                    new ConnectionName("foo"),
                    new InputChannelFactory(rmqMessageConsumerFactory, rmqMessageProducerFactory),
                    typeof(MyEvent),
                    new ChannelName("mary"),
                    "bob",
                    timeoutInMilliseconds: 200),
                new Connection(
                    new ConnectionName("bar"),
                    new InputChannelFactory(rmqMessageConsumerFactory, rmqMessageProducerFactory),
                    typeof(MyEvent),
                    new ChannelName("alice"),
                    "simon",
                    timeoutInMilliseconds: 200)
            };

            _builder = DispatchBuilder.With()
                .CommandProcessor(CommandProcessorBuilder.With()
                        .Handlers(new HandlerConfiguration(new SubscriberRegistry(),
                            new TinyIocHandlerFactory(new TinyIoCContainer())))
                        .Policies(new PolicyRegistry()
                        {
                            {CommandProcessor.RETRYPOLICY, retryPolicy},
                            {CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy}
                        })
                        .NoTaskQueues()
                        .RequestContextFactory(new InMemoryRequestContextFactory())
                        .Build()
                )
                .MessageMappers(messageMapperRegistry)
                .ChannelFactory(new InputChannelFactory(rmqMessageConsumerFactory, rmqMessageProducerFactory))
                .Connections(connections);
        }

        [Test]
        public void When_Building_A_Dispatcher()
        {
            _dispatcher = _builder.Build();

            //_should_build_a_dispatcher
            Assert.NotNull(_dispatcher);
            //_should_have_a_foo_connection
            Assert.NotNull(GetConnection("foo"));
            //_should_have_a_bar_connection
            Assert.NotNull(GetConnection("bar"));
            //_should_be_in_the_awaiting_state
            Assert.AreEqual(DispatcherState.DS_AWAITING, _dispatcher.State);
        }

        private Connection GetConnection(string name)
        {
            return _dispatcher.Connections.SingleOrDefault(conn => conn.Name == name);
        }
    }
}
