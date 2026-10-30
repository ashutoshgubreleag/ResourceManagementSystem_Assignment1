using System.Text.Json;
using ApexPost.Database.Models;
using ApexPost.Repositories.Json;
using ApexPost.Repositories.Specifications;

namespace ApexPost.Repositories;

/// <summary>
/// JSON-file implementation of <see cref="IBaseEntityRepository{TEntity}"/>.
///
/// Design (see DESIGN.md, ADR-W2-05):
/// <list type="bullet">
/// <item>Registered as a <b>singleton</b> per entity type, so there is exactly one
/// <see cref="SemaphoreSlim"/> guarding each file. A <c>lock</c> cannot be held across
/// <c>await</c>; SemaphoreSlim.WaitAsync can, and it does not block a thread-pool thread.</item>
/// <item>The file is read once into an immutable snapshot. Reads are served from memory and
/// complete synchronously (hence <see cref="ValueTask{TResult}"/>).</item>
/// <item>Writes are copy-on-write: build a new list, persist it atomically (write a temp file,
/// then <see cref="File.Move(string,string,bool)"/>), and only then publish the new snapshot, so
/// readers never see half-applied changes and a failed write leaves memory consistent.</item>
/// <item>Callers always receive deep copies, so mutating a returned entity cannot corrupt the
/// cache before it is saved.</item>
/// </list>
/// </summary>
public class BaseJsonRepository<TEntity> : IBaseEntityRepository<TEntity>, IDisposable where TEntity : class, IEntity
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _filePath;
    private IReadOnlyList<TEntity>? _snapshot;

    public BaseJsonRepository(JsonStorageSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _filePath = Path.Combine(settings.DataDirectory, JsonStorageSettings.FileNameFor(typeof(TEntity)));
    }

    public ValueTask<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default) =>
        QueryAsync<object?>(static (items, _) => items, null, cancellationToken);

    public ValueTask<IReadOnlyList<TEntity>> FindAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(specification);
        return QueryAsync(static (items, spec) => items.Where(spec.IsSatisfiedBy), specification, cancellationToken);
    }

    public async ValueTask<TEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var match = snapshot.FirstOrDefault(e => e.Id == id);
        return match is null ? null : Clone(match);
    }

    public async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        TEntity stored = default!;
        await MutateAsync(items =>
        {
            stored = Clone(entity);
            if (stored.Id == 0)
            {
                stored.Id = items.Count == 0 ? 1 : items.Max(e => e.Id) + 1;
            }
            else if (items.Any(e => e.Id == stored.Id))
            {
                throw new InvalidOperationException($"{typeof(TEntity).Name} {stored.Id} already exists.");
            }

            items.Add(stored);
            return true;
        }, cancellationToken).ConfigureAwait(false);

        entity.Id = stored.Id;
        return Clone(stored);
    }

    public Task<bool> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return MutateAsync(items =>
        {
            var index = items.FindIndex(e => e.Id == entity.Id);
            if (index < 0)
            {
                return false;
            }

            items[index] = Clone(entity);
            return true;
        }, cancellationToken);
    }

    public Task<bool> UpdateRangeAsync(IReadOnlyCollection<TEntity> entities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);
        return MutateAsync(items =>
        {
            foreach (var entity in entities)
            {
                var index = items.FindIndex(e => e.Id == entity.Id);
                if (index < 0)
                {
                    return false; // nothing is written: the working copy is discarded
                }

                items[index] = Clone(entity);
            }

            return entities.Count > 0;
        }, cancellationToken);
    }

    public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default) =>
        MutateAsync(items => items.RemoveAll(e => e.Id == id) > 0, cancellationToken);

    public void Dispose()
    {
        _gate.Dispose();
        GC.SuppressFinalize(this);
    }

    private async ValueTask<IReadOnlyList<TEntity>> QueryAsync<TState>(
        Func<IReadOnlyList<TEntity>, TState, IEnumerable<TEntity>> query,
        TState state,
        CancellationToken cancellationToken)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        return query(snapshot, state).Select(Clone).ToList();
    }

    private ValueTask<IReadOnlyList<TEntity>> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        // Fast path: already loaded, no await, no allocation of a Task.
        var snapshot = Volatile.Read(ref _snapshot);
        return snapshot is not null
            ? ValueTask.FromResult(snapshot)
            : new ValueTask<IReadOnlyList<TEntity>>(LoadAsync(cancellationToken));
    }

    private async Task<IReadOnlyList<TEntity>> LoadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_snapshot is null)
            {
                Volatile.Write(ref _snapshot, await ReadFileAsync(cancellationToken).ConfigureAwait(false));
            }

            return _snapshot!;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<bool> MutateAsync(Func<List<TEntity>, bool> change, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = _snapshot ?? await ReadFileAsync(cancellationToken).ConfigureAwait(false);
            var working = current.ToList();
            if (!change(working))
            {
                return false;
            }

            await WriteFileAsync(working, cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _snapshot, working.AsReadOnly());
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IReadOnlyList<TEntity>> ReadFileAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        await using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        // Accept a bare array (current format) or a wrapper object holding one array (Week 1 format).
        var array = document.RootElement.ValueKind switch
        {
            JsonValueKind.Array => document.RootElement,
            JsonValueKind.Object => document.RootElement.EnumerateObject()
                .Select(p => p.Value)
                .FirstOrDefault(v => v.ValueKind == JsonValueKind.Array),
            _ => default
        };

        return array.ValueKind == JsonValueKind.Array
            ? (array.Deserialize<List<TEntity>>(ApexPostJson.Options) ?? []).AsReadOnly()
            : [];
    }

    private async Task WriteFileAsync(List<TEntity> items, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = _filePath + ".tmp";
        await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
        {
            await JsonSerializer.SerializeAsync(stream, items, ApexPostJson.Options, cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, _filePath, overwrite: true);
    }

    private static TEntity Clone(TEntity entity) =>
        JsonSerializer.Deserialize<TEntity>(JsonSerializer.SerializeToUtf8Bytes(entity, ApexPostJson.Options), ApexPostJson.Options)!;
}
