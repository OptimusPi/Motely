using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Motely.Filters;

namespace Motely.Repository
{
    /// <summary>
    /// Desktop-only implementation of filter repository
    /// Manages JamlConfig files in a local directory
    /// </summary>
    public class DesktopFilterRepository : IFilterRepository
    {
        private readonly string _filtersDirectory;
        private readonly JsonSerializerOptions _jsonOptions;

        public DesktopFilterRepository()
        {
            // Initialize filters directory
            var baseDir =
                Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
                ?? AppDomain.CurrentDomain.BaseDirectory;
            _filtersDirectory = Path.Combine(baseDir, "JsonItemFilters");
            Directory.CreateDirectory(_filtersDirectory);

            // Configure JSON serialization options
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = System
                    .Text
                    .Json
                    .Serialization
                    .JsonIgnoreCondition
                    .WhenWritingNull,
            };
        }

        public async Task<JamlConfig> CreateFilterAsync(string name)
        {
            var filter = new JamlConfig
            {
                Name = name,
                Description = "Created with Balatro Seed Oracle",
                Author = "pifreak",
                DateCreated = DateTime.UtcNow,
                Deck = Motely.MotelyDeck.Red,
                Stake = Motely.MotelyStake.White,
                Must = new JamlClauseSet(),
                Should = new JamlClauseSet(),
                MustNot = new JamlClauseSet(),
            };

            await SaveFilterAsync(filter);
            return filter;
        }

        public async Task<JamlConfig?> GetFilterAsync(string name)
        {
            try
            {
                var filePath = GetFilterFilePath(name);
                if (!File.Exists(filePath))
                    return null;

                var json = await File.ReadAllTextAsync(filePath);
                return JsonSerializer.Deserialize<JamlConfig>(json, _jsonOptions);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<List<JamlConfig>> GetAllFiltersAsync()
        {
            var filters = new List<JamlConfig>();

            try
            {
                var files = Directory
                    .GetFiles(_filtersDirectory, "*.json")
                    .Where(f => !Path.GetFileName(f).StartsWith("_")); // Skip temp files

                foreach (var file in files)
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(file);
                        var filter = JsonSerializer.Deserialize<JamlConfig>(json, _jsonOptions);
                        if (filter != null)
                        {
                            filters.Add(filter);
                        }
                    }
                    catch
                    {
                        // Skip invalid files
                        continue;
                    }
                }
            }
            catch
            {
                // Return empty list if directory access fails
            }

            return filters.OrderBy(f => f.Name).ToList();
        }

        public async Task SaveFilterAsync(JamlConfig filter)
        {
            if (filter == null)
                throw new ArgumentNullException(nameof(filter));

            try
            {
                var filePath = GetFilterFilePath(filter.Name);
                var json = JsonSerializer.Serialize(filter, _jsonOptions);
                await File.WriteAllTextAsync(filePath, json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to save filter '{filter.Name}': {ex.Message}",
                    ex
                );
            }
        }

        public async Task<bool> DeleteFilterAsync(string name)
        {
            try
            {
                var filePath = GetFilterFilePath(name);
                if (!File.Exists(filePath))
                    return false;

                await Task.Run(() => File.Delete(filePath));
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> FilterExistsAsync(string name)
        {
            try
            {
                var filePath = GetFilterFilePath(name);
                return await Task.FromResult(File.Exists(filePath));
            }
            catch
            {
                return false;
            }
        }

        public string GetFiltersDirectory()
        {
            return _filtersDirectory;
        }

        private string GetFilterFilePath(string name)
        {
            // Sanitize filename - remove invalid characters
            var sanitizedName = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(_filtersDirectory, $"{sanitizedName}.json");
        }
    }
}
