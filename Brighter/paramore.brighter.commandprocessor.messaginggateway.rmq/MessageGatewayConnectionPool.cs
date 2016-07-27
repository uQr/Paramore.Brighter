// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor.messaginggateway.rmq
// Author           : AnthonyP
// Created          : 08-04-2015
//
// Last Modified By : 
// Last Modified On : 
// ***********************************************************************
// <copyright file="MessageGatewayConnectionPool.cs" company="">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

#region Licence
/* The MIT License (MIT)
Copyright © 2015 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System.Collections.Generic;
using paramore.brighter.commandprocessor.Logging;
using RabbitMQ.Client;

namespace paramore.brighter.commandprocessor.messaginggateway.rmq
{
    using System;

    /// <summary>
    /// Class MessageGatewayConnectionPool.
    /// </summary>
    public class MessageGatewayConnectionPool
    {
        private static readonly Dictionary<string, MessageGatewayConnectionPoolConnection> s_connectionPool = new Dictionary<string, MessageGatewayConnectionPoolConnection>();
        private static readonly object s_lock = new object();
        private static readonly ILog s_logger = LogProvider.GetCurrentClassLogger();

        /// <summary>
        /// Return matching RabbitMQ connection if exist (match by amqp scheme)
        /// or create new connection to RabbitMQ (thread-safe)
        /// </summary>
        /// <param name="connectionFactory"></param>
        /// <returns></returns>
        public IConnection GetConnection(ConnectionFactory connectionFactory)
        {
            lock (s_lock)
            {
                var connectionId = GetConnectionId(connectionFactory);

                s_logger.DebugFormat("RMQMessagingGateway: Acquiring leased connection to Rabbit MQ endpoint {0}", connectionFactory.Endpoint);

                MessageGatewayConnectionPoolConnection leasedConnection;

                var leasedConnectionFound = s_connectionPool.TryGetValue(connectionId, out leasedConnection);

                if (leasedConnectionFound && leasedConnection != null && leasedConnection.IsOpen())
                {
                    s_logger.DebugFormat("RMQMessagingGateway: Renewing leased connection to Rabbit MQ endpoint {0}", connectionFactory.Endpoint);

                    leasedConnection.RenewLease();

                    return leasedConnection.RmqConnection;
                }

                s_logger.DebugFormat("RMQMessagingGateway: Creating leased connection to Rabbit MQ endpoint {0}", connectionFactory.Endpoint);

                var newLeasedConnection = new MessageGatewayConnectionPoolConnection(connectionId, connectionFactory);

                if (leasedConnectionFound && (leasedConnection == null || !leasedConnection.IsOpen()))
                {
                    s_connectionPool[connectionId] = newLeasedConnection;
                }
                else
                {
                    s_connectionPool.Add(connectionId, newLeasedConnection);
                }

                return newLeasedConnection.RmqConnection;
            }
        }

        public static void ShutdownAllConnections()
        {
            s_logger.Info("Shutting down connection pool and all connections to Rabbit MQ endpoints");

            foreach (var connectionKey in s_connectionPool.Keys)
            {
                if (s_connectionPool[connectionKey] != null && s_connectionPool[connectionKey].IsOpen())
                {
                    s_connectionPool[connectionKey].Close();
                }
            }
        }

        private static void TryRemoveConnection(string connectionId, string uniqueIdentifier, IConnectionFactory connectionFactory)
        {
            lock (s_lock)
            {
                if (s_connectionPool.ContainsKey(connectionId))
                {
                    MessageGatewayConnectionPoolConnection connectionData;

                    var connectionFound = s_connectionPool.TryGetValue(connectionId, out connectionData);

                    if (connectionFound && connectionData.LeaseIdentifier == uniqueIdentifier)
                    {
                        s_connectionPool[connectionId] = null;
                    }
                    else if (connectionFound)
                    {
                        s_logger.ErrorFormat("RMQMessagingGateway: Unable to remove connection as unique identifier is different indicating connection renewed.");
                        s_logger.WarnFormat("RMQMessagingGateway: Recovering connection.");

                        connectionData.RecoverConnection(connectionFactory);
                    }
                }
            }
        }

        private string GetConnectionId(ConnectionFactory connectionFactory)
        {
            return string.Concat(connectionFactory.UserName, ".", connectionFactory.Password, ".", connectionFactory.HostName, ".", connectionFactory.Port, ".", connectionFactory.VirtualHost).ToLowerInvariant();
        }

        private class MessageGatewayConnectionPoolConnection : IDisposable
        {
            private string Id { get; }

            public IConnection RmqConnection { get; private set; }

            public string LeaseIdentifier { get; private set; }

            public MessageGatewayConnectionPoolConnection(string id, IConnectionFactory rmqConnectionFactory)
            {
                this.Id = id;
                this.LeaseIdentifier = Guid.NewGuid().ToString();
                this.RmqConnection = CreateRmqConnection(this.Id, rmqConnectionFactory);
            }

            public void RecoverConnection(IConnectionFactory rmqConnectionFactory)
            {
                if (this.RmqConnection == null || !this.RmqConnection.IsOpen)
                {
                    this.RmqConnection = CreateRmqConnection(this.Id, rmqConnectionFactory);
                }
            }

            public void RenewLease()
            {
                this.LeaseIdentifier = Guid.NewGuid().ToString();
            }

            public bool IsOpen()
            {
                return this.RmqConnection != null && this.RmqConnection.IsOpen;
            }

            public void Close()
            {
                this.RmqConnection.Close();
            }

            public void Dispose()
            {
                this.RmqConnection.Close();
                this.RmqConnection = null;
            }

            private IConnection CreateRmqConnection(string id, IConnectionFactory rmqConnectionFactory)
            {
                var newConnection = rmqConnectionFactory.CreateConnection();
                newConnection.ConnectionShutdown += delegate { TryRemoveConnection(id, this.LeaseIdentifier, rmqConnectionFactory); };
                return newConnection;
            }
        }
    }
}