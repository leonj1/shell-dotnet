namespace DotNetShell.Abstractions.Services;

/// <summary>
/// Service interface for event bus operations supporting domain events and module communication.
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Publishes an event to all registered handlers.
    /// </summary>
    /// <typeparam name="T">The type of the event.</typeparam>
    /// <param name="domainEvent">The event to publish.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the publish operation.</returns>
    Task PublishAsync<T>(T domainEvent, CancellationToken cancellationToken = default) where T : IDomainEvent;

    /// <summary>
    /// Publishes multiple events in a single operation.
    /// </summary>
    /// <param name="domainEvents">The events to publish.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the publish operation.</returns>
    Task PublishManyAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to events of a specific type.
    /// </summary>
    /// <typeparam name="T">The type of events to subscribe to.</typeparam>
    /// <param name="handler">The handler function to process events.</param>
    /// <param name="options">Optional subscription options.</param>
    /// <returns>A subscription that can be disposed to unsubscribe.</returns>
    IEventSubscription Subscribe<T>(Func<T, EventContext, Task> handler, EventSubscriptionOptions? options = null) where T : IDomainEvent;

    /// <summary>
    /// Subscribes to events of a specific type with error handling.
    /// </summary>
    /// <typeparam name="T">The type of events to subscribe to.</typeparam>
    /// <param name="handler">The handler function to process events.</param>
    /// <param name="errorHandler">The error handler function for processing failures.</param>
    /// <param name="options">Optional subscription options.</param>
    /// <returns>A subscription that can be disposed to unsubscribe.</returns>
    IEventSubscription Subscribe<T>(Func<T, EventContext, Task> handler, Func<Exception, T, EventContext, Task> errorHandler, EventSubscriptionOptions? options = null) where T : IDomainEvent;

    /// <summary>
    /// Subscribes to all events using a dynamic handler.
    /// </summary>
    /// <param name="handler">The handler function to process any event.</param>
    /// <param name="options">Optional subscription options.</param>
    /// <returns>A subscription that can be disposed to unsubscribe.</returns>
    IEventSubscription SubscribeToAll(Func<IDomainEvent, EventContext, Task> handler, EventSubscriptionOptions? options = null);

    /// <summary>
    /// Subscribes to events that match a predicate condition.
    /// </summary>
    /// <param name="predicate">The predicate to filter events.</param>
    /// <param name="handler">The handler function to process matching events.</param>
    /// <param name="options">Optional subscription options.</param>
    /// <returns>A subscription that can be disposed to unsubscribe.</returns>
    IEventSubscription SubscribeWhere(Func<IDomainEvent, EventContext, bool> predicate, Func<IDomainEvent, EventContext, Task> handler, EventSubscriptionOptions? options = null);

    /// <summary>
    /// Publishes an event and waits for all handlers to complete processing.
    /// </summary>
    /// <typeparam name="T">The type of the event.</typeparam>
    /// <param name="domainEvent">The event to publish.</param>
    /// <param name="timeout">Optional timeout for waiting for handlers.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The results from all handlers.</returns>
    Task<IEnumerable<EventHandlerResult>> PublishAndWaitAsync<T>(T domainEvent, TimeSpan? timeout = null, CancellationToken cancellationToken = default) where T : IDomainEvent;

    /// <summary>
    /// Publishes an event with guaranteed delivery (persisted until all handlers process it).
    /// </summary>
    /// <typeparam name="T">The type of the event.</typeparam>
    /// <param name="domainEvent">The event to publish.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the publish operation.</returns>
    Task PublishWithGuaranteeAsync<T>(T domainEvent, CancellationToken cancellationToken = default) where T : IDomainEvent;

    /// <summary>
    /// Schedules an event to be published at a specific time.
    /// </summary>
    /// <typeparam name="T">The type of the event.</typeparam>
    /// <param name="domainEvent">The event to schedule.</param>
    /// <param name="scheduleTime">When to publish the event.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A scheduled event ID that can be used to cancel the scheduled event.</returns>
    Task<string> ScheduleEventAsync<T>(T domainEvent, DateTimeOffset scheduleTime, CancellationToken cancellationToken = default) where T : IDomainEvent;

    /// <summary>
    /// Cancels a scheduled event.
    /// </summary>
    /// <param name="scheduledEventId">The ID of the scheduled event to cancel.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>True if the event was found and cancelled; otherwise, false.</returns>
    Task<bool> CancelScheduledEventAsync(string scheduledEventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins an event transaction for atomic event publishing.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An event transaction.</returns>
    Task<IEventTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets statistics about the event bus.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>Event bus statistics.</returns>
    Task<EventBusStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets information about event handlers for a specific event type.
    /// </summary>
    /// <typeparam name="T">The event type.</typeparam>
    /// <returns>Handler information for the event type.</returns>
    EventHandlerInfo[] GetHandlers<T>() where T : IDomainEvent;

    /// <summary>
    /// Gets all registered event types.
    /// </summary>
    /// <returns>An enumerable collection of registered event types.</returns>
    IEnumerable<Type> GetRegisteredEventTypes();

    /// <summary>
    /// Replays events from the event store (if supported).
    /// </summary>
    /// <param name="fromTimestamp">The starting timestamp for replay.</param>
    /// <param name="toTimestamp">The ending timestamp for replay.</param>
    /// <param name="eventTypes">Optional filter for specific event types.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the replay operation.</returns>
    Task ReplayEventsAsync(DateTimeOffset fromTimestamp, DateTimeOffset? toTimestamp = null, Type[]? eventTypes = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the provider name (e.g., "InMemory", "Redis", "EventStore").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Gets a value indicating whether the event bus supports event persistence.
    /// </summary>
    bool SupportsPersistence { get; }

    /// <summary>
    /// Gets a value indicating whether the event bus supports event scheduling.
    /// </summary>
    bool SupportsScheduling { get; }

    /// <summary>
    /// Gets a value indicating whether the event bus supports transactions.
    /// </summary>
    bool SupportsTransactions { get; }

    /// <summary>
    /// Gets a value indicating whether the event bus supports event replay.
    /// </summary>
    bool SupportsReplay { get; }
}

/// <summary>
/// Base interface for all domain events.
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// Gets the unique identifier for this event instance.
    /// </summary>
    string EventId { get; }

    /// <summary>
    /// Gets the timestamp when the event occurred.
    /// </summary>
    DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets the version of the event schema.
    /// </summary>
    int Version { get; }

    /// <summary>
    /// Gets additional metadata associated with the event.
    /// </summary>
    IDictionary<string, object> Metadata { get; }
}

