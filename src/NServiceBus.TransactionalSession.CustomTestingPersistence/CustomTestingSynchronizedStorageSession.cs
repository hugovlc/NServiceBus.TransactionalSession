﻿namespace NServiceBus.AcceptanceTesting
{
    using System;
    using System.Threading.Tasks;
    using System.Transactions;
    using Persistence;

    class CustomTestingSynchronizedStorageSession : CompletableSynchronizedStorageSession
    {
        public AcceptanceTestingTransaction Transaction { get; private set; }

        public void Dispose() => Transaction = null;

        public CustomTestingSynchronizedStorageSession()
        {
            Transaction = new AcceptanceTestingTransaction();
            ownsTransaction = true;
        }

        public CustomTestingSynchronizedStorageSession(CustomTestingOutboxTransaction outboxTransaction)
        {
            Transaction = outboxTransaction.Transaction;
            ownsTransaction = false;
        }

        public CustomTestingSynchronizedStorageSession(Transaction ambientTransaction)
        {
            Transaction = new AcceptanceTestingTransaction();
            ambientTransaction.EnlistVolatile(new EnlistmentNotification(Transaction), EnlistmentOptions.None);
            ownsTransaction = true;
        }

        public Task Open()
        {
            ownsTransaction = true;
            Transaction = new AcceptanceTestingTransaction();
            return Task.CompletedTask;
        }

        public Task CompleteAsync()
        {
            if (ownsTransaction)
            {
                Transaction.Commit();
            }

            return Task.CompletedTask;
        }

        public void Enlist(Action action) => Transaction.Enlist(action);

        bool ownsTransaction;

        sealed class EnlistmentNotification : IEnlistmentNotification
        {
            public EnlistmentNotification(AcceptanceTestingTransaction transaction) => this.transaction = transaction;

            public void Prepare(PreparingEnlistment preparingEnlistment)
            {
                try
                {
                    transaction.Commit();
                    preparingEnlistment.Prepared();
                }
                catch (Exception ex)
                {
                    preparingEnlistment.ForceRollback(ex);
                }
            }

            public void Commit(Enlistment enlistment) => enlistment.Done();

            public void Rollback(Enlistment enlistment)
            {
                transaction.Rollback();
                enlistment.Done();
            }

            public void InDoubt(Enlistment enlistment) => enlistment.Done();

            readonly AcceptanceTestingTransaction transaction;
        }
    }
}