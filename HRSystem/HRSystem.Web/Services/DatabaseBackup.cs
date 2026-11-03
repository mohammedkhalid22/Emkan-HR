using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace HRSystem.Web.Services;

public class BackupOptions
{
    public const string Section = "Backup";

    public bool Enabled { get; set; } = true;

    // A folder on the SQL Server machine that the SQL Server service can write to.
    // Empty = SQL Server's own default backup folder, which always works.
    public string? Folder { get; set; }

    // How many backup files to keep; older ones are deleted after each backup
    public int KeepCount { get; set; } = 14;

    // The daily backup runs at or after this hour (server time)
    public int AtHour { get; set; } = 2;
}

// Exists is null when the app may not look into the folder (it belongs to SQL Server)
public record BackupFile(string Path, DateTime FinishedAt, long SizeBytes, bool? Exists)
{
    public string Name => System.IO.Path.GetFileName(Path);
}

public record BackupResult(bool Succeeded, string? File, string? Error);

// What the last run did; kept in memory, the history itself comes from SQL Server (msdb)
public class BackupState
{
    public DateTime? LastAttemptAt { get; set; }
    public BackupResult? LastResult { get; set; }
    public string? PruneWarning { get; set; }
}

// Full database backups with SQL Server's own BACKUP DATABASE. Needs a server where the app's
// login may run BACKUP (own server, VPS); shared hosting usually refuses it.
public class DatabaseBackup
{
    public const string FilePrefix = "EmkanHR_";

    private readonly string _connectionString;
    private readonly BackupOptions _options;
    private readonly BackupState _state;
    private readonly ILogger<DatabaseBackup> _logger;

    private static readonly SemaphoreSlim Running = new(1, 1);

    public DatabaseBackup(IConfiguration configuration, IOptions<BackupOptions> options, BackupState state, ILogger<DatabaseBackup> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        _options = options.Value;
        _state = state;
        _logger = logger;
    }

    public BackupOptions Options => _options;
    public BackupState State => _state;
    public string DatabaseName => new SqlConnectionStringBuilder(_connectionString).InitialCatalog;