/// <summary>
/// Base implementation of IDomainEvent.
/// </summary>
public abstract class DomainEventBase : IDomainEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DomainEventBase"/> class.
    /// </summary>
    protected DomainEventBase()
    {
        EventId = Guid.NewGuid().ToString();
        Timestamp = DateTimeOffset.UtcNow;
        Version = 1;
        Metadata = new Dictionary<string, object>();
    }

    /// <inheritdoc/>
    public string EventId { get; private set; }

    /// <inheritdoc/>
    public DateTimeOffset Timestamp { get; private set; }

    /// <inheritdoc/>
    public virtual int Version { get; protected set; }

    /// <inheritdoc/>
    public IDictionary<string, object> Metadata { get; private set; }

    /// <summary>
    /// Sets metadata for the event.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    /// <returns>This event instance for method chaining.</returns>
    public DomainEventBase WithMetadata(string key, object value)
    {
        Metadata[key] = value;
        return this;
    }

    /// <summary>
    /// Sets multiple metadata values for the event.
    /// </summary>
    /// <param name="metadata">The metadata to set.</param>
    /// <returns>This event instance for method chaining.</returns>
    public DomainEventBase WithMetadata(IDictionary<string, object> metadata)
    {
        foreach (var kvp in metadata)
        {
            Metadata[kvp.Key] = kvp.Value;
        }
        return this;
    }
}

