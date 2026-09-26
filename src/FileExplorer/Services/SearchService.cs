using System.Data.OleDb;
using System.IO;
using System.Runtime.Versioning;
using FileExplorer.Models;

namespace FileExplorer.Services;

/// <summary>
/// Folder search. Uses the Windows Search index when the folder is indexed &mdash; that is what
/// makes Explorer's search feel instant &mdash; and falls back to a plain recursive walk
/// otherwise (or when the Windows Search service is not installed, e.g. on Server SKUs).
/// </summary>
[SupportedOSPlatform("windows")]
public static class SearchService
{
    private const string ConnectionString =
        "Provider=Search.CollatorDSO;Extended Properties=\"Application=Windows\"";

    public static async Task SearchAsync(
        string root,
        string query,
        bool showHidden,
        Action<IReadOnlyList<FileSystemEntry>> onBatch,
        CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(query) || KnownLocations.IsVirtual(root))
            return;

        var indexed = await Task.Run(() => TrySearchIndex(root, query, onBatch, token), token);
        if (!indexed)
            await Task.Run(() => SearchFileSystem(root, query, showHidden, onBatch, token), token);
    }

    private static bool TrySearchIndex(
        string root, string query, Action<IReadOnlyList<FileSystemEntry>> onBatch, CancellationToken token)
    {
        try
        {
            using var connection = new OleDbConnection(ConnectionString);
            connection.Open();

            // LIKE patterns are passed as parameters; the scope is escaped by doubling quotes
            // because Search.CollatorDSO does not accept a parameter for SCOPE.
            var scope = "file:" + root.Replace("'", "''");
            var sql =
                "SELECT System.ItemPathDisplay, System.ItemNameDisplay, System.Size, " +
                "System.DateModified, System.ItemType " +
                $"FROM SystemIndex WHERE SCOPE='{scope}' AND System.ItemNameDisplay LIKE ? " +
                "ORDER BY System.ItemPathDisplay";

            using var command = new OleDbCommand(sql, connection);
            command.Parameters.Add(new OleDbParameter("name", "%" + query + "%"));

            using var reader = command.ExecuteReader();
            var batch = new List<FileSystemEntry>(64);
            var found = 0;

            while (reader.Read())
            {
                token.ThrowIfCancellationRequested();

                if (reader.IsDBNull(0))
                    continue;

                var path = reader.GetString(0);
                var name = reader.IsDBNull(1) ? Path.GetFileName(path) : reader.GetString(1);
                var size = reader.IsDBNull(2) ? -1L : Convert.ToInt64(reader.GetValue(2));
                // The index stores dates in UTC; the listing shows local LastWriteTime.
                var modified = reader.IsDBNull(3)
                    ? default
                    : DateTime.SpecifyKind(reader.GetDateTime(3), DateTimeKind.Utc).ToLocalTime();
                var isDirectory = !reader.IsDBNull(4) && reader.GetString(4) == "Directory";

                batch.Add(new FileSystemEntry(
                    path, name,
                    isDirectory ? EntryKind.Directory : EntryKind.File,
                    isDirectory ? -1 : size, modified,
                    isDirectory ? FileAttributes.Directory : FileAttributes.Normal));

                found++;

                if (batch.Count >= 64)
                {
                    onBatch(batch);
                    batch = new List<FileSystemEntry>(64);
                }
            }

            if (batch.Count > 0)
                onBatch(batch);

            // A folder outside the index answers with an empty result set rather than an
            // error, which is indistinguishable from "nothing matched" - so an empty answer
            // is treated as "not indexed" and the file system walk runs instead.
            return found > 0;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // No index, service disabled, or the folder is not covered: use the fallback.
            return false;
        }
    }

    private static void SearchFileSystem(
        string root, string query, bool showHidden, Action<IReadOnlyList<FileSystemEntry>> onBatch, CancellationToken token)
    {
        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true,
            AttributesToSkip = showHidden ? 0 : FileAttributes.System,
        };

        var batch = new List<FileSystemEntry>(64);

        try
        {
            foreach (var info in new DirectoryInfo(root).EnumerateFileSystemInfos("*", options))
            {
                token.ThrowIfCancellationRequested();

                if (info.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) == false)
                    continue;

                // As in the directory listing: a single unreadable entry is skipped rather
                // than cutting the walk short.
                try
                {
                    var attributes = info.Attributes;
                    if (!showHidden && (attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0)
                        continue;

                    var isDirectory = (attributes & FileAttributes.Directory) != 0;

                    batch.Add(new FileSystemEntry(
                        info.FullName, info.Name,
                        isDirectory ? EntryKind.Directory : EntryKind.File,
                        isDirectory ? -1 : ((FileInfo)info).Length,
                        info.LastWriteTime, attributes));
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }

                if (batch.Count >= 64)
                {
                    onBatch(batch);
                    batch = new List<FileSystemEntry>(64);
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Partial results are better than an error dialog mid-search.
        }

        if (batch.Count > 0)
            onBatch(batch);
    }
}
