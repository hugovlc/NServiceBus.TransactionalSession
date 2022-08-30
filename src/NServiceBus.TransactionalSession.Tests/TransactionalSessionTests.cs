﻿namespace NServiceBus.TransactionalSession.Tests;

using System;
using System.Linq;
using System.Threading.Tasks;
using Extensibility;
using Fakes;
using NUnit.Framework;

[TestFixture]
public class TransactionalSessionTests
{
    [Test]
    public async Task Open_should_use_session_id_from_options()
    {
        using var session = new TransactionalSession(new FakeSynchronizableStorageSession(), new FakeMessageSession(), new FakeDispatcher());

        var openOptions = new OpenSessionOptions();
        await session.Open(openOptions);

        Assert.AreEqual(openOptions.SessionId, session.SessionId);
    }

    [Test]
    public async Task Open_should_throw_if_session_already_open()
    {
        using var session = new TransactionalSession(new FakeSynchronizableStorageSession(), new FakeMessageSession(), new FakeDispatcher());

        await session.Open();

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Open());

        StringAssert.Contains($"This session is already open. {nameof(ITransactionalSession)}.{nameof(ITransactionalSession.Open)} should only be called once.", exception.Message);
    }

    [Test]
    public async Task Open_should_open_synchronized_storage_session()
    {
        var synchronizedStorageSession = new FakeSynchronizableStorageSession();

        using var session = new TransactionalSession(synchronizedStorageSession, new FakeMessageSession(), new FakeDispatcher());

        var options = new OpenSessionOptions();
        await session.Open(options);

        Assert.IsEmpty(synchronizedStorageSession.OpenedOutboxTransactionSessions);
        Assert.AreEqual(1, synchronizedStorageSession.OpenedTransactionSessions.Count);
        Assert.AreEqual(options.Extensions, synchronizedStorageSession.OpenedTransactionSessions.Single());
        Assert.AreEqual(synchronizedStorageSession, session.SynchronizedStorageSession);
    }

    [Test]
    public async Task Send_should_set_PendingOperations_collection_on_context()
    {
        var messageSession = new FakeMessageSession();
        using var session = new TransactionalSession(new FakeSynchronizableStorageSession(), messageSession, new FakeDispatcher());

        await session.Open();
        await session.Send(new object());

        Assert.IsTrue(messageSession.SentMessages.Single().Options.GetExtensions().TryGet(out PendingTransportOperations pendingTransportOperations));
    }

    [Test]
    public async Task Publish_should_set_PendingOperations_collection_on_context()
    {
        var messageSession = new FakeMessageSession();
        using var session = new TransactionalSession(new FakeSynchronizableStorageSession(), messageSession, new FakeDispatcher());

        await session.Open();
        await session.Publish(new object());

        Assert.IsTrue(messageSession.PublishedMessages.Single().Options.GetExtensions().TryGet(out PendingTransportOperations pendingTransportOperations));
    }

    [Test]
    public void Send_should_throw_exeception_when_session_not_opened()
    {
        var messageSession = new FakeMessageSession();
        using var session = new TransactionalSession(new FakeSynchronizableStorageSession(), messageSession, new FakeDispatcher());

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Send(new object()));

        StringAssert.Contains("This session has not been opened yet.", exception.Message);
        Assert.IsEmpty(messageSession.SentMessages);
    }

    [Test]
    public void Publish_should_throw_exception_when_session_not_opened()
    {
        var messageSession = new FakeMessageSession();
        using var session = new TransactionalSession(new FakeSynchronizableStorageSession(), messageSession, new FakeDispatcher());

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Publish(new object()));

        StringAssert.Contains("This session has not been opened yet.", exception.Message);
        Assert.IsEmpty(messageSession.PublishedMessages);
    }

    [Test]
    public async Task Commit_should_send_message_and_commit_storage_tx()
    {
        var dispatcher = new FakeDispatcher();
        var synchronizableSession = new FakeSynchronizableStorageSession();
        using var session = new TransactionalSession(synchronizableSession, new FakeMessageSession(), dispatcher);

        await session.Open();
        var sendOptions = new SendOptions();
        string messageId = Guid.NewGuid().ToString();
        sendOptions.SetMessageId(messageId);
        var messageObj = new object();
        await session.Send(messageObj, sendOptions);
        await session.Commit();

        Assert.AreEqual(1, dispatcher.Dispatched.Count, "should have dispatched message");
        var dispatched = dispatcher.Dispatched.Single();
        Assert.AreEqual(1, dispatched.outgoingMessages.UnicastTransportOperations.Count);
        var dispatchedMessage = dispatched.outgoingMessages.UnicastTransportOperations.Single();
        Assert.AreEqual(messageId, dispatchedMessage.Message.MessageId);
        Assert.IsFalse(dispatchedMessage.Message.Headers.ContainsKey(Headers.ControlMessageHeader));

        Assert.IsTrue(synchronizableSession.Completed);
    }

    [Test]
    public async Task Commit_should_not_send_message_when_storage_tx_fails()
    {
        var dispatcher = new FakeDispatcher();
        var storageSession = new FakeSynchronizableStorageSession();
        storageSession.CompleteCallback = () => throw new Exception("session complete exception");

        using var session = new TransactionalSession(storageSession, new FakeMessageSession(), dispatcher);

        await session.Open();
        await session.Send(new object());
        Assert.ThrowsAsync<Exception>(async () => await session.Commit());

        Assert.IsEmpty(dispatcher.Dispatched, "should not have dispatched message");
    }
}