using System.Data;
using DuckDB.NET.Data;

namespace Motely.TUI;

/// <summary>
/// Non-modal browser for the Motely DuckLake results store.
/// Layout: path bar on top, filter list on the left, results table on the right,
/// toolbar at the bottom. No modal dialogs.
/// </summary>
public class ResultsBrowserWindow : Window
{
    private readonly TextField _pathField;
    private readonly ListView _filterList;
    private readonly TableView _resultsTable;
    private readonly Label _statusLabel;
    private readonly Label _countLabel;

    private DataTable _dataTable = new();
    private List<string> _filterIds = new();
    private string _selectedFilterId = "";
    private int _detectedTallyCount;

    public ResultsBrowserWindow()
    {
        Title = "Results Browser — DuckLake";
        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();
        CanFocus = true;
        ColorScheme = BalatroTheme.Window;

        // ── top bar: lake path + refresh/export ─────────────────────────────
        var pathLabel = new Label { X = 1, Y = 1, Text = "Lake:" };
        Add(pathLabel);

        _pathField = new TextField
        {
            X = Pos.Right(pathLabel) + 1,
            Y = 1,
            Width = Dim.Fill()! - 42,
            Text = TuiSettings.DataLakePath,
        };
        _pathField.ColorScheme = new ColorScheme
        {
            Normal = new Attribute(BalatroTheme.White, BalatroTheme.DarkGrey),
            Focus = new Attribute(BalatroTheme.White, BalatroTheme.Blue),
        };
        Add(_pathField);

        var refreshBtn = new CleanButton { X = Pos.AnchorEnd(40), Y = 1, Text = " Refresh " };
        refreshBtn.ColorScheme = BalatroTheme.BlueButton;
        refreshBtn.Accept += (_, _) => LoadFilterIds();
        Add(refreshBtn);

        var exportBtn = new CleanButton { X = Pos.AnchorEnd(28), Y = 1, Text = " Export .parquet " };
        exportBtn.ColorScheme = BalatroTheme.GreenButton;
        exportBtn.Accept += (_, _) => ExportParquet();
        Add(exportBtn);

        var clearBtn = new CleanButton { X = Pos.AnchorEnd(10), Y = 1, Text = " Clear " };
        clearBtn.ColorScheme = BalatroTheme.RedButton;
        clearBtn.Accept += (_, _) => ClearFilter();
        Add(clearBtn);

        // ── left panel: filter_id list ──────────────────────────────────────
        var filterFrame = new FrameView
        {
            X = 1,
            Y = 3,
            Width = 28,
            Height = Dim.Fill()! - 5,
            Title = "Filters",
        };
        filterFrame.ColorScheme = BalatroTheme.InnerPanel;
        Add(filterFrame);

        _filterList = new ListView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            CanFocus = true,
        };
        _filterList.ColorScheme = new ColorScheme
        {
            Normal = new Attribute(BalatroTheme.White, BalatroTheme.InnerPanelGrey),
            Focus = new Attribute(BalatroTheme.White, BalatroTheme.Blue),
            HotNormal = new Attribute(BalatroTheme.White, BalatroTheme.InnerPanelGrey),
            HotFocus = new Attribute(BalatroTheme.White, BalatroTheme.Blue),
        };
        _filterList.SelectedItemChanged += (_, _) => OnFilterSelected();
        _filterList.Accepting += (_, _) => OnFilterSelected();
        filterFrame.Add(_filterList);

        // ── right panel: results table ──────────────────────────────────────
        var resultsFrame = new FrameView
        {
            X = Pos.Right(filterFrame) + 1,
            Y = 3,
            Width = Dim.Fill()! - 2,
            Height = Dim.Fill()! - 5,
            Title = "Results (select a filter)",
        };
        resultsFrame.ColorScheme = BalatroTheme.InnerPanel;
        Add(resultsFrame);

