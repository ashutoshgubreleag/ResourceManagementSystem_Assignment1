using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ResourceManagementSystem.Repositories
{
    public class BaseJsonRepository<TEntity> : IBaseEntityRepository<TEntity> where TEntity : class
    {
        private readonly object _fileLock = new object();
        private readonly string _filePath;

        public BaseJsonRepository(string? filePath = null)
        {
            var fileName = filePath ?? GetDefaultFileName();
            _filePath = FindJsonPath(fileName) ?? fileName;
        }

        private static string GetDefaultFileName()
        {
            return typeof(TEntity).Name + ".json";
        }

        private static string? FindJsonPath(string fileName)
        {
            var dir = AppContext.BaseDirectory;
            var current = new DirectoryInfo(dir);
            while (current != null)
            {
                var candidate = Path.Combine(current.FullName, fileName);
                if (File.Exists(candidate))
                    return candidate;
                current = current.Parent;
            }
            return null;
        }

        private List<TEntity> ReadEntities()
        {
            lock (_fileLock)
            {
                if (!File.Exists(_filePath))
                    return new List<TEntity>();

                var json = File.ReadAllText(_filePath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Array)
                {
                    return JsonSerializer.Deserialize<List<TEntity>>(json, options) ?? new List<TEntity>();
                }

                if (root.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in root.EnumerateObject())
                    {
                        if (prop.Value.ValueKind == JsonValueKind.Array)
                        {
                            return JsonSerializer.Deserialize<List<TEntity>>(prop.Value.GetRawText(), options) ?? new List<TEntity>();
                        }
                    }
                }

                return new List<TEntity>();
            }
        }

        private void WriteEntities(List<TEntity> entities)
        {
            lock (_fileLock)
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                // create a simple wrapper property using pluralized type name (basic)
                var wrapperName = typeof(TEntity).Name.ToLower() + "s";
                var wrapper = new Dictionary<string, List<TEntity>>
                {
                    [wrapperName] = entities
                };

                var json = JsonSerializer.Serialize(wrapper, options);
                File.WriteAllText(_filePath, json);
            }
        }

        private PropertyInfo? GetIdProperty()
        {
            return typeof(TEntity).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(p => string.Equals(p.Name, "Id", StringComparison.OrdinalIgnoreCase));
        }

        private int? GetIntIdValue(object? value)
        {
            if (value == null) return null;
            if (value is int i) return i;
            if (value is long l) return (int)l;
            if (value is string s && int.TryParse(s, out var ii)) return ii;
            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return null;
            }
        }

        public TEntity Add(TEntity entity)
        {
            var list = ReadEntities();
            var idProp = GetIdProperty();
            if (idProp != null)
            {
                var maxId = list.Select(e => GetIntIdValue(idProp.GetValue(e))).Where(v => v.HasValue).Select(v => v!.Value).DefaultIfEmpty(0).Max();
                var next = maxId + 1;
                // only set if current value is zero or default
                var cur = idProp.GetValue(entity);
                var curInt = GetIntIdValue(cur);
                if (!curInt.HasValue || curInt.Value == 0)
                {
                    if (idProp.PropertyType == typeof(int) || idProp.PropertyType == typeof(int?))
                        idProp.SetValue(entity, next);
                    else if (idProp.PropertyType == typeof(long) || idProp.PropertyType == typeof(long?))
                        idProp.SetValue(entity, (long)next);
                }
            }

            list.Add(entity);
            WriteEntities(list);
            return entity;
        }

        public bool Update(TEntity entity)
        {
            var idProp = GetIdProperty();
            if (idProp == null) return false;
            var idVal = idProp.GetValue(entity);
            var idInt = GetIntIdValue(idVal);
            if (!idInt.HasValue) return false;

            var list = ReadEntities();
            var idx = list.FindIndex(e => GetIntIdValue(idProp.GetValue(e)) == idInt.Value);
            if (idx < 0) return false;
            list[idx] = entity;
            WriteEntities(list);
            return true;
        }

        public TEntity? GetById(object id)
        {
            var idInt = GetIntIdValue(id);
            if (!idInt.HasValue) return null;
            var idProp = GetIdProperty();
            if (idProp == null) return null;
            var list = ReadEntities();
            return list.FirstOrDefault(e => GetIntIdValue(idProp.GetValue(e)) == idInt.Value);
        }

        public IEnumerable<TEntity> GetAll()
        {
            return ReadEntities();
        }

        public bool Delete(object id)
        {
            var idInt = GetIntIdValue(id);
            if (!idInt.HasValue) return false;
            var idProp = GetIdProperty();
            if (idProp == null) return false;
            var list = ReadEntities();
            var idx = list.FindIndex(e => GetIntIdValue(idProp.GetValue(e)) == idInt.Value);
            if (idx < 0) return false;
            list.RemoveAt(idx);
            WriteEntities(list);
            return true;
        }
    }
}
