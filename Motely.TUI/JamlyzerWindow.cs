namespace Motely.TUI;

/// <summary>
/// JAMLyzer — single-seed snapshot viewer. Type a seed, optionally a JAML lens, and
/// step ◄ ► (buttons) or ← → (keys) through the 8 antes. Items the lens's <c>should</c>
/// clauses match GLOW (orange) with their match reason shown inline.
///
/// This is the FACE for the analyzer engine in Motely.Analysis
/// (<see cref="Jamlyzer"/> / <see cref="JamlyzerFilterDesc"/>) — it renders the
/// already-computed <see cref="JamlyzerSnapshot"/>, it does not re-implement the walk.
/// </summary>
public sealed class JamlyzerWindow : Window
{
    // Later antes carry up to 50 shop slots; cap what we draw and SAY so (no silent truncation).
    private const int MaxShopShown = 15;

    private readonly TextField _seedField;
    private readonly TextView _lensField;
    private readonly Label _anteLabel;
    private readonly Label _statusLabel;
    private readonly FrameView _board;
    private readonly List<View> _boardItems = new();

    private JamlyzerSnapshot? _analysis;
    private int _ante; // 0-based index into _analysis.Antes

    public JamlyzerWindow()
    {
        Title = "JAMLyzer — seed snapshot";
        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();
        CanFocus = true;
        ColorScheme = BalatroTheme.Window;

        // ── seed + navigation row (Y=1) ─────────────────────────────────────
        var seedLabel = new Label { X = 1, Y = 1, Text = "Seed:" };
        Add(seedLabel);

        _seedField = new TextField
        {
            X = Pos.Right(seedLabel) + 1,
            Y = 1,
            Width = 18,
            Text = "UNITTEST",
        };
        _seedField.ColorScheme = new ColorScheme
        {
            Normal = new Attribute(BalatroTheme.White, BalatroTheme.DarkGrey),
            Focus = new Attribute(BalatroTheme.White, BalatroTheme.Blue),
        };
        Add(_seedField);

        var analyzeBtn = new CleanButton
        {
            X = Pos.Right(_seedField) + 2,
            Y = 1,
            Text = " Analyze ",
        };
        analyzeBtn.ColorScheme = BalatroTheme.GreenButton;
        analyzeBtn.Accept += (_, _) => RunAnalysis();
        Add(analyzeBtn);

        var prevBtn = new CleanButton
        {
            X = Pos.Right(analyzeBtn) + 2,
            Y = 1,
            Text = " ◄ ",
        };
        prevBtn.ColorScheme = BalatroTheme.PurpleButton;
        prevBtn.Accept += (_, _) => Step(-1);
        Add(prevBtn);

        _anteLabel = new Label
        {
            X = Pos.Right(prevBtn) + 1,
            Y = 1,
            Text = "ANTE -/-",
        };
        Add(_anteLabel);

        var nextBtn = new CleanButton
        {
            X = Pos.Right(_anteLabel) + 1,
            Y = 1,
            Text = " ► ",
        };
        nextBtn.ColorScheme = BalatroTheme.PurpleButton;
        nextBtn.Accept += (_, _) => Step(1);
        Add(nextBtn);

        // ── lens (JAML) editor (Y=3..7) ─────────────────────────────────────
        var lensLabel = new Label
        {
            X = 1,
            Y = 3,
            Text = "Lens (JAML — required; items matching `should` glow, deck/stake come from it):",
        };
        Add(lensLabel);

        _lensField = new TextView
        {
            X = 1,
            Y = 4,
            Width = Dim.Fill()! - 2,
            Height = 4,
            Text = "deck: Red\nstake: White\nshould:\n  - joker: any\n",
        };
        _lensField.ColorScheme = new ColorScheme
        {
            Normal = new Attribute(BalatroTheme.White, BalatroTheme.DarkGrey),
            Focus = new Attribute(BalatroTheme.White, BalatroTheme.DarkGrey),
        };
        Add(_lensField);

        // ── snapshot board (Y=9..bottom-1) ──────────────────────────────────
        _board = new FrameView
        {
            X = 0,
            Y = 9,
            Width = Dim.Fill(),
            Height = Dim.Fill()! - 1,
            Title = "Snapshot",
        };
        _board.ColorScheme = BalatroTheme.Window;
        Add(_board);

        _statusLabel = new Label
        {
            X = 1,
            Y = Pos.AnchorEnd(1),
            Text = "Enter a seed and press Analyze.   ◄ ► buttons or ← → keys change ante.   Esc closes.",
        };
        _statusLabel.ColorScheme = BalatroTheme.Hint;
        Add(_statusLabel);

        // Arrow keys step antes when focus isn't inside the lens/seed editors
        // (text fields consume arrows for caret movement — the ◄ ► buttons always work).
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == KeyCode.CursorLeft)
            {
                Step(-1);
                e.Handled = true;
            }
            else if (e.KeyCode == KeyCode.CursorRight)
            {
                Step(1);
                e.Handled = true;
            }
            else if (e.KeyCode == KeyCode.Esc)
            {
                MotelyTUI.CloseWindow(this);
                e.Handled = true;
            }
        };
    }

    private void RunAnalysis()
    {
        string seed = (_seedField.Text ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(seed))
        {
            SetStatus("Enter a seed first.");
            return;
        }

        string lensText = (_lensField.Text ?? string.Empty).Trim();

        // JAML *is* the lens. No lens = no JAMLyzer (that's just the legacy dump, which
        // already exists elsewhere). Don't silently fall back to it.
        if (lensText.Length == 0)
        {
            SetStatus("Write a JAML lens first — JAMLyzer shows the seed *through* the lens.");
            return;
        }

        if (!JamlConfigLoader.TryLoad(lensText, out var lens, out var error))
        {
            SetStatus($"Lens error: {error}");
            return;
        }

        _analysis = Jamlyzer.Analyze(seed, lens!);

        _ante = 0;

        if (!string.IsNullOrEmpty(_analysis.Error))
            SetStatus($"Analyze failed: {_analysis.Error}");
        else
            SetStatus(
                $"Analyzed {seed} — {_analysis.Antes.Count} antes.   ◄ ► / ← → to navigate."
            );

        Render();
    }

    private void Step(int delta)
    {
        if (_analysis is null || _analysis.Antes.Count == 0)
            return;

        int next = Math.Clamp(_ante + delta, 0, _analysis.Antes.Count - 1);
        if (next == _ante)
            return;

        _ante = next;
        Render();
    }

    private void Render()
    {
        foreach (var v in _boardItems)
            _board.Remove(v);
        _boardItems.Clear();

        if (_analysis is null)
        {
            _anteLabel.Text = "ANTE -/-";
            _anteLabel.SetNeedsDraw();
            _board.SetNeedsDraw();
            return;
        }

        if (!string.IsNullOrEmpty(_analysis.Error) || _analysis.Antes.Count == 0)
        {
            AddLine(0, _analysis.Error ?? "No antes produced.", BalatroTheme.Red);
            _anteLabel.Text = "ANTE -/-";
            _anteLabel.SetNeedsDraw();
            _board.SetNeedsDraw();
            return;
        }

        var ante = _analysis.Antes[_ante];
        _anteLabel.Text = $"ANTE {ante.Ante}/{_analysis.Antes.Count}";
        _anteLabel.SetNeedsDraw();

        int y = 0;
        AddLine(y++, $"Boss:    {FormatUtils.FormatBoss(ante.Boss)}", BalatroTheme.White);
        AddLine(y++, $"Voucher: {FormatUtils.FormatVoucher(ante.Voucher)}", BalatroTheme.White);
        AddLine(
            y++,
            $"Tags:    {FormatUtils.FormatTag(ante.SmallBlindTag)}, {FormatUtils.FormatTag(ante.BigBlindTag)}",
            BalatroTheme.White
        );
        if (ante.SmallBlindTagGrantedJoker is { } stj)
            AddItem(y++, "  small-tag → ", stj);
        if (ante.BigBlindTagGrantedJoker is { } btj)
            AddItem(y++, "  big-tag → ", btj);
        y++;

        AddLine(y++, "Shop:", BalatroTheme.LightGrey);
        int shown = Math.Min(ante.ShopQueue.Count, MaxShopShown);
        for (int i = 0; i < shown; i++)
            AddItem(y++, $"  {i + 1,2}) ", ante.ShopQueue[i]);
        if (ante.ShopQueue.Count > shown)
            AddLine(
                y++,
                $"  … {ante.ShopQueue.Count - shown} more (showing first {shown})",
                BalatroTheme.MediumGrey
            );

        y++;
        AddLine(y++, "Packs:", BalatroTheme.LightGrey);
        foreach (var pack in ante.Packs)
        {
            AddLine(y++, $"  {FormatUtils.FormatPackName(pack.Type)}", BalatroTheme.BrightSilver);
            foreach (var pi in pack.Items)
                AddItem(y++, "      • ", pi);
            if (pack.GrantedLegendaryJoker is { } lj)
                AddItem(y++, "      ☼ Soul → ", lj);
        }

        _board.SetNeedsDraw();
    }

    private void AddItem(int y, string prefix, SnapshotItem item)
    {
        bool glow = item.IsHighlighted;
        string text = glow ? $"{prefix}{item.Name}   ◄ {item.MatchedBy}" : $"{prefix}{item.Name}";
        AddLine(y, text, glow ? BalatroTheme.Orange : BalatroTheme.White);
    }

    private void AddLine(int y, string text, Color fg)
    {
        var lbl = new Label
        {
            X = 1,
            Y = y,
            Text = text,
            ColorScheme = new ColorScheme { Normal = new Attribute(fg, BalatroTheme.DarkGrey) },
        };
        _board.Add(lbl);
        _boardItems.Add(lbl);
    }

    private void SetStatus(string text)
    {
        _statusLabel.Text = text;
        _statusLabel.SetNeedsDraw();
    }
}
