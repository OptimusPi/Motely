using System.Data;
using Terminal.Gui;

namespace Motely.TUI;

/// <summary>
/// Compact search window that shows top 5 results (Seed + Score only).
/// Can be expanded to full view or minimized back to compact.
/// </summary>
public class MiniSearchWindow : Window
{
    private readonly string _filterName;
    private readonly SearchWindow _fullSearchWindow;
    private TableView _miniTable;
    private Label _statusLabel;
    private static int _windowCount = 0;
    private readonly int _windowId;

    // Position offset for cascading windows
    private const int CASCADE_OFFSET = 2;

    public MiniSearchWindow(string configPath, string configFormat)
    {
        _windowId = ++_windowCount;
        _filterName = System.IO.Path.GetFileNameWithoutExtension(configPath);

        Title = $"Search #{_windowId}: {_filterName}";
        X = CASCADE_OFFSET * _windowId;
        Y = CASCADE_OFFSET * _windowId;
        Width = 35;
        Height = 12;
        CanFocus = true;
        SetScheme(BalatroTheme.Window);

        // Create the full search window (but don't run it yet)
        _fullSearchWindow = new SearchWindow(configPath, configFormat);

        // Status label
        _statusLabel = new Label()
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill() - 2,
            Text = "Starting...",
        };
        Add(_statusLabel);

        // Mini table showing top 5
        _miniTable = new TableView()
        {
            X = 1,
            Y = 2,
            Width = Dim.Fill() - 2,
            Height = 6,
            FullRowSelect = true,
            CanFocus = true,
        };
        _miniTable.SetScheme(new Scheme()
        {
            Normal = new Attribute(BalatroTheme.White, BalatroTheme.DarkGrey),
            Focus = new Attribute(BalatroTheme.Black, BalatroTheme.Red),
        });

        // Initialize empty table
        var emptyTable = new DataTable();
        emptyTable.Columns.Add("Seed");
        emptyTable.Columns.Add("Score");
        _miniTable.Table = new DataTableSource(emptyTable);
        Add(_miniTable);

        // Expand button
        var expandBtn = new CleanButton()
        {
            X = 1,
            Y = Pos.AnchorEnd(1),
            Text = "Expand",
            Width = 10,
        };
        expandBtn.SetScheme(BalatroTheme.BlueButton);
        expandBtn.Accept += (s, e) => ExpandWindow();
        Add(expandBtn);

        // Close button
        var closeBtn = new CleanButton()
        {
            X = Pos.Right(expandBtn) + 1,
            Y = Pos.AnchorEnd(1),
            Text = "X",
            Width = 5,
        };
        closeBtn.SetScheme(BalatroTheme.ModalButton);
        closeBtn.Accept += (s, e) => CloseSearch();
        Add(closeBtn);

        // ESC to close
        KeyDown += (s, e) =>
        {
            if (e.KeyCode == KeyCode.Esc)
            {
                CloseSearch();
                e.Handled = true;
            }
            else if (e.KeyCode == KeyCode.Enter)
            {
                ExpandWindow();
                e.Handled = true;
            }
        };
    }

    public void StartSearch()
    {
        // TODO: Hook into the search executor to get updates
        // For now, this is a placeholder - the actual search runs in SearchWindow
        _statusLabel.Text = "Running...";
    }

    public void UpdateResults(string[] seeds, double[] scores, int total, double rate)
    {
        App?.Invoke(() =>
        {
            _statusLabel.Text = $"Checked: {total:N0} | {rate:N0}/s";

            var table = new DataTable();
            table.Columns.Add("Seed");
            table.Columns.Add("Score");

            int count = Math.Min(5, seeds.Length);
            for (int i = 0; i < count; i++)
            {
                table.Rows.Add(seeds[i], scores[i].ToString("F1"));
            }

            _miniTable.Table = new DataTableSource(table);
        });
    }

    private void ExpandWindow()
    {
        // Run the full search window
        App?.Run(_fullSearchWindow);
    }

    private void CloseSearch()
    {
        // Stop search and remove window
        SuperView?.Remove(this);
    }
}
