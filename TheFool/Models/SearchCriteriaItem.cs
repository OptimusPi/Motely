using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TheFool.Models;

public partial class SearchCriteriaItem : ObservableObject
{
    [ObservableProperty]
    private string _type = "Joker";

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _icon = "🃏";

    [ObservableProperty]
    private string? _details;

    [ObservableProperty]
    private bool _hasDetails;

    [ObservableProperty]
    private bool _showAntes;

    [ObservableProperty]
    private bool _ante1 = true;

    [ObservableProperty]
    private bool _ante2 = true;

    [ObservableProperty]
    private bool _ante3 = true;

    [ObservableProperty]
    private bool _ante4 = true;

    [ObservableProperty]
    private bool _ante5 = true;

    [ObservableProperty]
    private bool _ante6 = true;

    [ObservableProperty]
    private bool _ante7;

    [ObservableProperty]
    private bool _ante8;

    [ObservableProperty]
    private bool _isRequired;

    partial void OnTypeChanged(string value)
    {
        // Update icon based on type
        Icon = value switch
        {
            "Joker" => "🃏",
            "Tarot" => "🌟",
            "Spectral" => "👻",
            "Tag" => "🏷️",
            "Voucher" => "🎫",
            "Card" => "🂠",
            _ => "❓"
        };

        // Show antes only for Jokers
        ShowAntes = value == "Joker";
    }

    partial void OnIsRequiredChanged(bool value)
    {
        // Property change notification is handled automatically by ObservableProperty
    }

    public string GetFilterString()
    {
        if (Type == "Card" && !string.IsNullOrEmpty(Details))
        {
            // For cards, combine rank and suit
            return $"{Name} of {Details}";
        }
        
        if (HasDetails && !string.IsNullOrEmpty(Details))
        {
            // For editions
            return $"{Name} ({Details})";
        }

        return Name;
    }

    public string GetAnteString()
    {
        if (!ShowAntes) return "";

        var antes = new List<int>();
        if (Ante1) antes.Add(1);
        if (Ante2) antes.Add(2);
        if (Ante3) antes.Add(3);
        if (Ante4) antes.Add(4);
        if (Ante5) antes.Add(5);
        if (Ante6) antes.Add(6);
        if (Ante7) antes.Add(7);
        if (Ante8) antes.Add(8);

        return string.Join(",", antes);
    }
}
