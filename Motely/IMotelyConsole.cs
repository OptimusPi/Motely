namespace Motely;

/// <summary>
/// Platform-agnostic console interface for Motely logging operations
/// </summary>
public interface IMotelyConsole
{
    /// <summary>
    /// Gets or sets whether the console is enabled
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    /// Sets the bottom status line (desktop only)
    /// </summary>
    /// <param name="bottomLine">Text to display on bottom line, or null to clear</param>
    void SetBottomLine(string? bottomLine);

    /// <summary>
    /// Writes a line to the console
    /// </summary>
    /// <typeparam name="T">Type of message</typeparam>
    /// <param name="message">Message to write</param>
    void WriteLine<T>(T message);

    /// <summary>
    /// Writes a line to the console
    /// </summary>
    /// <param name="message">Message to write</param>
    void WriteLine(string? message);
}
