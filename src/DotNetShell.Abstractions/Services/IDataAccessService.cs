using System.Data;
using System.Data.Common;

namespace DotNetShell.Abstractions.Services;

/// <summary>
/// Service interface for data access operations supporting multiple database providers and patterns.
/// </summary>
public interface IDataAccessService
{
    /// <summary>
    /// Executes a SQL query and returns the results as an enumerable collection.
    /// </summary>
    /// <typeparam name="T">The type to map results to.</typeparam>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">Optional parameters for the query.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An enumerable collection of results.</returns>
    Task<IEnumerable<T>> QueryAsync<T>(string sql, object? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a SQL query and returns the first result or default value.
    /// </summary>
    /// <typeparam name="T">The type to map the result to.</typeparam>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">Optional parameters for the query.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The first result or the default value for T.</returns>
    Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a SQL query and returns a single result.
    /// </summary>
    /// <typeparam name="T">The type to map the result to.</typeparam>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">Optional parameters for the query.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A single result.</returns>
    Task<T> QuerySingleAsync<T>(string sql, object? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a SQL query and returns a single result or default value.
    /// </summary>
    /// <typeparam name="T">The type to map the result to.</typeparam>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">Optional parameters for the query.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A single result or the default value for T.</returns>
    Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a SQL command and returns the number of affected rows.
    /// </summary>
    /// <param name="sql">The SQL command to execute.</param>
    /// <param name="parameters">Optional parameters for the command.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The number of affected rows.</returns>
    Task<int> ExecuteAsync(string sql, object? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a SQL command and returns a scalar value.
    /// </summary>
    /// <typeparam name="T">The type of the scalar value.</typeparam>
    /// <param name="sql">The SQL command to execute.</param>
    /// <param name="parameters">Optional parameters for the command.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The scalar value.</returns>
    Task<T> ExecuteScalarAsync<T>(string sql, object? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes multiple SQL commands in a batch.
    /// </summary>
    /// <param name="commands">The SQL commands to execute.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The results of each command execution.</returns>
    Task<BatchExecutionResult> ExecuteBatchAsync(IEnumerable<SqlCommand> commands, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a stored procedure and returns the results.
    /// </summary>
    /// <typeparam name="T">The type to map results to.</typeparam>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <param name="parameters">Optional parameters for the procedure.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An enumerable collection of results.</returns>
    Task<IEnumerable<T>> ExecuteStoredProcedureAsync<T>(string procedureName, object? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a stored procedure and returns multiple result sets.
    /// </summary>
    /// <param name="procedureName">The name of the stored procedure.</param>
    /// <param name="parameters">Optional parameters for the procedure.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>Multiple result sets from the stored procedure.</returns>
    Task<MultipleResultSets> ExecuteStoredProcedureMultipleAsync(string procedureName, object? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new database transaction.
    /// </summary>
    /// <param name="isolationLevel">The isolation level for the transaction.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A database transaction.</returns>
    Task<IDataTransaction> BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes operations within a transaction scope.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The operation to execute within the transaction.</param>
    /// <param name="isolationLevel">The isolation level for the transaction.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The result of the operation.</returns>
    Task<T> ExecuteInTransactionAsync<T>(Func<IDataTransaction, Task<T>> operation, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes operations within a transaction scope without a return value.
    /// </summary>
    /// <param name="operation">The operation to execute within the transaction.</param>
    /// <param name="isolationLevel">The isolation level for the transaction.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the operation.</returns>
    Task ExecuteInTransactionAsync(Func<IDataTransaction, Task> operation, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a repository instance for the specified entity type.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <returns>A repository for the entity type.</returns>
    IRepository<T> GetRepository<T>() where T : class;

    /// <summary>
    /// Creates a repository instance for the specified entity type with a primary key type.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <typeparam name="TKey">The primary key type.</typeparam>
    /// <returns>A repository for the entity type.</returns>
    IRepository<T, TKey> GetRepository<T, TKey>() where T : class;

    /// <summary>
    /// Gets a connection to the database.
    /// </summary>
    /// <param name="connectionName">Optional connection name (uses default if not specified).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A database connection.</returns>
    Task<IDbConnection> GetConnectionAsync(string? connectionName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a command builder for fluent SQL construction.
    /// </summary>
    /// <param name="connectionName">Optional connection name (uses default if not specified).</param>
    /// <returns>A SQL command builder.</returns>
    ISqlCommandBuilder CreateCommandBuilder(string? connectionName = null);

    /// <summary>
    /// Executes a bulk insert operation.
    /// </summary>
    /// <typeparam name="T">The type of entities to insert.</typeparam>
    /// <param name="entities">The entities to insert.</param>
    /// <param name="options">Optional bulk insert options.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The number of entities inserted.</returns>
    Task<int> BulkInsertAsync<T>(IEnumerable<T> entities, BulkInsertOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a bulk update operation.
    /// </summary>
    /// <typeparam name="T">The type of entities to update.</typeparam>
    /// <param name="entities">The entities to update.</param>
    /// <param name="options">Optional bulk update options.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The number of entities updated.</returns>
    Task<int> BulkUpdateAsync<T>(IEnumerable<T> entities, BulkUpdateOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a bulk delete operation.
    /// </summary>
    /// <typeparam name="T">The type of entities to delete.</typeparam>
    /// <param name="entities">The entities to delete.</param>
    /// <param name="options">Optional bulk delete options.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The number of entities deleted.</returns>
    Task<int> BulkDeleteAsync<T>(IEnumerable<T> entities, BulkDeleteOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes database migrations.
    /// </summary>
    /// <param name="targetVersion">Optional target version (migrates to latest if not specified).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The migration result.</returns>
    Task<MigrationResult> MigrateAsync(string? targetVersion = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets information about available database migrations.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>Information about available migrations.</returns>
    Task<IEnumerable<MigrationInfo>> GetMigrationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a database backup.
    /// </summary>
    /// <param name="backupOptions">The backup options.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The backup result.</returns>
    Task<BackupResult> BackupAsync(DatabaseBackupOptions backupOptions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores a database from backup.
    /// </summary>
    /// <param name="restoreOptions">The restore options.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The restore result.</returns>
    Task<RestoreResult> RestoreAsync(DatabaseRestoreOptions restoreOptions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks the health of the database connection.
    /// </summary>
    /// <param name="connectionName">Optional connection name (uses default if not specified).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The database health status.</returns>
    Task<DatabaseHealthStatus> CheckHealthAsync(string? connectionName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets database statistics and performance metrics.
    /// </summary>
    /// <param name="connectionName">Optional connection name (uses default if not specified).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>Database statistics.</returns>
    Task<DatabaseStatistics> GetStatisticsAsync(string? connectionName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the database provider name (e.g., "SqlServer", "PostgreSQL", "MySQL").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Gets a value indicating whether the provider supports transactions.
    /// </summary>
    bool SupportsTransactions { get; }

    /// <summary>
    /// Gets a value indicating whether the provider supports bulk operations.
    /// </summary>
    bool SupportsBulkOperations { get; }

    /// <summary>
    /// Gets a value indicating whether the provider supports migrations.
    /// </summary>
    bool SupportsMigrations { get; }

    /// <summary>
    /// Gets a value indicating whether the provider supports stored procedures.
    /// </summary>
    bool SupportsStoredProcedures { get; }
}

/// <summary>
/// Represents a database transaction with extended capabilities.
/// </summary>
public interface IDataTransaction : IDbTransaction
{
    /// <summary>
    /// Gets the transaction ID.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the timestamp when the transaction started.
    /// </summary>
    DateTimeOffset StartTime { get; }

    /// <summary>
    /// Gets a value indicating whether the transaction is active.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Gets the data access service associated with this transaction.
    /// </summary>
    IDataAccessService DataAccess { get; }

    /// <summary>
    /// Creates a savepoint within the transaction.
    /// </summary>
    /// <param name="savepointName">The name of the savepoint.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the savepoint creation.</returns>
    Task CreateSavepointAsync(string savepointName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back to a specific savepoint.
    /// </summary>
    /// <param name="savepointName">The name of the savepoint to roll back to.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the rollback operation.</returns>
    Task RollbackToSavepointAsync(string savepointName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the transaction asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the commit operation.</returns>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the transaction asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the rollback operation.</returns>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Generic repository interface for entity operations.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public interface IRepository<T> where T : class
{
    /// <summary>
    /// Gets all entities.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>All entities.</returns>
    Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds entities matching the specified predicate.
    /// </summary>
    /// <param name="predicate">The predicate to match entities against.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>Entities matching the predicate.</returns>
    Task<IEnumerable<T>> FindAsync(System.Linq.Expressions.Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the first entity matching the predicate or default value.
    /// </summary>
    /// <param name="predicate">The predicate to match entities against.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The first matching entity or default value.</returns>
    Task<T?> FirstOrDefaultAsync(System.Linq.Expressions.Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single entity matching the predicate.
    /// </summary>
    /// <param name="predicate">The predicate to match entities against.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The single matching entity.</returns>
    Task<T> SingleAsync(System.Linq.Expressions.Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single entity matching the predicate or default value.
    /// </summary>
    /// <param name="predicate">The predicate to match entities against.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The single matching entity or default value.</returns>
    Task<T?> SingleOrDefaultAsync(System.Linq.Expressions.Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new entity.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the add operation.</returns>
    Task AddAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds multiple entities.
    /// </summary>
    /// <param name="entities">The entities to add.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the add operation.</returns>
    Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing entity.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the update operation.</returns>
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates multiple entities.
    /// </summary>
    /// <param name="entities">The entities to update.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the update operation.</returns>
    Task UpdateRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an entity.
    /// </summary>
    /// <param name="entity">The entity to delete.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the delete operation.</returns>
    Task DeleteAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes multiple entities.
    /// </summary>
    /// <param name="entities">The entities to delete.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the delete operation.</returns>
    Task DeleteRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the number of entities matching the predicate.
    /// </summary>
    /// <param name="predicate">Optional predicate to filter entities.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The count of matching entities.</returns>
    Task<int> CountAsync(System.Linq.Expressions.Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if any entities match the predicate.
    /// </summary>
    /// <param name="predicate">Optional predicate to filter entities.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>True if any entities match; otherwise, false.</returns>
    Task<bool> AnyAsync(System.Linq.Expressions.Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets entities with paging support.
    /// </summary>
    /// <param name="pageNumber">The page number (1-based).</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="orderBy">The ordering expression.</param>
    /// <param name="predicate">Optional predicate to filter entities.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A paged result of entities.</returns>
    Task<PagedResult<T>> GetPagedAsync<TKey>(int pageNumber, int pageSize, System.Linq.Expressions.Expression<Func<T, TKey>> orderBy, System.Linq.Expressions.Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Generic repository interface for entity operations with a specific key type.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
/// <typeparam name="TKey">The primary key type.</typeparam>
public interface IRepository<T, TKey> : IRepository<T> where T : class
{
    /// <summary>
    /// Gets an entity by its primary key.
    /// </summary>
    /// <param name="id">The primary key value.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The entity with the specified key, or null if not found.</returns>
    Task<T?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets multiple entities by their primary keys.
    /// </summary>
    /// <param name="ids">The primary key values.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The entities with the specified keys.</returns>
    Task<IEnumerable<T>> GetByIdsAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an entity by its primary key.
    /// </summary>
    /// <param name="id">The primary key value.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>True if the entity was found and deleted; otherwise, false.</returns>
    Task<bool> DeleteByIdAsync(TKey id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an entity with the specified key exists.
    /// </summary>
    /// <param name="id">The primary key value.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>True if the entity exists; otherwise, false.</returns>
    Task<bool> ExistsAsync(TKey id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for building SQL commands fluently.
/// </summary>
public interface ISqlCommandBuilder
{
    /// <summary>
    /// Starts a SELECT query.
    /// </summary>
    /// <param name="columns">The columns to select.</param>
    /// <returns>The command builder for method chaining.</returns>
    ISqlCommandBuilder Select(params string[] columns);

    /// <summary>
    /// Adds a FROM clause.
    /// </summary>
    /// <param name="table">The table name.</param>
    /// <returns>The command builder for method chaining.</returns>
    ISqlCommandBuilder From(string table);

    /// <summary>
    /// Adds a WHERE clause.
    /// </summary>
    /// <param name="condition">The WHERE condition.</param>
    /// <param name="parameters">Optional parameters for the condition.</param>
    /// <returns>The command builder for method chaining.</returns>
    ISqlCommandBuilder Where(string condition, object? parameters = null);

    /// <summary>
    /// Adds an ORDER BY clause.
    /// </summary>
    /// <param name="column">The column to order by.</param>
    /// <param name="descending">Whether to order in descending order.</param>
    /// <returns>The command builder for method chaining.</returns>
    ISqlCommandBuilder OrderBy(string column, bool descending = false);

    /// <summary>
    /// Adds a JOIN clause.
    /// </summary>
    /// <param name="table">The table to join.</param>
    /// <param name="condition">The join condition.</param>
    /// <param name="joinType">The type of join (INNER, LEFT, RIGHT, etc.).</param>
    /// <returns>The command builder for method chaining.</returns>
    ISqlCommandBuilder Join(string table, string condition, string joinType = "INNER");

    /// <summary>
    /// Adds a GROUP BY clause.
    /// </summary>
    /// <param name="columns">The columns to group by.</param>
    /// <returns>The command builder for method chaining.</returns>
    ISqlCommandBuilder GroupBy(params string[] columns);

    /// <summary>
    /// Adds a HAVING clause.
    /// </summary>
    /// <param name="condition">The HAVING condition.</param>
    /// <param name="parameters">Optional parameters for the condition.</param>
    /// <returns>The command builder for method chaining.</returns>
    ISqlCommandBuilder Having(string condition, object? parameters = null);

    /// <summary>
    /// Adds a LIMIT/TOP clause for paging.
    /// </summary>
    /// <param name="count">The maximum number of rows to return.</param>
    /// <param name="offset">The number of rows to skip (for OFFSET/LIMIT).</param>
    /// <returns>The command builder for method chaining.</returns>
    ISqlCommandBuilder Limit(int count, int offset = 0);

    /// <summary>
    /// Builds the SQL command string.
    /// </summary>
    /// <returns>The SQL command string.</returns>
    string Build();

    /// <summary>
    /// Gets the parameters for the built command.
    /// </summary>
    /// <returns>The command parameters.</returns>
    IDictionary<string, object> GetParameters();

    /// <summary>
    /// Creates an INSERT command.
    /// </summary>
    /// <param name="table">The table name.</param>
    /// <param name="data">The data to insert.</param>
    /// <returns>The command builder for method chaining.</returns>
    ISqlCommandBuilder Insert(string table, object data);

    /// <summary>
    /// Creates an UPDATE command.
    /// </summary>
    /// <param name="table">The table name.</param>
    /// <param name="data">The data to update.</param>
    /// <returns>The command builder for method chaining.</returns>
    ISqlCommandBuilder Update(string table, object data);

    /// <summary>
    /// Creates a DELETE command.
    /// </summary>
    /// <param name="table">The table name.</param>
    /// <returns>The command builder for method chaining.</returns>
    ISqlCommandBuilder Delete(string table);
}

/// <summary>
/// Represents a SQL command with parameters.
/// </summary>
public class SqlCommand
{
    /// <summary>
    /// Gets or sets the SQL command text.
    /// </summary>
    public string CommandText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the command parameters.
    /// </summary>
    public object? Parameters { get; set; }

    /// <summary>
    /// Gets or sets the command type.
    /// </summary>
    public CommandType CommandType { get; set; } = CommandType.Text;

    /// <summary>
    /// Gets or sets the command timeout in seconds.
    /// </summary>
    public int? TimeoutSeconds { get; set; }
}

/// <summary>
/// Represents the result of batch execution.
/// </summary>
public class BatchExecutionResult
{
    /// <summary>
    /// Gets or sets the results of individual commands.
    /// </summary>
    public IList<CommandResult> CommandResults { get; set; } = new List<CommandResult>();

    /// <summary>
    /// Gets or sets the total execution time.
    /// </summary>
    public TimeSpan TotalExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether all commands succeeded.
    /// </summary>
    public bool AllSucceeded { get; set; }

    /// <summary>
    /// Gets or sets the number of successful commands.
    /// </summary>
    public int SuccessfulCommands { get; set; }

    /// <summary>
    /// Gets or sets the number of failed commands.
    /// </summary>
    public int FailedCommands { get; set; }
}

/// <summary>
/// Represents the result of a single command execution.
/// </summary>
public class CommandResult
{
    /// <summary>
    /// Gets or sets the command index in the batch.
    /// </summary>
    public int CommandIndex { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the command succeeded.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Gets or sets the number of affected rows (for non-query commands).
    /// </summary>
    public int AffectedRows { get; set; }

    /// <summary>
    /// Gets or sets the error message if the command failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets the exception if the command failed.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Gets or sets the execution time for this command.
    /// </summary>
    public TimeSpan ExecutionTime { get; set; }
}

/// <summary>
/// Represents multiple result sets from a stored procedure.
/// </summary>
public class MultipleResultSets
{
    /// <summary>
    /// Gets or sets the result sets.
    /// </summary>
    public IList<IEnumerable<dynamic>> ResultSets { get; set; } = new List<IEnumerable<dynamic>>();

    /// <summary>
    /// Gets a specific result set as a typed enumerable.
    /// </summary>
    /// <typeparam name="T">The type to cast results to.</typeparam>
    /// <param name="index">The result set index (0-based).</param>
    /// <returns>The typed result set.</returns>
    public IEnumerable<T> GetResultSet<T>(int index)
    {
        if (index < 0 || index >= ResultSets.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        return ResultSets[index].Cast<T>();
    }
}

/// <summary>
/// Represents a paged result set.
/// </summary>
/// <typeparam name="T">The type of items in the result set.</typeparam>
public class PagedResult<T>
{
    /// <summary>
    /// Gets or sets the items in the current page.
    /// </summary>
    public IEnumerable<T> Items { get; set; } = Enumerable.Empty<T>();

    /// <summary>
    /// Gets or sets the total number of items across all pages.
    /// </summary>
    public int TotalItems { get; set; }

    /// <summary>
    /// Gets or sets the current page number (1-based).
    /// </summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Gets the total number of pages.
    /// </summary>
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);

    /// <summary>
    /// Gets a value indicating whether there is a previous page.
    /// </summary>
    public bool HasPreviousPage => PageNumber > 1;

    /// <summary>
    /// Gets a value indicating whether there is a next page.
    /// </summary>
    public bool HasNextPage => PageNumber < TotalPages;
}

// ... (continuing with remaining classes for brevity) ...

/// <summary>
/// Options for bulk insert operations.
/// </summary>
public class BulkInsertOptions
{
    /// <summary>
    /// Gets or sets the batch size for bulk operations.
    /// </summary>
    public int BatchSize { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the timeout for the bulk operation.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to check constraints.
    /// </summary>
    public bool CheckConstraints { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to fire triggers.
    /// </summary>
    public bool FireTriggers { get; set; } = true;
}

/// <summary>
/// Options for bulk update operations.
/// </summary>
public class BulkUpdateOptions : BulkInsertOptions
{
    /// <summary>
    /// Gets or sets the columns to use for matching existing records.
    /// </summary>
    public string[] KeyColumns { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the columns to update (all if not specified).
    /// </summary>
    public string[]? UpdateColumns { get; set; }
}

/// <summary>
/// Options for bulk delete operations.
/// </summary>
public class BulkDeleteOptions : BulkInsertOptions
{
    /// <summary>
    /// Gets or sets the columns to use for matching records to delete.
    /// </summary>
    public string[] KeyColumns { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Represents database migration information.
/// </summary>
public class MigrationInfo
{
    /// <summary>
    /// Gets or sets the migration version.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the migration name/description.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the migration has been applied.
    /// </summary>
    public bool IsApplied { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the migration was applied.
    /// </summary>
    public DateTimeOffset? AppliedAt { get; set; }

    /// <summary>
    /// Gets or sets the checksum of the migration script.
    /// </summary>
    public string? Checksum { get; set; }
}

/// <summary>
/// Represents the result of a migration operation.
/// </summary>
public class MigrationResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the migration was successful.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Gets or sets the migrations that were applied.
    /// </summary>
    public IList<string> AppliedMigrations { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the error message if the migration failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets the total migration time.
    /// </summary>
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Options for database backup operations.
/// </summary>
public class DatabaseBackupOptions
{
    /// <summary>
    /// Gets or sets the backup file path.
    /// </summary>
    public string BackupPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the backup type.
    /// </summary>
    public DatabaseBackupType BackupType { get; set; } = DatabaseBackupType.Full;

    /// <summary>
    /// Gets or sets a value indicating whether to compress the backup.
    /// </summary>
    public bool Compress { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to verify the backup after creation.
    /// </summary>
    public bool Verify { get; set; } = true;

    /// <summary>
    /// Gets or sets additional backup options.
    /// </summary>
    public IDictionary<string, object> AdditionalOptions { get; set; } = new Dictionary<string, object>();
}

/// <summary>
/// Enumeration of database backup types.
/// </summary>
public enum DatabaseBackupType
{
    /// <summary>
    /// Full backup of the entire database.
    /// </summary>
    Full,

    /// <summary>
    /// Incremental backup of changes since the last backup.
    /// </summary>
    Incremental,

    /// <summary>
    /// Differential backup of changes since the last full backup.
    /// </summary>
    Differential,

    /// <summary>
    /// Transaction log backup.
    /// </summary>
    Log
}

/// <summary>
/// Represents the result of a backup operation.
/// </summary>
public class BackupResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the backup was successful.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Gets or sets the backup file path.
    /// </summary>
    public string? BackupPath { get; set; }

    /// <summary>
    /// Gets or sets the backup size in bytes.
    /// </summary>
    public long BackupSizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the backup duration.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets the error message if the backup failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets the backup timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Options for database restore operations.
/// </summary>
public class DatabaseRestoreOptions
{
    /// <summary>
    /// Gets or sets the backup file path to restore from.
    /// </summary>
    public string BackupPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target database name.
    /// </summary>
    public string? TargetDatabase { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to replace the existing database.
    /// </summary>
    public bool ReplaceDatabase { get; set; }

    /// <summary>
    /// Gets or sets additional restore options.
    /// </summary>
    public IDictionary<string, object> AdditionalOptions { get; set; } = new Dictionary<string, object>();
}

/// <summary>
/// Represents the result of a restore operation.
/// </summary>
public class RestoreResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the restore was successful.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Gets or sets the restore duration.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets the error message if the restore failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets the restore timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Represents the health status of a database connection.
/// </summary>
public class DatabaseHealthStatus
{
    /// <summary>
    /// Gets or sets the health status.
    /// </summary>
    public HealthStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the health check duration.
    /// </summary>
    public TimeSpan CheckDuration { get; set; }

    /// <summary>
    /// Gets or sets the error message if the health check failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets additional health data.
    /// </summary>
    public IDictionary<string, object> Data { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets the timestamp when the health check was performed.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Enumeration of health status values.
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// The database is healthy and functioning normally.
    /// </summary>
    Healthy,

    /// <summary>
    /// The database is functioning but with degraded performance.
    /// </summary>
    Degraded,

    /// <summary>
    /// The database is not functioning properly.
    /// </summary>
    Unhealthy
}

/// <summary>
/// Contains database statistics and performance metrics.
/// </summary>
public class DatabaseStatistics
{
    /// <summary>
    /// Gets or sets the number of active connections.
    /// </summary>
    public int ActiveConnections { get; set; }

    /// <summary>
    /// Gets or sets the total number of connections created.
    /// </summary>
    public long TotalConnections { get; set; }

    /// <summary>
    /// Gets or sets the number of queries executed.
    /// </summary>
    public long QueriesExecuted { get; set; }

    /// <summary>
    /// Gets or sets the average query execution time.
    /// </summary>
    public TimeSpan AverageQueryTime { get; set; }

    /// <summary>
    /// Gets or sets the database size in bytes.
    /// </summary>
    public long DatabaseSizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the number of tables in the database.
    /// </summary>
    public int TableCount { get; set; }

    /// <summary>
    /// Gets or sets the database uptime.
    /// </summary>
    public TimeSpan? Uptime { get; set; }

    /// <summary>
    /// Gets or sets additional provider-specific statistics.
    /// </summary>
    public IDictionary<string, object> ProviderStats { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets the timestamp when these statistics were collected.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}