        _resultsTable = new TableView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            FullRowSelect = true,
            CanFocus = true,
        };
        _resultsTable.Style.ShowHorizontalHeaderOverline = false;
        _resultsTable.Style.ShowHorizontalHeaderUnderline = true;
        _resultsTable.Style.AlwaysShowHeaders = true;
        _resultsTable.ColorScheme = new ColorScheme
        {
            Normal = new Attribute(BalatroTheme.White, BalatroTheme.InnerPanelGrey),
            Focus = new Attribute(BalatroTheme.White, BalatroTheme.Blue),
            HotNormal = new Attribute(BalatroTheme.Orange, BalatroTheme.InnerPanelGrey),
            HotFocus = new Attribute(BalatroTheme.Orange, BalatroTheme.Blue),
        };
        _resultsTable.Table = new DataTableSource(_dataTable);
        resultsFrame.Add(_resultsTable);

        // ── bottom bar: status + back ───────────────────────────────────────
        _statusLabel = new Label { X = 1, Y = Pos.AnchorEnd(2), Text = "Idle" };
        _statusLabel.ColorScheme = new ColorScheme
        {
            Normal = new Attribute(BalatroTheme.LightGrey, BalatroTheme.ModalGrey),
        };
        Add(_statusLabel);

        _countLabel = new Label { X = Pos.Right(_statusLabel) + 2, Y = Pos.AnchorEnd(2), Text = "" };
        _countLabel.ColorScheme = new ColorScheme
        {
            Normal = new Attribute(BalatroTheme.Orange, BalatroTheme.ModalGrey),
        };
        Add(_countLabel);

        var backBtn = new CleanButton
        {
            X = Pos.AnchorEnd(10),
            Y = Pos.AnchorEnd(2),
            Text = " Back ",
        };
        backBtn.ColorScheme = BalatroTheme.BackButton;
        backBtn.Accept += (_, _) => MotelyTUI.CloseWindow(this);
        Add(backBtn);

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == KeyCode.Esc)
            {
                MotelyTUI.CloseWindow(this);
                e.Handled = true;
            }
            else if (e.KeyCode == KeyCode.F5)
            {
                LoadFilterIds();
                e.Handled = true;
            }
        };

        LoadFilterIds();
    }

    private string ResolveLakePath()
    {
        var raw = _pathField.Text;
        if (string.IsNullOrWhiteSpace(raw)) raw = "seeds";
        TuiSettings.DataLakePath = raw;
        TuiSettings.Save();
        return Path.GetFullPath(raw);
    }

    private (string LakeDir, string MetaFile, string DataDir) ResolveLakePaths()
    {
        var fullPath = ResolveLakePath();
        if (!Path.HasExtension(fullPath))
        {
            return (fullPath, Path.Combine(fullPath, "metadata.ducklake"), Path.Combine(fullPath, "data"));
        }
        var dir = Path.GetDirectoryName(fullPath)!;
        var baseName = Path.GetFileNameWithoutExtension(fullPath);
        var lakeDir = Path.Combine(dir, $"{baseName}_lake");
        return (lakeDir, Path.Combine(lakeDir, "metadata.ducklake"), Path.Combine(lakeDir, "data"));
    }

    private DuckDBConnection OpenConnection()
    {
        var (lakeDir, metaFile, dataDir) = ResolveLakePaths();
        if (!Directory.Exists(lakeDir) || !File.Exists(metaFile))
            throw new FileNotFoundException($"DuckLake not found at: {lakeDir}");

        var conn = new DuckDBConnection("Data Source=:memory:");
        conn.Open();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "INSTALL ducklake; LOAD ducklake;";
            cmd.ExecuteNonQuery();
            cmd.CommandText = $"ATTACH 'ducklake:{EscapeSqlPath(metaFile)}' AS motely_lake (DATA_PATH '{EscapeSqlPath(dataDir)}');";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "USE motely_lake;";
            cmd.ExecuteNonQuery();
        }
        return conn;
    }

    private static string EscapeSqlPath(string path) => path.Replace("\\", "/").Replace("'", "''");
    private static string EscapeSqlLiteral(string value) => value.Replace("'", "''");

    private void LoadFilterIds()
    {
        _filterIds.Clear();
        _filterList.SetSource(new System.Collections.ObjectModel.ObservableCollection<string>(new List<string>()));
        _dataTable = new DataTable();
        _resultsTable.Table = new DataTableSource(_dataTable);
        _countLabel.Text = "";

        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();

            _detectedTallyCount = DetectTallyCount(conn);

            cmd.CommandText =
                "SELECT DISTINCT filter_id FROM results ORDER BY filter_id";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                _filterIds.Add(reader.IsDBNull(0) ? "" : reader.GetString(0));

            var display = _filterIds
                .Select(id => string.IsNullOrEmpty(id) ? "(default)" : id)
                .ToList();
            _filterList.SetSource(new System.Collections.ObjectModel.ObservableCollection<string>(display));

            if (_filterIds.Count > 0)
            {
                _filterList.SelectedItem = 0;
                _selectedFilterId = _filterIds[0];
                LoadResults(conn, _selectedFilterId);
            }

            _statusLabel.Text = $"OK — {_filterIds.Count} filter(s), {_detectedTallyCount} tally col(s)";
            _statusLabel.ColorScheme = new ColorScheme
            {
                Normal = new Attribute(BalatroTheme.Green, BalatroTheme.ModalGrey),
            };
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Error: {ex.Message}";
            _statusLabel.ColorScheme = new ColorScheme
            {
                Normal = new Attribute(BalatroTheme.Red, BalatroTheme.ModalGrey),
            };
        }

        SetNeedsDraw();
    }

    private static int DetectTallyCount(DuckDBConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT COUNT(*) FROM information_schema.columns " +
            "WHERE table_name = 'results' AND column_name LIKE 'tally%'";
        var result = cmd.ExecuteScalar();
        return result is long l ? (int)l : 0;
    }

    private void OnFilterSelected()
    {
        if (_filterList.SelectedItem < 0 || _filterList.SelectedItem >= _filterIds.Count)
            return;

        _selectedFilterId = _filterIds[_filterList.SelectedItem];
        try
        {
            using var conn = OpenConnection();
            LoadResults(conn, _selectedFilterId);
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Error: {ex.Message}";
        }
    }

    private void LoadResults(DuckDBConnection conn, string filterId, int limit = 1000)
    {
        var dt = new DataTable();
        dt.Columns.Add("Seed", typeof(string));
        dt.Columns.Add("Score", typeof(int));
        for (int i = 0; i < _detectedTallyCount; i++)
            dt.Columns.Add($"t{i}", typeof(int));

        using var cmd = conn.CreateCommand();
        var tallyCols = string.Concat(Enumerable.Range(0, _detectedTallyCount).Select(i => $", tally{i}"));
        cmd.CommandText =
            $"SELECT seed, score{tallyCols} FROM results " +
            $"WHERE filter_id = '{EscapeSqlLiteral(filterId)}' " +
            $"ORDER BY score DESC LIMIT {limit}";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var row = dt.NewRow();
            row[0] = reader.GetString(0);
            row[1] = reader.GetInt32(1);
            for (int i = 0; i < _detectedTallyCount; i++)
                row[2 + i] = reader.GetInt32(2 + i);
            dt.Rows.Add(row);
        }

        _dataTable = dt;
        _resultsTable.Table = new DataTableSource(_dataTable);

        var displayId = string.IsNullOrEmpty(filterId) ? "(default)" : filterId;
        _countLabel.Text = $"{dt.Rows.Count:N0} row(s) • filter: {displayId}";

        SetNeedsDraw();
    }

    private void ExportParquet()
    {
        if (_dataTable.Rows.Count == 0)
        {
            _statusLabel.Text = "Nothing to export — load a filter first.";
            return;
        }

        try
        {
            var target = Path.GetFullPath(
                Path.Combine(
                    Environment.CurrentDirectory,
                    $"{(string.IsNullOrEmpty(_selectedFilterId) ? "default" : _selectedFilterId)}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.parquet"));

            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            var tallyCols = string.Concat(Enumerable.Range(0, _detectedTallyCount).Select(i => $", tally{i}"));
            cmd.CommandText =
                $"COPY (SELECT seed, score{tallyCols} FROM results " +
                $"WHERE filter_id = '{EscapeSqlLiteral(_selectedFilterId)}' " +
                $"ORDER BY score DESC) TO '{EscapeSqlPath(target)}' (FORMAT PARQUET)";
            cmd.ExecuteNonQuery();

            _statusLabel.Text = $"Exported → {target}";
            _statusLabel.ColorScheme = new ColorScheme
            {
                Normal = new Attribute(BalatroTheme.Green, BalatroTheme.ModalGrey),
            };
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Export failed: {ex.Message}";
            _statusLabel.ColorScheme = new ColorScheme
            {
                Normal = new Attribute(BalatroTheme.Red, BalatroTheme.ModalGrey),
            };
        }
    }

    private void ClearFilter()
    {
        if (string.IsNullOrEmpty(_selectedFilterId) && _filterIds.Count == 0) return;

        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DELETE FROM results WHERE filter_id = '{EscapeSqlLiteral(_selectedFilterId)}'";
            var rows = cmd.ExecuteNonQuery();
            _statusLabel.Text = $"Cleared {rows:N0} row(s) from filter '{_selectedFilterId}'.";
            LoadFilterIds();
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Clear failed: {ex.Message}";
        }
    }
}