/// <summary>
/// Represents an event subscription that can be disposed to unsubscribe.
/// </summary>
public interface IEventSubscription : IDisposable
{
    /// <summary>
    /// Gets the subscription ID.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the event type being subscribed to.
    /// </summary>
    Type EventType { get; }

    /// <summary>
    /// Gets a value indicating whether the subscription is active.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Gets the subscription options.
    /// </summary>
    EventSubscriptionOptions Options { get; }

    /// <summary>
    /// Gets the subscription statistics.
    /// </summary>
    EventSubscriptionStatistics Statistics { get; }

    /// <summary>
    /// Unsubscribes from events.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the unsubscribe operation.</returns>
    Task UnsubscribeAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an event transaction for atomic event operations.
/// </summary>
public interface IEventTransaction : IDisposable
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
    /// Gets the events that will be published when the transaction commits.
    /// </summary>
    IReadOnlyList<IDomainEvent> PendingEvents { get; }

    /// <summary>
    /// Adds an event to the transaction.
    /// </summary>
    /// <typeparam name="T">The type of the event.</typeparam>
    /// <param name="domainEvent">The event to add.</param>
    void Add<T>(T domainEvent) where T : IDomainEvent;

    /// <summary>
    /// Commits the transaction, publishing all events.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the commit operation.</returns>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the transaction, discarding all events.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the rollback operation.</returns>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Contains context information about an event being processed.
/// </summary>
public class EventContext
{
    /// <summary>
    /// Gets or sets the correlation ID for tracking related events.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the causation ID (the event that caused this event).
    /// </summary>
    public string? CausationId { get; set; }

    /// <summary>
    /// Gets or sets the user ID associated with the event.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Gets or sets the module that published the event.
    /// </summary>
    public string? SourceModule { get; set; }

    /// <summary>
    /// Gets or sets the aggregate ID associated with the event.
    /// </summary>
    public string? AggregateId { get; set; }

    /// <summary>
    /// Gets or sets the aggregate type associated with the event.
    /// </summary>
    public string? AggregateType { get; set; }

    /// <summary>
    /// Gets or sets the sequence number for ordered event processing.
    /// </summary>
    public long? SequenceNumber { get; set; }

    /// <summary>
    /// Gets or sets the retry attempt number for failed event processing.
    /// </summary>
    public int RetryAttempt { get; set; }

