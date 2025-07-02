using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TheFool.Models;

namespace TheFool.Services;

public class UserConfigService
{
    private readonly string _configDirectory;
    
    public UserConfigService()
    {
        _configDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TheFool",
            "UserConfigs");
        
        Directory.CreateDirectory(_configDirectory);
    }
    
    public async Task<bool> SaveConfigurationAsync(string configName, UserConfiguration config)
    {
        try
        {
            var fileName = SanitizeFileName(configName) + ".json";
            var filePath = Path.Combine(_configDirectory, fileName);
            
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            await File.WriteAllTextAsync(filePath, json);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving configuration: {ex.Message}");
            return false;
        }
    }
    
    public async Task<UserConfiguration?> LoadConfigurationAsync(string configName)
    {
        try
        {
            var fileName = SanitizeFileName(configName) + ".json";
            var filePath = Path.Combine(_configDirectory, fileName);
            
            if (!File.Exists(filePath))
                return null;
                
            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<UserConfiguration>(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading configuration: {ex.Message}");
            return null;
        }
    }
    
    public Task<List<string>> GetAvailableConfigurationsAsync()
    {
        try
        {
            var files = Directory.GetFiles(_configDirectory, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => !string.IsNullOrEmpty(name))
                .Cast<string>()
                .ToList();
                
            return Task.FromResult(files);
        }
        catch
        {
            return Task.FromResult(new List<string>());
        }
    }
    
    public Task<bool> DeleteConfigurationAsync(string configName)
    {
        try
        {
            var fileName = SanitizeFileName(configName) + ".json";
            var filePath = Path.Combine(_configDirectory, fileName);
            
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                return Task.FromResult(true);
            }
            
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting configuration: {ex.Message}");
            return Task.FromResult(false);
        }
    }
    
    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
    }
}

public class UserConfiguration
{
    public string Name { get; set; } = string.Empty;
    public string Deck { get; set; } = string.Empty;
    public string Stake { get; set; } = string.Empty;
    public List<SearchCriteriaItem> SearchItems { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime LastModified { get; set; } = DateTime.Now;
}