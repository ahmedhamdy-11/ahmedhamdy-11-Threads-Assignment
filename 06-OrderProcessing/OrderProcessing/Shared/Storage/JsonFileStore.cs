using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OrderProcessing.Shared.Exceptions;

namespace OrderProcessing.Shared.Storage;

public class JsonFileStore
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _dataDir;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new();
    private readonly ConcurrentDictionary<string, object> _syncLocks = new();

    public JsonFileStore(string? dataDir = null)
    {
        _dataDir = dataDir ?? Path.Combine(AppContext.BaseDirectory, "Data");
    }

    private SemaphoreSlim GetSemaphore(string fileName)
    {
        return _semaphores.GetOrAdd(fileName.ToLowerInvariant(), _ => new SemaphoreSlim(1, 1));
    }

    private object GetSyncLock(string fileName)
    {
        return _syncLocks.GetOrAdd(fileName.ToLowerInvariant(), _ => new object());
    }

    private string GetFilePath(string fileName) => Path.Combine(_dataDir, fileName);

    public async Task<List<T>> ReadListAsync<T>(string fileName, CancellationToken ct = default)
    {
        var sem = GetSemaphore(fileName);
        await sem.WaitAsync(ct);
        try
        {
            string path = GetFilePath(fileName);
            if (!File.Exists(path))
                return new List<T>();

            string json = await File.ReadAllTextAsync(path, Encoding.UTF8, ct);
            if (string.IsNullOrWhiteSpace(json) || json.Trim() == "[]")
                return new List<T>();

            try
            {
                return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? new List<T>();
            }
            catch (JsonException ex)
            {
                throw new DataFileCorruptedException(fileName, ex);
            }
        }
        finally
        {
            sem.Release();
        }
    }

    public async Task WriteListAsync<T>(string fileName, List<T> items, CancellationToken ct = default)
    {
        var sem = GetSemaphore(fileName);
        await sem.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(_dataDir);
            string targetPath = GetFilePath(fileName);
            string tempPath = Path.Combine(_dataDir, $"{fileName}.{Guid.NewGuid():N}.tmp");

            string json = JsonSerializer.Serialize(items ?? new List<T>(), JsonOptions);
            await File.WriteAllTextAsync(tempPath, json, Encoding.UTF8, ct);

            File.Move(tempPath, targetPath, overwrite: true);
        }
        finally
        {
            sem.Release();
        }
    }

    public async Task UpdateListAsync<T>(string fileName, Action<List<T>> updateAction, CancellationToken ct = default)
    {
        var sem = GetSemaphore(fileName);
        await sem.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(_dataDir);
            string targetPath = GetFilePath(fileName);
            List<T> items;

            if (File.Exists(targetPath))
            {
                string json = await File.ReadAllTextAsync(targetPath, Encoding.UTF8, ct);
                if (string.IsNullOrWhiteSpace(json) || json.Trim() == "[]")
                {
                    items = new List<T>();
                }
                else
                {
                    try
                    {
                        items = JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? new List<T>();
                    }
                    catch (JsonException ex)
                    {
                        throw new DataFileCorruptedException(fileName, ex);
                    }
                }
            }
            else
            {
                items = new List<T>();
            }

            updateAction(items);

            string tempPath = Path.Combine(_dataDir, $"{fileName}.{Guid.NewGuid():N}.tmp");
            string newJson = JsonSerializer.Serialize(items, JsonOptions);
            await File.WriteAllTextAsync(tempPath, newJson, Encoding.UTF8, ct);
            File.Move(tempPath, targetPath, overwrite: true);
        }
        finally
        {
            sem.Release();
        }
    }

    // Synchronous methods for comparison runners
    public List<T> ReadList<T>(string fileName)
    {
        lock (GetSyncLock(fileName))
        {
            string path = GetFilePath(fileName);
            if (!File.Exists(path))
                return new List<T>();

            string json = File.ReadAllText(path, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json) || json.Trim() == "[]")
                return new List<T>();

            try
            {
                return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? new List<T>();
            }
            catch (JsonException ex)
            {
                throw new DataFileCorruptedException(fileName, ex);
            }
        }
    }

    public void WriteList<T>(string fileName, List<T> items)
    {
        lock (GetSyncLock(fileName))
        {
            Directory.CreateDirectory(_dataDir);
            string targetPath = GetFilePath(fileName);
            string tempPath = Path.Combine(_dataDir, $"{fileName}.{Guid.NewGuid():N}.tmp");

            string json = JsonSerializer.Serialize(items ?? new List<T>(), JsonOptions);
            File.WriteAllText(tempPath, json, Encoding.UTF8);
            File.Move(tempPath, targetPath, overwrite: true);
        }
    }

    public void UpdateList<T>(string fileName, Action<List<T>> updateAction)
    {
        lock (GetSyncLock(fileName))
        {
            Directory.CreateDirectory(_dataDir);
            string targetPath = GetFilePath(fileName);
            List<T> items;

            if (File.Exists(targetPath))
            {
                string json = File.ReadAllText(targetPath, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json) || json.Trim() == "[]")
                {
                    items = new List<T>();
                }
                else
                {
                    try
                    {
                        items = JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? new List<T>();
                    }
                    catch (JsonException ex)
                    {
                        throw new DataFileCorruptedException(fileName, ex);
                    }
                }
            }
            else
            {
                items = new List<T>();
            }

            updateAction(items);

            string tempPath = Path.Combine(_dataDir, $"{fileName}.{Guid.NewGuid():N}.tmp");
            string newJson = JsonSerializer.Serialize(items, JsonOptions);
            File.WriteAllText(tempPath, newJson, Encoding.UTF8);
            File.Move(tempPath, targetPath, overwrite: true);
        }
    }
}
