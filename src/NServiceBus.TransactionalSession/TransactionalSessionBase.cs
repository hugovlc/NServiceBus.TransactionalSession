namespace NServiceBus.TransactionalSession
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Persistence;
    using Transport;

    abstract class TransactionalSessionBase : ITransactionalSession
    {
        protected TransactionalSessionBase(
            CompletableSynchronizedStorageSessionAdapter synchronizedStorageSession,
            IMessageSession messageSession,
            IDispatchMessages dispatcher,
            IEnumerable<IOpenSessionOptionsCustomization> customizations)
        {
            this.synchronizedStorageSession = synchronizedStorageSession;
            this.messageSession = messageSession;
            this.dispatcher = dispatcher;
            this.customizations = customizations;
            pendingOperations = new PendingTransportOperations();
        }


        public object manualSessionData;

        public object ManualSessionData
        {
            get
            {
                if (manualSessionData == null)
                {
                    throw new InvalidOperationException(
                        "The session has to be opened before accessing the SynchronizedStorageSession.");
                }

                return manualSessionData;
            }
        }

        public SynchronizedStorageSession _synchronizedStorageSession;
        public SynchronizedStorageSession SynchronizedStorageSession
        {
            get
            {
                if (_synchronizedStorageSession == null)
                {
                    throw new InvalidOperationException(
                        "The session has to be opened before accessing the SynchronizedStorageSession.");
                }

                return _synchronizedStorageSession;
            }
        }

        public string SessionId
        {
            get
            {
                if (synchronizedStorageSession == null)
                {
                    throw new InvalidOperationException(
                        "The session has to be opened before accessing the SessionId.");
                }

                return options?.SessionId;
            }
        }

        protected ContextBag Context => options.Extensions;

        protected bool IsOpen { get; private set; }

        public async Task Commit(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ThrowIfNotOpened();

            await CommitInternal(cancellationToken).ConfigureAwait(false);

            committed = true;
            IsOpen = false;
        }


        protected abstract Task CommitInternal(CancellationToken cancellationToken = default);

        public virtual Task Open(OpenSessionOptions options, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ThrowIfCommitted();

            if (IsOpen)
            {
                throw new InvalidOperationException($"This session is already open. {nameof(ITransactionalSession)}.{nameof(ITransactionalSession.Open)} should only be called once.");
            }

            this.options = options;
            IsOpen = true;

            foreach (var customization in customizations)
            {
                customization.Apply(this.options);
            }

            return Task.CompletedTask;
        }

        internal virtual Task OpenWithoutTransaction(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (IsOpen)
            {
                throw new InvalidOperationException($"This session is already open. {nameof(ITransactionalSession)}.{nameof(ITransactionalSession.Open)} should only be called once.");
            }

            IsOpen = true;
            committed = false;

            return Task.CompletedTask;
        }

        public virtual Task InitManualSessioMode(object manualSession, OpenSessionOptions options, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task InitOptions(OpenSessionOptions options, CancellationToken cancellationToken = default)
        {
            this.options = options;

            foreach (var customization in customizations)
            {
                customization.Apply(this.options);
            }

            return Task.CompletedTask;
        }

        public async Task Send(object message, SendOptions sendOptions, CancellationToken cancellationToken = default)
        {
            ThrowIfInvalidState();

            sendOptions.GetExtensions().Set(pendingOperations);
            await messageSession.Send(message, sendOptions).ConfigureAwait(false);
        }

        public async Task Send<T>(Action<T> messageConstructor, SendOptions sendOptions, CancellationToken cancellationToken = default)
        {
            ThrowIfInvalidState();

            sendOptions.GetExtensions().Set(pendingOperations);
            await messageSession.Send(messageConstructor, sendOptions).ConfigureAwait(false);
        }

        public async Task Publish(object message, PublishOptions publishOptions, CancellationToken cancellationToken = default)
        {
            ThrowIfInvalidState();

            publishOptions.GetExtensions().Set(pendingOperations);
            await messageSession.Publish(message, publishOptions).ConfigureAwait(false);
        }

        public async Task Publish<T>(Action<T> messageConstructor, PublishOptions publishOptions, CancellationToken cancellationToken = default)
        {
            ThrowIfInvalidState();

            publishOptions.GetExtensions().Set(pendingOperations);
            await messageSession.Publish(messageConstructor, publishOptions).ConfigureAwait(false);
        }

        void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(Dispose));
            }
        }

        void ThrowIfCommitted()
        {
            if (committed)
            {
                throw new InvalidOperationException("This session has already been committed. Complete all session operations before calling `Commit` or use a new session.");
            }
        }

        void ThrowIfNotOpened()
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException("This session has not been opened yet.");
            }
        }

        void ThrowIfInvalidState()
        {
            ThrowIfDisposed();
            ThrowIfCommitted();
            ThrowIfNotOpened();
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

            disposed = true;
        }

        protected readonly CompletableSynchronizedStorageSessionAdapter synchronizedStorageSession;
        protected readonly IDispatchMessages dispatcher;
        readonly IEnumerable<IOpenSessionOptionsCustomization> customizations;
        protected readonly PendingTransportOperations pendingOperations;
        protected OpenSessionOptions options;
        readonly IMessageSession messageSession;
        protected bool disposed;
        protected bool committed;
    }
}