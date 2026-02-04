using System;
using System.Collections.Generic;
using System.Linq;
using Motely.Filters;

namespace Motely.Executors;

/// <summary>
/// Consolidates common search settings configuration that was previously duplicated across executors.
/// </summary>
public static class SearchSettingsBuilder
{
    public static MotelySearchSettings<TFilter> ApplyCommonSettings<TFilter>(
        MotelySearchSettings<TFilter> settings,
        JsonSearchParams parameters,
        Action<MotelyProgress>? progressCallback = null
    )
        where TFilter : struct, IMotelySeedFilter
    {
        settings = settings
            .WithThreadCount(parameters.Threads)
            .WithBatchCharacterCount(parameters.BatchSize)
            .WithStartBatchIndex((long)parameters.StartBatch)
            .WithCsvOutput(true);

        if (parameters.EndBatch > 0)
        {
            settings = settings.WithEndBatchIndex((long)parameters.EndBatch + 1);
        }

        if (parameters.Quiet)
        {
            settings = settings.WithQuietMode(true);
        }

        if (progressCallback != null)
        {
            settings = settings.WithProgressCallback(progressCallback);
        }
        else if (parameters.ProgressCallback != null)
        {
            settings = settings.WithProgressCallback(parameters.ProgressCallback);
        }

        if (
            parameters.Deck != null
            && Enum.TryParse<MotelyDeck>(parameters.Deck, true, out var parsedDeck)
        )
        {
            settings = settings.WithDeck(parsedDeck);
        }

        if (
            parameters.Stake != null
            && Enum.TryParse<MotelyStake>(parameters.Stake, true, out var parsedStake)
        )
        {
            settings = settings.WithStake(parsedStake);
        }

        return settings;
    }
}
