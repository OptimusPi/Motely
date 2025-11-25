using System.Collections.ObjectModel;
using Motely.Filters;
using Terminal.Gui;

namespace Motely.TUI;

public class ItemSelectorDialog : Dialog
{
    public string? SelectedItem { get; private set; }

    public ItemSelectorDialog(string category)
    {
        Title = $"Select {category}";
        Width = 50;
        Height = 20;

        // Balatro-style color scheme
        ColorScheme = new ColorScheme()
        {
            Normal = new Terminal.Gui.Attribute(ColorName.White, ColorName.Black),
            Focus = new Terminal.Gui.Attribute(ColorName.Black, ColorName.BrightBlue),
            HotNormal = new Terminal.Gui.Attribute(ColorName.White, ColorName.Black),
            HotFocus = new Terminal.Gui.Attribute(ColorName.White, ColorName.BrightBlue),
        };

        var instructionLabel = new Label()
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill() - 2,
            Text = "Use arrows + Enter to select:",
        };
        Add(instructionLabel);

        var items = GetItemsForCategory(category);

        var itemStrings = items.Select((item, index) => $"{item}").ToArray();

        var listView = new ListView()
        {
            X = 1,
            Y = 3,
            Width = Dim.Fill() - 2,
            Height = Dim.Fill() - 5,
            AllowsMarking = false,
            CanFocus = true,
        };
        listView.SetSource(new ObservableCollection<string>(itemStrings));

        // Handle Enter key for selection
        listView.KeyDown += (sender, e) =>
        {
            if (e.KeyCode == KeyCode.Enter)
            {
                if (listView.SelectedItem >= 0 && listView.SelectedItem < items.Length)
                {
                    SelectedItem = items[listView.SelectedItem];
                    Application.RequestStop(this);
                }
                e.Handled = true;
            }
        };

        Add(listView);

        var cancelBtn = new Button() { Text = "Cancel" };
        cancelBtn.Accept += (s, e) =>
        {
            SelectedItem = null;
            Application.RequestStop(this);
        };
        AddButton(cancelBtn);

        listView.SetFocus();
    }

    private string[] GetItemsForCategory(string category)
    {
        return category switch
        {
            "Joker" => GetJokers(),
            "Legendary" => GetLegendaryJokers(),
            "Card" => GetPlayingCards(),
            "Tarot" => GetTarots(),
            "Spectral" => GetSpectrals(),
            "Planet" => GetPlanets(),
            "Voucher" => GetVouchers(),
            "Boss" => GetBosses(),
            "Tags" => GetTags(),
            _ => GetJokers(),
        };
    }

    private string[] GetJokers()
    {
        // Get all joker names from MotelyJoker enum
        return Enum.GetNames(typeof(MotelyJoker)).OrderBy(x => x).ToArray();
    }

    private string[] GetLegendaryJokers()
    {
        // Soul jokers are legendary jokers (the 5 legendary souls in Balatro)
        return Enum.GetNames(typeof(MotelyJoker))
            .Where(j =>
                j == "Perkeo" || j == "Triboulet" || j == "Yorick" || j == "Chicot" || j == "Canio"
            )
            .OrderBy(x => x)
            .ToArray();
    }

    private string[] GetPlayingCards()
    {
        // Generate all playing cards (Ace through King, all suits)
        var ranks = new[]
        {
            "Ace",
            "2",
            "3",
            "4",
            "5",
            "6",
            "7",
            "8",
            "9",
            "10",
            "Jack",
            "Queen",
            "King",
        };
        var suits = new[] { "Spades", "Hearts", "Diamonds", "Clubs" };
        return (from suit in suits from rank in ranks select $"{rank} of {suit}").ToArray();
    }

    private string[] GetTarots()
    {
        return Enum.GetNames(typeof(MotelyTarotCard)).OrderBy(x => x).ToArray();
    }

    private string[] GetSpectrals()
    {
        return Enum.GetNames(typeof(MotelySpectralCard)).OrderBy(x => x).ToArray();
    }

    private string[] GetPlanets()
    {
        return Enum.GetNames(typeof(MotelyPlanetCard)).OrderBy(x => x).ToArray();
    }

    private string[] GetVouchers()
    {
        return Enum.GetNames(typeof(MotelyVoucher)).OrderBy(x => x).ToArray();
    }

    private string[] GetBosses()
    {
        return Enum.GetNames(typeof(MotelyBossBlind)).OrderBy(x => x).ToArray();
    }

    private string[] GetTags()
    {
        return Enum.GetNames(typeof(MotelyTag)).OrderBy(x => x).ToArray();
    }
}
