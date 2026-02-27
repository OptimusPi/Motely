using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Motely.Filters;

namespace Motely.Repository
{
    /// <summary>
    /// Repository interface for filter management - Desktop only
    /// Provides a single, clean API for all filter operations
    /// </summary>
    public interface IFilterRepository
    {
        /// <summary>
        /// Creates a new filter with default values
        /// </summary>
        /// <param name="name">Filter name</param>
        /// <returns>Created filter configuration</returns>
        Task<JamlConfig> CreateFilterAsync(string name);
        
        /// <summary>
        /// Gets a filter by name
        /// </summary>
        /// <param name="name">Filter name</param>
        /// <returns>Filter configuration or null if not found</returns>
        Task<JamlConfig?> GetFilterAsync(string name);
        
        /// <summary>
        /// Gets all available filters
        /// </summary>
        /// <returns>List of all filter configurations</returns>
        Task<List<JamlConfig>> GetAllFiltersAsync();
        
        /// <summary>
        /// Saves a filter to disk
        /// </summary>
        /// <param name="filter">Filter configuration to save</param>
        Task SaveFilterAsync(JamlConfig filter);
        
        /// <summary>
        /// Deletes a filter by name
        /// </summary>
        /// <param name="name">Filter name to delete</param>
        /// <returns>True if deleted, false if not found</returns>
        Task<bool> DeleteFilterAsync(string name);
        
        /// <summary>
        /// Checks if a filter exists
        /// </summary>
        /// <param name="name">Filter name</param>
        /// <returns>True if filter exists</returns>
        Task<bool> FilterExistsAsync(string name);
        
        /// <summary>
        /// Gets the filters directory path
        /// </summary>
        /// <returns>Full path to filters directory</returns>
        string GetFiltersDirectory();
    }
}
