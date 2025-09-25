namespace DotNetShell.Abstractions.Services;

/// <summary>
/// Service interface for message bus operations supporting publish-subscribe patterns and message queuing.
/// </summary>
public interface IMessageBus
{
    /// <summary>
    /// Publishes a message to the specified topic.
    /// </summary>
    /// <typeparam name="T">The type of the message.</typeparam>
    /// <param name="topic">The topic to publish to.</param>
    /// <param name="message">The message to publish.</param>
    /// <param name="options">Optional publishing options.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the publish operation.</returns>
    Task PublishAsync<T>(string topic, T message, PublishOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes multiple messages to the specified topic in a single operation.
    /// </summary>
    /// <typeparam name="T">The type of the messages.</typeparam>
    /// <param name="topic">The topic to publish to.</param>
    /// <param name="messages">The messages to publish.</param>
    /// <param name="options">Optional publishing options.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the publish operation.</returns>
    Task PublishManyAsync<T>(string topic, IEnumerable<T> messages, PublishOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to messages from the specified topic.
    /// </summary>
    /// <typeparam name="T">The type of messages to receive.</typeparam>
    /// <param name="topic">The topic to subscribe to.</param>
    /// <param name="handler">The handler function to process received messages.</param>
    /// <param name="options">Optional subscription options.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A subscription that can be disposed to unsubscribe.</returns>
    Task<ISubscription> SubscribeAsync<T>(string topic, Func<T, MessageContext, Task> handler, SubscriptionOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to messages from the specified topic with error handling.
    /// </summary>
    /// <typeparam name="T">The type of messages to receive.</typeparam>
    /// <param name="topic">The topic to subscribe to.</param>
    /// <param name="handler">The handler function to process received messages.</param>
    /// <param name="errorHandler">The error handler function for processing failures.</param>
    /// <param name="options">Optional subscription options.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A subscription that can be disposed to unsubscribe.</returns>
    Task<ISubscription> SubscribeAsync<T>(string topic, Func<T, MessageContext, Task> handler, Func<Exception, MessageContext, Task> errorHandler, SubscriptionOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to all messages matching the specified pattern.
    /// </summary>
    /// <param name="topicPattern">The topic pattern to match (supports wildcards).</param>
    /// <param name="handler">The handler function to process received messages.</param>
    /// <param name="options">Optional subscription options.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A subscription that can be disposed to unsubscribe.</returns>
    Task<ISubscription> SubscribeToPatternAsync(string topicPattern, Func<object, MessageContext, Task> handler, SubscriptionOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message to a specific queue for guaranteed delivery and processing.
    /// </summary>
    /// <typeparam name="T">The type of the message.</typeparam>
    /// <param name="queue">The queue name.</param>
    /// <param name="message">The message to send.</param>
    /// <param name="options">Optional queue message options.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the send operation.</returns>
    Task SendToQueueAsync<T>(string queue, T message, QueueMessageOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message with a delay before it becomes available for processing.
    /// </summary>
    /// <typeparam name="T">The type of the message.</typeparam>
    /// <param name="queue">The queue name.</param>
    /// <param name="message">The message to send.</param>
    /// <param name="delay">The delay before the message becomes available.</param>
    /// <param name="options">Optional queue message options.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the send operation.</returns>
    Task SendDelayedAsync<T>(string queue, T message, TimeSpan delay, QueueMessageOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedules a message to be sent at a specific time.
    /// </summary>
    /// <typeparam name="T">The type of the message.</typeparam>
    /// <param name="queue">The queue name.</param>
    /// <param name="message">The message to send.</param>
    /// <param name="scheduleTime">The time when the message should be sent.</param>
    /// <param name="options">Optional queue message options.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the send operation.</returns>
    Task ScheduleAsync<T>(string queue, T message, DateTimeOffset scheduleTime, QueueMessageOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Receives messages from a queue with manual acknowledgment.
    /// </summary>
    /// <typeparam name="T">The type of messages to receive.</typeparam>
    /// <param name="queue">The queue name.</param>
    /// <param name="handler">The handler function to process received messages.</param>
    /// <param name="options">Optional queue subscription options.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A subscription that can be disposed to stop receiving messages.</returns>
    Task<ISubscription> ReceiveFromQueueAsync<T>(string queue, Func<T, IMessageAcknowledgment, Task> handler, QueueSubscriptionOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a request message and waits for a response (RPC pattern).
    /// </summary>
    /// <typeparam name="TRequest">The type of the request message.</typeparam>
    /// <typeparam name="TResponse">The type of the response message.</typeparam>
    /// <param name="topic">The topic to send the request to.</param>
    /// <param name="request">The request message.</param>
    /// <param name="timeout">The timeout for waiting for a response.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The response message.</returns>
    Task<TResponse> RequestAsync<TRequest, TResponse>(string topic, TRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles request messages and sends responses (RPC pattern).
    /// </summary>
    /// <typeparam name="TRequest">The type of the request messages.</typeparam>
    /// <typeparam name="TResponse">The type of the response messages.</typeparam>
    /// <param name="topic">The topic to handle requests for.</param>
    /// <param name="handler">The handler function to process requests and generate responses.</param>
    /// <param name="options">Optional subscription options.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A subscription that can be disposed to stop handling requests.</returns>
    Task<ISubscription> HandleRequestsAsync<TRequest, TResponse>(string topic, Func<TRequest, MessageContext, Task<TResponse>> handler, SubscriptionOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a message transaction for atomic operations.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A message transaction.</returns>
    Task<IMessageTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets statistics about the message bus.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>Message bus statistics.</returns>
    Task<MessageBusStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets information about the specified topic.
    /// </summary>
    /// <param name="topic">The topic name.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>Topic information.</returns>
    Task<TopicInfo> GetTopicInfoAsync(string topic, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets information about the specified queue.
    /// </summary>
    /// <param name="queue">The queue name.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>Queue information.</returns>
    Task<QueueInfo> GetQueueInfoAsync(string queue, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the provider name (e.g., "RabbitMQ", "Azure Service Bus", "In-Memory").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Gets a value indicating whether the message bus supports transactions.
    /// </summary>
    bool SupportsTransactions { get; }

    /// <summary>
    /// Gets a value indicating whether the message bus supports delayed messages.
    /// </summary>
    bool SupportsDelayedMessages { get; }

    /// <summary>
    /// Gets a value indicating whether the message bus supports message scheduling.
    /// </summary>
    bool SupportsScheduledMessages { get; }
}

/// <summary>
/// Represents a subscription to messages that can be disposed to unsubscribe.
/// </summary>
public interface ISubscription : IDisposable
{
    /// <summary>
    /// Gets the subscription ID.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the topic or queue being subscribed to.
    /// </summary>
    string Target { get; }

    /// <summary>
    /// Gets a value indicating whether the subscription is active.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Gets the subscription statistics.
    /// </summary>
    SubscriptionStatistics Statistics { get; }

    /// <summary>
    /// Unsubscribes from the messages.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the unsubscribe operation.</returns>
    Task UnsubscribeAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a message acknowledgment for manual acknowledgment scenarios.
/// </summary>
public interface IMessageAcknowledgment
{
    /// <summary>
    /// Gets the message ID.
    /// </summary>
    string MessageId { get; }

    /// <summary>
    /// Acknowledges the message as successfully processed.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the acknowledgment operation.</returns>
    Task AckAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Negatively acknowledges the message, indicating processing failure.
    /// </summary>
    /// <param name="requeue">Whether to requeue the message for retry.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the negative acknowledgment operation.</returns>
    Task NackAsync(bool requeue = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects the message and optionally sends it to a dead letter queue.
    /// </summary>
    /// <param name="reason">The reason for rejection.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the rejection operation.</returns>
    Task RejectAsync(string? reason = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a message transaction for atomic operations.
/// </summary>
public interface IMessageTransaction : IDisposable
{
    /// <summary>
    /// Gets the transaction ID.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets a value indicating whether the transaction is active.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Commits the transaction.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the commit operation.</returns>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the transaction.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the rollback operation.</returns>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Contains context information about a received message.
/// </summary>
public class MessageContext
{
    /// <summary>
    /// Gets or sets the message ID.
    /// </summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the correlation ID for tracking related messages.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the reply-to address for RPC scenarios.
    /// </summary>
    public string? ReplyTo { get; set; }

    /// <summary>
    /// Gets or sets the topic or queue the message was received from.
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the message was sent.
    /// </summary>
    public DateTimeOffset SentAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the message was received.
    /// </summary>
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the number of delivery attempts for this message.
    /// </summary>
    public int DeliveryAttempt { get; set; } = 1;

    /// <summary>
    /// Gets or sets the message headers.
    /// </summary>
    public IDictionary<string, object> Headers { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets the message properties.
    /// </summary>
    public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets the content type of the message.
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Gets or sets the content encoding of the message.
    /// </summary>
    public string? ContentEncoding { get; set; }

    /// <summary>
    /// Gets or sets the user ID that sent the message.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Gets or sets the application ID that sent the message.
    /// </summary>
    public string? ApplicationId { get; set; }
}

/// <summary>
/// Options for publishing messages.
/// </summary>
public class PublishOptions
{
    /// <summary>
    /// Gets or sets the correlation ID for the message.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the message headers.
    /// </summary>
    public IDictionary<string, object> Headers { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets the message properties.
    /// </summary>
    public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets the message expiration time.
    /// </summary>
    public TimeSpan? Expiry { get; set; }

    /// <summary>
    /// Gets or sets the message priority (0-9, 9 being highest).
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the message should be persisted.
    /// </summary>
    public bool Persistent { get; set; } = true;
}

/// <summary>
/// Options for subscribing to messages.
/// </summary>
public class SubscriptionOptions
{
    /// <summary>
    /// Gets or sets the consumer group for the subscription.
    /// </summary>
    public string? ConsumerGroup { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of concurrent messages to process.
    /// </summary>
    public int MaxConcurrency { get; set; } = 1;

    /// <summary>
    /// Gets or sets the prefetch count for message buffering.
    /// </summary>
    public int PrefetchCount { get; set; } = 10;

    /// <summary>
    /// Gets or sets whether to auto-acknowledge messages after processing.
    /// </summary>
    public bool AutoAcknowledge { get; set; } = true;

    /// <summary>
    /// Gets or sets the acknowledgment timeout.
    /// </summary>
    public TimeSpan? AckTimeout { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of delivery attempts before dead lettering.
    /// </summary>
    public int? MaxDeliveryAttempts { get; set; }

    /// <summary>
    /// Gets or sets whether to start consuming from the beginning of the topic/queue.
    /// </summary>
    public bool ConsumeFromBeginning { get; set; }

    /// <summary>
    /// Gets or sets additional provider-specific options.
    /// </summary>
    public IDictionary<string, object> ProviderOptions { get; set; } = new Dictionary<string, object>();
}

/// <summary>
/// Options for queue messages.
/// </summary>
public class QueueMessageOptions : PublishOptions
{
    /// <summary>
    /// Gets or sets the time to live for the message.
    /// </summary>
    public TimeSpan? TimeToLive { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of delivery attempts.
    /// </summary>
    public int? MaxDeliveryAttempts { get; set; }

    /// <summary>
    /// Gets or sets the dead letter queue name.
    /// </summary>
    public string? DeadLetterQueue { get; set; }

    /// <summary>
    /// Gets or sets the retry delay for failed messages.
    /// </summary>
    public TimeSpan? RetryDelay { get; set; }
}

/// <summary>
/// Options for queue subscriptions.
/// </summary>
public class QueueSubscriptionOptions : SubscriptionOptions
{
    /// <summary>
    /// Gets or sets the visibility timeout for messages.
    /// </summary>
    public TimeSpan? VisibilityTimeout { get; set; }

    /// <summary>
    /// Gets or sets the lock duration for messages.
    /// </summary>
    public TimeSpan? LockDuration { get; set; }

    /// <summary>
    /// Gets or sets whether to include dead letter messages.
    /// </summary>
    public bool IncludeDeadLetterMessages { get; set; }
}

/// <summary>
/// Contains statistics about the message bus.
/// </summary>
public class MessageBusStatistics
{
    /// <summary>
    /// Gets or sets the total number of messages published.
    /// </summary>
    public long MessagesPublished { get; set; }

    /// <summary>
    /// Gets or sets the total number of messages consumed.
    /// </summary>
    public long MessagesConsumed { get; set; }

    /// <summary>
    /// Gets or sets the total number of active subscriptions.
    /// </summary>
    public long ActiveSubscriptions { get; set; }

    /// <summary>
    /// Gets or sets the total number of active queues.
    /// </summary>
    public long ActiveQueues { get; set; }

    /// <summary>
    /// Gets or sets the total number of active topics.
    /// </summary>
    public long ActiveTopics { get; set; }

    /// <summary>
    /// Gets or sets the average message processing time.
    /// </summary>
    public TimeSpan AverageProcessingTime { get; set; }

    /// <summary>
    /// Gets or sets additional provider-specific statistics.
    /// </summary>
    public IDictionary<string, object> ProviderStats { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets the timestamp when these statistics were collected.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Contains statistics about a subscription.
/// </summary>
public class SubscriptionStatistics
{
    /// <summary>
    /// Gets or sets the number of messages received.
    /// </summary>
    public long MessagesReceived { get; set; }

    /// <summary>
    /// Gets or sets the number of messages processed successfully.
    /// </summary>
    public long MessagesProcessed { get; set; }

    /// <summary>
    /// Gets or sets the number of messages that failed processing.
    /// </summary>
    public long MessagesFailed { get; set; }

    /// <summary>
    /// Gets or sets the average processing time per message.
    /// </summary>
    public TimeSpan AverageProcessingTime { get; set; }

    /// <summary>
    /// Gets or sets the last message received timestamp.
    /// </summary>
    public DateTimeOffset? LastMessageReceived { get; set; }

    /// <summary>
    /// Gets or sets the subscription start time.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }
}

/// <summary>
/// Contains information about a topic.
/// </summary>
public class TopicInfo
{
    /// <summary>
    /// Gets or sets the topic name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of active subscribers.
    /// </summary>
    public long SubscriberCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of messages published to the topic.
    /// </summary>
    public long MessageCount { get; set; }

    /// <summary>
    /// Gets or sets the topic creation time.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets additional topic metadata.
    /// </summary>
    public IDictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
}

/// <summary>
/// Contains information about a queue.
/// </summary>
public class QueueInfo
{
    /// <summary>
    /// Gets or sets the queue name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of messages in the queue.
    /// </summary>
    public long MessageCount { get; set; }

    /// <summary>
    /// Gets or sets the number of active consumers.
    /// </summary>
    public long ConsumerCount { get; set; }

    /// <summary>
    /// Gets or sets the number of messages in the dead letter queue.
    /// </summary>
    public long DeadLetterCount { get; set; }

    /// <summary>
    /// Gets or sets the queue creation time.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets additional queue metadata.
    /// </summary>
    public IDictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
}