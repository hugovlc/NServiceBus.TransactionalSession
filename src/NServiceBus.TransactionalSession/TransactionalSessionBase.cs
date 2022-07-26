﻿namespace NServiceBus.TransactionalSession
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Persistence;
    using Transport;

    abstract class TransactionalSessionBase : ITransactionalSession
    {
        protected TransactionalSessionBase(
            CompletableSynchronizedStorageSession synchronizedStorageSession,
            IMessageSession messageSession,
            IDispatchMessages dispatcher)
        {
            this.synchronizedStorageSession = synchronizedStorageSession;
            this.messageSession = messageSession;
            this.dispatcher = dispatcher;
            pendingOperations = new PendingTransportOperations();
            transportTransaction = new TransportTransaction();
        }

        public SynchronizedStorageSession SynchronizedStorageSession
        {
            get
            {
                if (!IsOpen)
                {
                    throw new InvalidOperationException(
                        "Before accessing the SynchronizedStorageSession, make sure to open the session by calling the `Open`-method.");
                }

                return synchronizedStorageSession;
            }
        }

        public string SessionId { get; private set; }

        protected ContextBag Context => options.Extensions;

        protected bool IsOpen => options != null;

        public abstract Task Commit(CancellationToken cancellationToken = default);

        public virtual Task Open(OpenSessionOptions options = null, CancellationToken cancellationToken = default)
        {
            this.options = options ?? new OpenSessionOptions();
            SessionId = Guid.NewGuid().ToString();
            return Task.CompletedTask;
        }

#pragma warning disable PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext
        public async Task Send(object message, SendOptions sendOptions)
#pragma warning restore PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException("Before sending any messages, make sure to open the session by calling the `Open`-method.");
            }

            sendOptions.GetExtensions().Set(pendingOperations);
            await messageSession.Send(message, sendOptions).ConfigureAwait(false);
        }

#pragma warning disable PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext
        public async Task Send<T>(Action<T> messageConstructor, SendOptions sendOptions)
#pragma warning restore PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException("Before sending any messages, make sure to open the session by calling the `Open`-method.");
            }

            sendOptions.GetExtensions().Set(pendingOperations);
            await messageSession.Send(messageConstructor, sendOptions).ConfigureAwait(false);
        }

#pragma warning disable PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext
        public async Task Publish(object message, PublishOptions publishOptions)
#pragma warning restore PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException("Before publishing any messages, make sure to open the session by calling the `Open`-method.");
            }

            publishOptions.GetExtensions().Set(pendingOperations);
            await messageSession.Publish(message, publishOptions).ConfigureAwait(false);
        }

#pragma warning disable PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext
        public async Task Publish<T>(Action<T> messageConstructor, PublishOptions publishOptions)
#pragma warning restore PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException("Before publishing any messages, make sure to open the session by calling the `Open`-method.");
            }

            publishOptions.GetExtensions().Set(pendingOperations);
            await messageSession.Publish(messageConstructor, publishOptions).ConfigureAwait(false);
        }

        public void Dispose()
        {
            // Dispose of unmanaged resources.
            Dispose(true);
            // Suppress finalization.
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                synchronizedStorageSession?.Dispose();
            }

            disposed = true;
        }

        protected readonly CompletableSynchronizedStorageSession synchronizedStorageSession;
        protected readonly IDispatchMessages dispatcher;
        protected readonly PendingTransportOperations pendingOperations;
        protected readonly TransportTransaction transportTransaction;
        protected OpenSessionOptions options;
        readonly IMessageSession messageSession;
        bool disposed;
    }
}