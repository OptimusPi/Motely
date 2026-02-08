namespace Motely.Repository;

/// <summary>
/// Source of truth for filter library metadata and JAML content (file-based or DB-backed).
/// Implementations: file system (Repository host) or Duck Lake (Motely.DB).
/// </summary>
public interface ILibraryMetadata
{
    /// <summary>
    /// Returns the current filter catalog (id, name, author, searchId, columns, etc.).
    /// </summary>
    IReadOnlyList<FilterMetadata> GetLibraryMetadata();

    /// <summary>
    /// Returns the raw JAML content for a filter by id, or null if not found.
    /// </summary>
    string? GetFilterJaml(string filterId);
}

/// <summary>
/// Metadata for one filter in the library (for list/catalog responses).
/// </summary>
/// <param name="Id">Filter id (e.g. file stem).</param>
/// <param name="Name">Display name.</param>
/// <param name="Author">Author.</param>
/// <param name="FilePath">File name or path segment.</param>
/// <param name="SearchId">Suggested search id (e.g. name_deck_stake).</param>
/// <param name="Columns">Column names for results.</param>
public sealed record FilterMetadata(
    string Id,
    string Name,
    string Author,
    string FilePath,
    string SearchId,
    IReadOnlyList<string> Columns
);