    public async Task<BackupResult> RunAsync(CancellationToken cancellationToken = default)
    {
        if (!await Running.WaitAsync(0, cancellationToken))
            return new BackupResult(false, null, "فيه نسخة بتتاخد دلوقتي، استنى لما تخلص");

        try
        {
            _state.LastAttemptAt = DateTime.Now;
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var folder = string.IsNullOrWhiteSpace(_options.Folder) ? await DefaultFolderAsync(connection, cancellationToken) : _options.Folder!;
            var file = Path.Combine(folder, $"{FilePrefix}{DatabaseName}_{DateTime.Now:yyyyMMdd_HHmmss}.bak");

            try
            {
                await BackupAsync(connection, file, compress: true, cancellationToken);
            }
            catch (SqlException ex) when (ex.Number == 1844)
            {
                // This edition (Express) can't compress
                await BackupAsync(connection, file, compress: false, cancellationToken);
            }

            _logger.LogInformation("Database backup written to {File}", file);
            _state.PruneWarning = Prune(folder);
            return _state.LastResult = new BackupResult(true, file, null);
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "Database backup failed");
            var error = ex.Number switch
            {
                262 or 916 or 229 => "حساب قاعدة البيانات مالوش صلاحية ياخد نسخة (BACKUP). ده المعتاد في الاستضافة المشتركة: خد النسخة من لوحة تحكم الاستضافة.",
                3201 or 3013 => "SQL Server مش قادر يكتب في فولدر النسخ. اتأكد إن الفولدر موجود وإن خدمة SQL Server ليها صلاحية الكتابة فيه، أو سيب الإعداد Folder فاضي.",
                _ => ex.Message
            };
            return _state.LastResult = new BackupResult(false, null, error);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Database backup failed");
            return _state.LastResult = new BackupResult(false, null, ex.Message);
        }
        finally
        {
            Running.Release();
        }
    }

    // Recent full backups of this database, newest first
    public async Task<List<BackupFile>> HistoryAsync(int take = 20)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("""
            SELECT TOP (@take) f.physical_device_name, s.backup_finish_date, s.backup_size
            FROM msdb.dbo.backupset s
            JOIN msdb.dbo.backupmediafamily f ON f.media_set_id = s.media_set_id
            WHERE s.database_name = @db AND s.type = 'D'
            ORDER BY s.backup_finish_date DESC
            """, connection);
        command.Parameters.AddWithValue("@take", take);
        command.Parameters.AddWithValue("@db", DatabaseName);

        var result = new List<BackupFile>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var path = reader.GetString(0);
            result.Add(new BackupFile(path, reader.GetDateTime(1), (long)reader.GetDecimal(2), SafeExists(path)));
        }
        return result;
    }

    // A file the app may hand out: only our own backups, only from the backup folder
    public async Task<string?> FindFileAsync(string name)
    {
        if (name != Path.GetFileName(name) || !name.StartsWith(FilePrefix) || !name.EndsWith(".bak"))
            return null;
        var match = (await HistoryAsync(200)).FirstOrDefault(b => b.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return match is { Exists: not false } ? match.Path : null;
    }

    private static async Task BackupAsync(SqlConnection connection, string file, bool compress, CancellationToken cancellationToken)
    {
        // The database name can't be a parameter; QUOTENAME keeps it safe
        await using var command = new SqlCommand($"""
            DECLARE @sql nvarchar(max) = N'BACKUP DATABASE ' + QUOTENAME(DB_NAME()) +
                N' TO DISK = @file WITH INIT, CHECKSUM{(compress ? ", COMPRESSION" : "")}, NAME = N''Emkan HR daily backup''';
            EXEC sp_executesql @sql, N'@file nvarchar(4000)', @file = @file;
            """, connection) { CommandTimeout = 0 };
        command.Parameters.AddWithValue("@file", file);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<string> DefaultFolderAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(4000))", connection);
        return (await command.ExecuteScalarAsync(cancellationToken)) as string
            ?? throw new InvalidOperationException("SQL Server didn't report its default backup folder; set Backup:Folder");
    }

    // Keeps the newest KeepCount files. The folder belongs to SQL Server and may not be
    // readable by the app (other machine, permissions): then old files stay and we say so.
    private string? Prune(string folder)
    {
        try
        {
            var old = new DirectoryInfo(folder).GetFiles($"{FilePrefix}{DatabaseName}_*.bak")
                .OrderByDescending(f => f.Name)
                .Skip(Math.Max(1, _options.KeepCount));
            foreach (var f in old)
                f.Delete();
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            _logger.LogWarning(ex, "Could not delete old backups in {Folder}", folder);
            return $"النسخة اتاخدت بس مقدرتش أمسح النسخ القديمة من الفولدر ({ex.GetType().Name}). امسحها بإيدك أو ادي البرنامج صلاحية على الفولدر.";
        }
    }

    private static bool? SafeExists(string path)
    {
        // File.Exists says false for a file we just can't see, so ask the folder instead
        try { return Directory.EnumerateFiles(System.IO.Path.GetDirectoryName(path)!, System.IO.Path.GetFileName(path)).Any(); }
        catch (Exception ex) when (ex is DirectoryNotFoundException or IOException) { return false; }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException) { return null; }
    }
}

// Takes one backup a day, after Backup:AtHour. A failed try waits a few hours before the next,
// so a server that refuses BACKUP doesn't fill the log.
public class BackupJob : BackgroundService
{
    private static readonly TimeSpan CheckEvery = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan RetryAfterFailure = TimeSpan.FromHours(6);

    private readonly DatabaseBackup _backup;
    private readonly ILogger<BackupJob> _logger;

    public BackupJob(DatabaseBackup backup, ILogger<BackupJob> logger)
    {
        _backup = backup;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await IsDueAsync())
                    await _backup.RunAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Backup check failed");
            }

            await Task.Delay(CheckEvery, stoppingToken);
        }
    }

    private async Task<bool> IsDueAsync()
    {
        var now = DateTime.Now;
        if (!_backup.Options.Enabled || now.Hour < _backup.Options.AtHour)
            return false;

        var state = _backup.State;
        if (state.LastResult is { Succeeded: false } && now - state.LastAttemptAt < RetryAfterFailure)
            return false;
        if (state.LastResult is { Succeeded: true } && state.LastAttemptAt?.Date == now.Date)
            return false;

        // After a restart: SQL Server remembers whether today's backup was already taken
        try
        {
            var last = (await _backup.HistoryAsync(1)).FirstOrDefault();
            return last is null || last.FinishedAt.Date < now.Date;
        }
        catch (SqlException)
        {
            // msdb not readable (shared hosting): try, the result tells the admin why if it fails
            return state.LastAttemptAt is null || state.LastAttemptAt.Value.Date < now.Date;
        }
    }
}
