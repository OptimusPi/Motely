using System.Data;
using Motely.Executors;
using Motely.Filters;

namespace Motely.TUI;

public class SearchWindow : Window
{
    private readonly ResultsTable _resultsTable;
    private readonly SearchExecutor _searchExecutor;
    private readonly StatusBar _statusBar;

    public SearchWindow(string configName, string configFormat)
    {
        Title = $"Search: {Path.GetFileNameWithoutExtension(configName)}";
        X = Pos.Center();
        Y = Pos.Center();
        Width = 70;
        Height = 22;
        CanFocus = true;
        SetScheme(BalatroTheme.Window);

        _resultsTable = new ResultsTable();
        _searchExecutor = new SearchExecutor(configName, configFormat);
        _statusBar = new StatusBar();

        Add(_resultsTable);
        Add(_statusBar);
    }
}

public class ResultsTable : TableView
{
    public ResultsTable()
    {
        X = 1;
        Y = 1;
        Width = Dim.Fill() - 2;
        Height = 13;
        FullRowSelect = true;
        CanFocus = true;
        SetScheme(new Scheme
        {
            Normal = new Attribute(BalatroTheme.White, BalatroTheme.DarkGrey),
            Focus = new Attribute(BalatroTheme.Black, BalatroTheme.Red),
        });
    }
}

public class SearchExecutor
{
    private readonly string _configName;
    private readonly string _configFormat;

    public SearchExecutor(string configName, string configFormat)
    {
        _configName = configName;
        _configFormat = configFormat;
    }

    public void ExecuteSearch()
    {
        // Search logic here
    }
}

public class StatusBar : Label
{
    public StatusBar()
    {
        X = 1;
        Y = Pos.AnchorEnd(3);
        Width = Dim.Fill() - 2;
        Height = 1;
        Text = "Initializing search...";
        SetScheme(new Scheme
        {
            Normal = new Attribute(BalatroTheme.White, BalatroTheme.ModalGrey),
        });
    }
}
