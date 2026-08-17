namespace Helichrysum.Core.Manifest;

using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.Sqlite;

/// <summary>
/// Manages the SQLite manifest database — the single source of truth
/// for all scan results, analysis, and relations.
/// </summary>
public sealed class ManifestRepository : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly object _lock = new();

    private const int CurrentSchemaVersion = 1;

    private ManifestRepository(SqliteConnection connection)
    {
        _connection = connection;
        _connection.Open();
        Initialize();
    }

    /// <summary>
    /// Opens or creates a manifest database at the given path.
    /// </summary>
    /// <param name="path">The file path to the SQLite database.</param>
    /// <returns>A new ManifestRepository instance.</returns>
    public static ManifestRepository Open(string path)
    {
        string? directory = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }

        var connection = new SqliteConnection($"Data Source={path}");
        return new ManifestRepository(connection);
    }

    private void Initialize()
    {
        // Apply performance pragmas.
        Execute("PRAGMA journal_mode = WAL;");
        Execute("PRAGMA synchronous = NORMAL;");
        Execute("PRAGMA cache_size = -200000;");
        Execute("PRAGMA temp_store = MEMORY;");
        Execute("PRAGMA foreign_keys = OFF;");

        // Create schema version table.
        Execute("""
            CREATE TABLE IF NOT EXISTS _schema_version (
                version INTEGER PRIMARY KEY,
                applied_at TEXT NOT NULL
            );
            """);

        int currentVersion = GetSchemaVersion();

        if (currentVersion < 1)
        {
            ApplyV1Schema();
            SetSchemaVersion(1);
        }
    }

    private void ApplyV1Schema()
    {
        Execute("""
            CREATE TABLE IF NOT EXISTS _manifest_meta (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """);

        Execute("""
            CREATE TABLE IF NOT EXISTS scopes (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                root_path TEXT NOT NULL,
                canonical TEXT NOT NULL,
                added_at TEXT NOT NULL
            );
            """);

        Execute("""
            CREATE TABLE IF NOT EXISTS objects (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                scope_id INTEGER NOT NULL REFERENCES scopes(id),
                path TEXT NOT NULL,
                canonical_path TEXT NOT NULL,
                kind TEXT NOT NULL,
                size INTEGER,
                mtime TEXT,
                ctime TEXT,
                inode_group INTEGER,
                device_id INTEGER NOT NULL,
                scope_relation TEXT NOT NULL
            );
            """);

        Execute("""
            CREATE INDEX IF NOT EXISTS idx_objects_size
                ON objects(size) WHERE size IS NOT NULL;
            """);

        Execute("""
            CREATE INDEX IF NOT EXISTS idx_objects_inode
                ON objects(inode_group) WHERE inode_group IS NOT NULL;
            """);

        Execute("""
            CREATE TABLE IF NOT EXISTS hashes (
                object_id INTEGER PRIMARY KEY REFERENCES objects(id),
                tier TEXT NOT NULL,
                hash_value TEXT,
                bytes_read INTEGER NOT NULL,
                computed_at TEXT NOT NULL
            );
            """);

        Execute("""
            CREATE TABLE IF NOT EXISTS relations (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                kind TEXT NOT NULL,
                confidence REAL NOT NULL,
                evidence TEXT NOT NULL
            );
            """);

        Execute("""
            CREATE TABLE IF NOT EXISTS relation_members (
                relation_id INTEGER NOT NULL REFERENCES relations(id),
                object_id INTEGER NOT NULL REFERENCES objects(id),
                role TEXT,
                PRIMARY KEY (relation_id, object_id)
            );
            """);

        Execute("""
            CREATE INDEX IF NOT EXISTS idx_relation_members_obj
                ON relation_members(object_id);
            """);

        Execute("""
            CREATE TABLE IF NOT EXISTS scan_state (
                scope_id INTEGER PRIMARY KEY,
                last_path TEXT,
                status TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            """);
    }

    /// <summary>
    /// Gets the current schema version from the database.
    /// </summary>
    public int GetSchemaVersion()
    {
        try
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "SELECT COALESCE(MAX(version), 0) FROM _schema_version;";
            object? result = command.ExecuteScalar();
            return Convert.ToInt32(result);
        }
        catch
        {
            return 0;
        }
    }

    private void SetSchemaVersion(int version)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "INSERT INTO _schema_version (version, applied_at) VALUES ($v, $t);";
        command.Parameters.AddWithValue("$v", version);
        command.Parameters.AddWithValue("$t", DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Inserts a single filesystem object and returns its assigned ID.
    /// </summary>
    public long InsertObject(FilesystemObject obj)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            INSERT INTO objects (scope_id, path, canonical_path, kind, size, mtime, ctime,
                                 inode_group, device_id, scope_relation)
            VALUES ($sid, $p, $cp, $k, $sz, $mt, $ct, $ig, $did, $sr);
            SELECT last_insert_rowid();
            """;

        command.Parameters.AddWithValue("$sid", obj.ScopeId);
        command.Parameters.AddWithValue("$p", obj.Path);
        command.Parameters.AddWithValue("$cp", obj.CanonicalPath);
        command.Parameters.AddWithValue("$k", obj.Kind);
        command.Parameters.AddWithValue("$sz", (object?)obj.Size ?? DBNull.Value);
        command.Parameters.AddWithValue("$mt", (object?)obj.ModifiedTime?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("$ct", (object?)obj.CreatedTime?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("$ig", (object?)obj.InodeGroup ?? DBNull.Value);
        command.Parameters.AddWithValue("$did", (long)obj.DeviceId);
        command.Parameters.AddWithValue("$sr", obj.ScopeRelation);

        object? result = command.ExecuteScalar();
        return Convert.ToInt64(result);
    }

    /// <summary>
    /// Inserts a batch of filesystem objects in a single transaction.
    /// </summary>
    public void BatchInsertObjects(IEnumerable<FilesystemObject> objects)
    {
        lock (_lock)
        {
            using var transaction = _connection.BeginTransaction();
            using var command = _connection.CreateCommand();
            command.CommandText = """
                INSERT INTO objects (scope_id, path, canonical_path, kind, size, mtime, ctime,
                                     inode_group, device_id, scope_relation)
                VALUES ($sid, $p, $cp, $k, $sz, $mt, $ct, $ig, $did, $sr);
                """;

            var sidParam = command.Parameters.Add("$sid", SqliteType.Integer);
            var pParam = command.Parameters.Add("$p", SqliteType.Text);
            var cpParam = command.Parameters.Add("$cp", SqliteType.Text);
            var kParam = command.Parameters.Add("$k", SqliteType.Text);
            var szParam = command.Parameters.Add("$sz", SqliteType.Integer);
            var mtParam = command.Parameters.Add("$mt", SqliteType.Text);
            var ctParam = command.Parameters.Add("$ct", SqliteType.Text);
            var igParam = command.Parameters.Add("$ig", SqliteType.Integer);
            var didParam = command.Parameters.Add("$did", SqliteType.Integer);
            var srParam = command.Parameters.Add("$sr", SqliteType.Text);

            foreach (var obj in objects)
            {
                sidParam.Value = obj.ScopeId;
                pParam.Value = obj.Path;
                cpParam.Value = obj.CanonicalPath;
                kParam.Value = obj.Kind;
                szParam.Value = (object?)obj.Size ?? DBNull.Value;
                mtParam.Value = (object?)obj.ModifiedTime?.ToString("O") ?? DBNull.Value;
                ctParam.Value = (object?)obj.CreatedTime?.ToString("O") ?? DBNull.Value;
                igParam.Value = (object?)obj.InodeGroup ?? DBNull.Value;
                didParam.Value = (long)obj.DeviceId;
                srParam.Value = obj.ScopeRelation;
                command.ExecuteNonQuery();
            }

            transaction.Commit();
        }
    }

    /// <summary>
    /// Inserts a hash record for a filesystem object.
    /// </summary>
    public void InsertHash(HashRecord hash)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            INSERT OR REPLACE INTO hashes (object_id, tier, hash_value, bytes_read, computed_at)
            VALUES ($oid, $t, $hv, $br, $ca);
            """;

        command.Parameters.AddWithValue("$oid", hash.ObjectId);
        command.Parameters.AddWithValue("$t", hash.Tier);
        command.Parameters.AddWithValue("$hv", (object?)hash.HashValue ?? DBNull.Value);
        command.Parameters.AddWithValue("$br", hash.BytesRead);
        command.Parameters.AddWithValue("$ca", hash.ComputedAt.ToString("O"));
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Gets the hash value for a specific object by its ID.
    /// </summary>
    public string? GetHashByObjectId(long objectId)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT hash_value FROM hashes WHERE object_id = $oid;";
        command.Parameters.AddWithValue("$oid", objectId);

        object? result = command.ExecuteScalar();
        return result as string;
    }

    /// <summary>
    /// Gets all filesystem objects that are regular files.
    /// </summary>
    /// <returns>List of all regular file objects.</returns>
    public List<FilesystemObject> GetAllFiles()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT id, scope_id, path, canonical_path, kind, size, scope_relation FROM objects WHERE kind = 'RegularFile' ORDER BY path;";

        var results = new List<FilesystemObject>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new FilesystemObject
            {
                Id = reader.GetInt64(0),
                ScopeId = reader.GetInt64(1),
                Path = reader.GetString(2),
                CanonicalPath = reader.GetString(3),
                Kind = reader.GetString(4),
                Size = reader.IsDBNull(5) ? null : reader.GetInt64(5),
                ModifiedTime = null,
                CreatedTime = null,
                InodeGroup = null,
                DeviceId = 0,
                ScopeRelation = reader.GetString(6),
                LinkTarget = null,
                ResolvedLinkTarget = null,
            });
        }

        return results;
    }

    /// <summary>
    /// Gets all directory objects with their child counts.
    /// </summary>
    /// <returns>A dictionary mapping directory path to a count of its direct regular-file children.</returns>
    public Dictionary<string, int> GetDirectoryTree()
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // Get all regular file paths and derive directory structure.
        var allFiles = GetAllFiles();
        foreach (var file in allFiles)
        {
            string? dir = System.IO.Path.GetDirectoryName(file.Path);

            // Mark each ancestor directory with +1 file count.
            while (!string.IsNullOrEmpty(dir))
            {
                result.TryGetValue(dir, out int count);
                result[dir] = count + 1;
                string? parent = System.IO.Path.GetDirectoryName(dir);
                if (parent == dir) break;
                dir = parent;
            }
        }

        return result;
    }

    /// <summary>
    /// Queries objects by exact size.
    /// </summary>
    /// <param name="size">The file size to match.</param>
    /// <returns>List of matching filesystem objects.</returns>
    public List<FilesystemObject> QueryObjectsBySize(long size)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT id, scope_id, path, canonical_path, kind, size, scope_relation FROM objects WHERE size = $sz;";
        command.Parameters.AddWithValue("$sz", size);

        var results = new List<FilesystemObject>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new FilesystemObject
            {
                Id = reader.GetInt64(0),
                ScopeId = reader.GetInt64(1),
                Path = reader.GetString(2),
                CanonicalPath = reader.GetString(3),
                Kind = reader.GetString(4),
                Size = reader.IsDBNull(5) ? null : reader.GetInt64(5),
                ModifiedTime = null,
                CreatedTime = null,
                InodeGroup = null,
                DeviceId = 0,
                ScopeRelation = reader.GetString(6),
                LinkTarget = null,
                ResolvedLinkTarget = null,
            });
        }

        return results;
    }

    /// <summary>
    /// Queries objects by their hash value.
    /// </summary>
    /// <param name="hashValue">The hash value to match.</param>
    /// <returns>List of matching filesystem objects.</returns>
    public List<FilesystemObject> QueryObjectsByHash(string hashValue)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT o.id, o.scope_id, o.path, o.canonical_path, o.kind, o.size, o.scope_relation
            FROM objects o
            INNER JOIN hashes h ON h.object_id = o.id
            WHERE h.hash_value = $hv;
            """;

        command.Parameters.AddWithValue("$hv", hashValue);

        var results = new List<FilesystemObject>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new FilesystemObject
            {
                Id = reader.GetInt64(0),
                ScopeId = reader.GetInt64(1),
                Path = reader.GetString(2),
                CanonicalPath = reader.GetString(3),
                Kind = reader.GetString(4),
                Size = reader.IsDBNull(5) ? null : reader.GetInt64(5),
                ModifiedTime = null,
                CreatedTime = null,
                InodeGroup = null,
                DeviceId = 0,
                ScopeRelation = reader.GetString(6),
                LinkTarget = null,
                ResolvedLinkTarget = null,
            });
        }

        return results;
    }

    /// <summary>
    /// Gets all duplicate groups (objects grouped by identical full hash).
    /// </summary>
    /// <returns>List of duplicate groups, each containing at least 2 members.</returns>
    public List<DuplicateGroup> GetDuplicateGroups()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT h.hash_value, COUNT(*), MAX(o.size), GROUP_CONCAT(o.id)
            FROM hashes h
            INNER JOIN objects o ON o.id = h.object_id
            WHERE h.tier = 'FullHash' AND h.hash_value IS NOT NULL
            GROUP BY h.hash_value
            HAVING COUNT(*) >= 2
            ORDER BY COUNT(*) DESC;
            """;

        var groups = new List<DuplicateGroup>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            string hashValue = reader.GetString(0);
            int count = reader.GetInt32(1);
            long size = reader.IsDBNull(2) ? 0 : reader.GetInt64(2);
            string membersCsv = reader.GetString(3);

            var members = new List<long>();
            foreach (string id in membersCsv.Split(','))
            {
                if (long.TryParse(id, out long parsed))
                {
                    members.Add(parsed);
                }
            }

            groups.Add(new DuplicateGroup
            {
                HashValue = hashValue,
                Members = members,
                Size = size,
                Count = count,
            });
        }

        return groups;
    }

    /// <summary>
    /// Inserts a relation with its member objects.
    /// </summary>
    /// <param name="relation">The relation to insert.</param>
    /// <param name="memberIds">The object IDs that are members of this relation.</param>
    /// <returns>The assigned relation ID.</returns>
    public long InsertRelation(Relation relation, List<long> memberIds)
    {
        using var transaction = _connection.BeginTransaction();
        try
        {
            long relationId;
            using (var command = _connection.CreateCommand())
            {
                command.CommandText = """
                    INSERT INTO relations (kind, confidence, evidence)
                    VALUES ($k, $c, $e);
                    SELECT last_insert_rowid();
                    """;

                command.Parameters.AddWithValue("$k", relation.Kind);
                command.Parameters.AddWithValue("$c", relation.Confidence);
                command.Parameters.AddWithValue("$e", relation.Evidence);
                relationId = Convert.ToInt64(command.ExecuteScalar());
            }

            foreach (long memberId in memberIds)
            {
                using var command = _connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO relation_members (relation_id, object_id)
                    VALUES ($rid, $oid);
                    """;

                command.Parameters.AddWithValue("$rid", relationId);
                command.Parameters.AddWithValue("$oid", memberId);
                command.ExecuteNonQuery();
            }

            transaction.Commit();
            return relationId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Saves the scan state for resumption.
    /// </summary>
    public void SaveScanState(long scopeId, string? lastPath, string status)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            INSERT OR REPLACE INTO scan_state (scope_id, last_path, status, updated_at)
            VALUES ($sid, $lp, $s, $ua);
            """;

        command.Parameters.AddWithValue("$sid", scopeId);
        command.Parameters.AddWithValue("$lp", (object?)lastPath ?? DBNull.Value);
        command.Parameters.AddWithValue("$s", status);
        command.Parameters.AddWithValue("$ua", DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Gets the current scan state for a scope.
    /// </summary>
    public ScanState? GetScanState(long scopeId)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT scope_id, last_path, status, updated_at FROM scan_state WHERE scope_id = $sid;";
        command.Parameters.AddWithValue("$sid", scopeId);

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return new ScanState
            {
                ScopeId = reader.GetInt64(0),
                LastPath = reader.IsDBNull(1) ? null : reader.GetString(1),
                Status = reader.GetString(2),
                UpdatedAt = DateTimeOffset.Parse(reader.GetString(3)),
            };
        }

        return null;
    }

    /// <summary>
    /// Sets a manifest metadata key-value pair.
    /// </summary>
    public void SetManifestMeta(string key, string value)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "INSERT OR REPLACE INTO _manifest_meta (key, value) VALUES ($k, $v);";
        command.Parameters.AddWithValue("$k", key);
        command.Parameters.AddWithValue("$v", value);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Gets a manifest metadata value by key.
    /// </summary>
    public string? GetManifestMeta(string key)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT value FROM _manifest_meta WHERE key = $k;";
        command.Parameters.AddWithValue("$k", key);

        object? result = command.ExecuteScalar();
        return result as string;
    }

    private void Execute(string sql)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Disposes the repository, closing the database connection.
    /// </summary>
    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }
}