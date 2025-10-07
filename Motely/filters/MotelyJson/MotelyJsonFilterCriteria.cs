using System;
using System.Collections.Generic;

namespace Motely.Filters;

/// <summary>
/// Criteria DTO for Joker filters - pre-aggregates all cross-clause calculations
/// </summary>
public readonly struct MotelyJsonJokerFilterCriteria
{
    public List<MotelyJsonJokerFilterClause> Clauses { get; init; }
    public int MinAnte { get; init; }
    public int MaxAnte { get; init; }
    public int MaxShopSlotsNeeded { get; init; }
}

/// <summary>
/// Criteria DTO for Tarot filters - pre-aggregates all cross-clause calculations
/// </summary>
public readonly struct MotelyJsonTarotFilterCriteria
{
    public List<MotelyJsonTarotFilterClause> Clauses { get; init; }
    public int MinAnte { get; init; }
    public int MaxAnte { get; init; }
    public int MaxShopSlotsNeeded { get; init; }
}

/// <summary>
/// Criteria DTO for Spectral filters - pre-aggregates all cross-clause calculations
/// </summary>
public readonly struct MotelyJsonSpectralFilterCriteria
{
    public List<MotelyJsonSpectralFilterClause> Clauses { get; init; }
    public int MinAnte { get; init; }
    public int MaxAnte { get; init; }
    public int MaxShopSlotsNeeded { get; init; }
}

/// <summary>
/// Criteria DTO for Planet filters - pre-aggregates all cross-clause calculations
/// </summary>
public readonly struct MotelyJsonPlanetFilterCriteria
{
    public List<MotelyJsonPlanetFilterClause> Clauses { get; init; }
    public int MinAnte { get; init; }
    public int MaxAnte { get; init; }
    public int MaxShopSlotsNeeded { get; init; }
}

/// <summary>
/// Criteria DTO for SoulJoker filters - pre-aggregates all cross-clause calculations
/// </summary>
public readonly struct MotelyJsonSoulJokerFilterCriteria
{
    public List<MotelyJsonSoulJokerFilterClause> Clauses { get; init; }
    public int MinAnte { get; init; }
    public int MaxAnte { get; init; }
    public Dictionary<int, int> MaxPackSlotsPerAnte { get; init; }  // Pre-calculated max pack slots for each ante
}

/// <summary>
/// Criteria DTO for Voucher filters - pre-aggregates all cross-clause calculations
/// </summary>
public readonly struct MotelyJsonVoucherFilterCriteria
{
    public List<MotelyJsonVoucherFilterClause> Clauses { get; init; }
    public int MinAnte { get; init; }
    public int MaxAnte { get; init; }
}

/// <summary>
/// Criteria DTO for Boss filters - pre-aggregates all cross-clause calculations
/// </summary>
public readonly struct MotelyJsonBossFilterCriteria
{
    public List<MotelyJsonConfig.MotleyJsonFilterClause> Clauses { get; init; }
    public int MinAnte { get; init; }
    public int MaxAnte { get; init; }
}

/// <summary>
/// Criteria DTO for Tag filters - pre-aggregates all cross-clause calculations
/// </summary>
public readonly struct MotelyJsonTagFilterCriteria
{
    public List<MotelyJsonConfig.MotleyJsonFilterClause> Clauses { get; init; }
    public int MinAnte { get; init; }
    public int MaxAnte { get; init; }
}

/// <summary>
/// Criteria DTO for PlayingCard filters - pre-aggregates all cross-clause calculations
/// </summary>
public readonly struct MotelyJsonPlayingCardFilterCriteria
{
    public List<MotelyJsonConfig.MotleyJsonFilterClause> Clauses { get; init; }
    public int MinAnte { get; init; }
    public int MaxAnte { get; init; }
}
