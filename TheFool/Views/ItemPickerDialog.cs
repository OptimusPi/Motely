using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System.Collections.Generic;
using System.Linq;
using TheFool.Models;

namespace TheFool.Views;

public partial class ItemPickerDialog : Window
{
    public string SelectedItemName { get; private set; } = "";
    public string SelectedItemType { get; private set; } = "";
    
    public ItemPickerDialog()
    {
        InitializeComponent();
    }

    public ItemPickerDialog(string itemType) : this()
    {
        SelectedItemType = itemType;
        DataContext = new ItemPickerViewModel(itemType);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string itemName)
        {
            SelectedItemName = itemName;
            Close(true);
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}

public class ItemPickerViewModel
{
    public string Title { get; }
    public List<ItemCategory> Categories { get; }
    public string SearchText { get; set; } = "";

    public ItemPickerViewModel(string itemType)
    {
        Title = $"Select {itemType}";
        Categories = LoadItemsForType(itemType);
    }

    private List<ItemCategory> LoadItemsForType(string itemType)
    {
        switch (itemType)
        {
            case "Joker":
                return new List<ItemCategory>
                {
                    new ItemCategory 
                    { 
                        Name = "Common",
                        Items = new List<PickerItem>
                        {
                            new PickerItem { Name = "Joker", Icon = "🃏", Description = "+4 Mult" },
                            new PickerItem { Name = "Greedy Joker", Icon = "💰", Description = "Played cards with Diamond suit give +4 Mult" },
                            new PickerItem { Name = "Lusty Joker", Icon = "❤️", Description = "Played cards with Heart suit give +4 Mult" },
                            new PickerItem { Name = "Wrathful Joker", Icon = "♠️", Description = "Played cards with Spade suit give +4 Mult" },
                            new PickerItem { Name = "Gluttonous Joker", Icon = "♣️", Description = "Played cards with Club suit give +4 Mult" },
                        }
                    },
                    new ItemCategory
                    {
                        Name = "Uncommon",
                        Items = new List<PickerItem>
                        {
                            new PickerItem { Name = "Jolly Joker", Icon = "😊", Description = "+8 Mult if played hand contains a Pair" },
                            new PickerItem { Name = "Zany Joker", Icon = "🤪", Description = "+12 Mult if played hand contains a Three of a Kind" },
                            new PickerItem { Name = "Mad Joker", Icon = "😠", Description = "+20 Mult if played hand contains a Four of a Kind" },
                            new PickerItem { Name = "Crazy Joker", Icon = "🤯", Description = "+12 Mult if played hand contains a Straight" },
                            new PickerItem { Name = "Droll Joker", Icon = "🙃", Description = "+10 Mult if played hand contains a Flush" },
                        }
                    },
                    new ItemCategory
                    {
                        Name = "Rare",
                        Items = new List<PickerItem>
                        {
                            new PickerItem { Name = "Half Joker", Icon = "➗", Description = "+20 Mult if played hand contains 3 or fewer cards" },
                            new PickerItem { Name = "Stencil Joker", Icon = "🖼️", Description = "X1 Mult for each empty Joker slot" },
                            new PickerItem { Name = "Four Fingers", Icon = "🖐️", Description = "All Flushes and Straights can be made with 4 cards" },
                            new PickerItem { Name = "Mime", Icon = "🎭", Description = "Retrigger all card held in hand abilities" },
                            new PickerItem { Name = "Credit Card", Icon = "💳", Description = "Go up to -$20 in debt" },
                        }
                    },
                    new ItemCategory
                    {
                        Name = "Legendary",
                        Items = new List<PickerItem>
                        {
                            new PickerItem { Name = "Canio", Icon = "🎪", Description = "X1 Mult when a face card is destroyed" },
                            new PickerItem { Name = "Triboulet", Icon = "👑", Description = "Played Kings and Queens each give X2 Mult" },
                            new PickerItem { Name = "Yorick", Icon = "💀", Description = "X5 Mult, Discard 1 random card per discard" },
                            new PickerItem { Name = "Chicot", Icon = "🎨", Description = "Disables effect of every Boss Blind" },
                            new PickerItem { Name = "Perkeo", Icon = "🍷", Description = "Creates a Negative copy of 1 random consumable after leaving shop" },
                        }
                    }
                };
                
            case "Tarot":
                return new List<ItemCategory>
                {
                    new ItemCategory
                    {
                        Name = "Tarot Cards",
                        Items = new List<PickerItem>
                        {
                            new PickerItem { Name = "The Fool", Icon = "0️⃣", Description = "Creates the last tarot or planet card used" },
                            new PickerItem { Name = "The Magician", Icon = "1️⃣", Description = "Enhances 1 selected card into a Lucky Card" },
                            new PickerItem { Name = "The High Priestess", Icon = "2️⃣", Description = "Creates up to 2 random Planet cards" },
                            new PickerItem { Name = "The Empress", Icon = "3️⃣", Description = "Enhances 2 selected cards into Mult Cards" },
                            new PickerItem { Name = "The Emperor", Icon = "4️⃣", Description = "Creates up to 2 random Tarot cards" },
                            new PickerItem { Name = "The Hierophant", Icon = "5️⃣", Description = "Enhances 2 selected cards into Bonus Cards" },
                            new PickerItem { Name = "The Lovers", Icon = "6️⃣", Description = "Enhances 1 selected card into a Wild Card" },
                            new PickerItem { Name = "The Chariot", Icon = "7️⃣", Description = "Enhances 1 selected card into a Steel Card" },
                            new PickerItem { Name = "Justice", Icon = "⚖️", Description = "Enhances 1 selected card into a Glass Card" },
                            new PickerItem { Name = "The Hermit", Icon = "9️⃣", Description = "Doubles money (Max of $20)" },
                            new PickerItem { Name = "Wheel of Fortune", Icon = "🎡", Description = "1 in 4 chance to add Foil, Holographic, or Polychrome" },
                        }
                    }
                };
                
            case "Spectral":
                return new List<ItemCategory>
                {
                    new ItemCategory
                    {
                        Name = "Spectral Cards",
                        Items = new List<PickerItem>
                        {
                            new PickerItem { Name = "Familiar", Icon = "🐈", Description = "Destroy 1 random card in hand, add 3 random Enhanced face cards" },
                            new PickerItem { Name = "Grim", Icon = "💀", Description = "Destroy 1 random card in hand, add 2 random Enhanced Aces" },
                            new PickerItem { Name = "Incantation", Icon = "📿", Description = "Destroy 1 random card in hand, add 4 random Enhanced numbered cards" },
                            new PickerItem { Name = "Talisman", Icon = "🔮", Description = "Add a Gold Seal to 1 selected card" },
                            new PickerItem { Name = "Aura", Icon = "✨", Description = "Add Foil, Holographic, or Polychrome to 1 selected card" },
                            new PickerItem { Name = "Wraith", Icon = "👻", Description = "Creates a random Rare Joker" },
                            new PickerItem { Name = "Sigil", Icon = "🌟", Description = "Converts all cards in hand to a single random suit" },
                        }
                    }
                };
                
            case "Tag":
                return new List<ItemCategory>
                {
                    new ItemCategory
                    {
                        Name = "Tags",
                        Items = new List<PickerItem>
                        {
                            new PickerItem { Name = "Uncommon Tag", Icon = "🟢", Description = "Shop has a free Uncommon Joker" },
                            new PickerItem { Name = "Rare Tag", Icon = "🔴", Description = "Shop has a free Rare Joker" },
                            new PickerItem { Name = "Negative Tag", Icon = "➖", Description = "Next Joker in shop is free and becomes Negative" },
                            new PickerItem { Name = "Foil Tag", Icon = "✨", Description = "Next Joker in shop is free and becomes Foil" },
                            new PickerItem { Name = "Holographic Tag", Icon = "🎆", Description = "Next Joker in shop is free and becomes Holographic" },
                            new PickerItem { Name = "Polychrome Tag", Icon = "🌈", Description = "Next Joker in shop is free and becomes Polychrome" },
                            new PickerItem { Name = "Investment Tag", Icon = "💰", Description = "After defeating the Boss Blind, gain $25" },
                            new PickerItem { Name = "Voucher Tag", Icon = "🎫", Description = "Adds one Voucher to the next shop" },
                            new PickerItem { Name = "Boss Tag", Icon = "👹", Description = "Rerolls the Boss Blind" },
                        }
                    }
                };
                
            case "Voucher":
                return new List<ItemCategory>
                {
                    new ItemCategory
                    {
                        Name = "Vouchers",
                        Items = new List<PickerItem>
                        {
                            new PickerItem { Name = "Overstock", Icon = "📦", Description = "+1 card slot in shop" },
                            new PickerItem { Name = "Clearance Sale", Icon = "🏷️", Description = "All cards and packs in shop are 25% off" },
                            new PickerItem { Name = "Hone", Icon = "🔪", Description = "Foil, Holographic, and Polychrome cards appear 2X more often" },
                            new PickerItem { Name = "Reroll Surplus", Icon = "🎲", Description = "Rerolls cost $2 less" },
                            new PickerItem { Name = "Crystal Ball", Icon = "🔮", Description = "+1 consumable slot" },
                            new PickerItem { Name = "Telescope", Icon = "🔭", Description = "Celestial Packs always have the Planet card for your most played hand" },
                            new PickerItem { Name = "Grabber", Icon = "🤏", Description = "+1 hand per round" },
                            new PickerItem { Name = "Wasteful", Icon = "🗑️", Description = "+1 discard per round" },
                        }
                    }
                };
                
            default:
                return new List<ItemCategory>();
        }
    }
}

public class ItemCategory
{
    public string Name { get; set; } = "";
    public List<PickerItem> Items { get; set; } = new();
}

public class PickerItem
{
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Description { get; set; } = "";
}