    /// <summary>
    /// Gets or sets additional context properties.
    /// </summary>
    public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets the timestamp when the event was received for processing.
    /// </summary>
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Options for event subscriptions.
/// </summary>
public class EventSubscriptionOptions
{
    /// <summary>
    /// Gets or sets the subscription name for identification.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the consumer group for the subscription.
    /// </summary>
    public string? ConsumerGroup { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of concurrent events to process.
    /// </summary>
    public int MaxConcurrency { get; set; } = 1;

    /// <summary>
    /// Gets or sets the maximum number of retry attempts for failed events.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the delay between retry attempts.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets the backoff multiplier for retry delays.
    /// </summary>
    public double RetryBackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Gets or sets the maximum retry delay.
    /// </summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets a value indicating whether to process events from the beginning.
    /// </summary>
    public bool ProcessFromBeginning { get; set; }

    /// <summary>
    /// Gets or sets the event filter predicate.
    /// </summary>
    public Func<IDomainEvent, EventContext, bool>? Filter { get; set; }

    /// <summary>
    /// Gets or sets the order in which handlers should be executed (lower numbers first).
    /// </summary>
    public int ExecutionOrder { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the handler should run in isolation.
    /// </summary>
    public bool Isolated { get; set; }

    /// <summary>
    /// Gets or sets additional provider-specific options.
    /// </summary>
    public IDictionary<string, object> ProviderOptions { get; set; } = new Dictionary<string, object>();
}

/// <summary>
/// Represents the result of an event handler execution.
/// </summary>
public class EventHandlerResult
{
    /// <summary>
    /// Gets the handler name or identifier.
    /// </summary>
    public string HandlerName { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the handler executed successfully.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the error message if the handler failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Gets the exception if the handler failed.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Gets the execution duration.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets additional result data.
    /// </summary>
    public IDictionary<string, object> Data { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="handlerName">The handler name.</param>
    /// <param name="duration">The execution duration.</param>
    /// <param name="data">Optional result data.</param>
    /// <returns>A successful handler result.</returns>
    public static EventHandlerResult Success(string handlerName, TimeSpan duration, IDictionary<string, object>? data = null)
    {
        return new EventHandlerResult
        {
            HandlerName = handlerName,
            IsSuccess = true,
            Duration = duration,
            Data = data ?? new Dictionary<string, object>()
        };
    }

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="handlerName">The handler name.</param>
    /// <param name="error">The error message.</param>
    /// <param name="exception">The exception.</param>
    /// <param name="duration">The execution duration.</param>
    /// <returns>A failed handler result.</returns>
    public static EventHandlerResult Failure(string handlerName, string error, Exception? exception = null, TimeSpan duration = default)
    {
        return new EventHandlerResult
        {
            HandlerName = handlerName,
            IsSuccess = false,
            Error = error,
            Exception = exception,
            Duration = duration
        };
    }
}

/// <summary>
/// Contains information about an event handler.
/// </summary>
public class EventHandlerInfo
{
    /// <summary>
    /// Gets or sets the handler name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the handler type.
    /// </summary>
    public Type HandlerType { get; set; } = typeof(object);

    /// <summary>
    /// Gets or sets the event type handled.
    /// </summary>
    public Type EventType { get; set; } = typeof(IDomainEvent);

    /// <summary>
    /// Gets or sets the execution order.
    /// </summary>
    public int ExecutionOrder { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the handler is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets the subscription options.
    /// </summary>
    public EventSubscriptionOptions Options { get; set; } = new();
}

/// <summary>
/// Contains statistics about the event bus.
/// </summary>
public class EventBusStatistics
{
    /// <summary>
    /// Gets or sets the total number of events published.
    /// </summary>
    public long EventsPublished { get; set; }

    /// <summary>
    /// Gets or sets the total number of events processed.
    /// </summary>
    public long EventsProcessed { get; set; }

    /// <summary>
    /// Gets or sets the total number of failed event processing attempts.
    /// </summary>
    public long EventsFailed { get; set; }

    /// <summary>
    /// Gets or sets the number of active subscriptions.
    /// </summary>
    public long ActiveSubscriptions { get; set; }

    /// <summary>
    /// Gets or sets the number of registered event types.
    /// </summary>
    public long RegisteredEventTypes { get; set; }

    /// <summary>
    /// Gets or sets the average event processing time.
    /// </summary>
    public TimeSpan AverageProcessingTime { get; set; }

    /// <summary>
    /// Gets or sets the number of scheduled events.
    /// </summary>
    public long ScheduledEvents { get; set; }

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
/// Contains statistics about an event subscription.
/// </summary>
public class EventSubscriptionStatistics
{
    /// <summary>
    /// Gets or sets the number of events received.
    /// </summary>
    public long EventsReceived { get; set; }

    /// <summary>
    /// Gets or sets the number of events processed successfully.
    /// </summary>
    public long EventsProcessed { get; set; }

    /// <summary>
    /// Gets or sets the number of events that failed processing.
    /// </summary>
    public long EventsFailed { get; set; }

    /// <summary>
    /// Gets or sets the number of events currently being retried.
    /// </summary>
    public long EventsRetrying { get; set; }

    /// <summary>
    /// Gets or sets the average event processing time.
    /// </summary>
    public TimeSpan AverageProcessingTime { get; set; }

    /// <summary>
    /// Gets or sets the last event received timestamp.
    /// </summary>
    public DateTimeOffset? LastEventReceived { get; set; }

    /// <summary>
    /// Gets or sets the subscription start time.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// Gets or sets the current lag (how far behind the subscription is).
    /// </summary>
    public TimeSpan? CurrentLag { get; set; }
}