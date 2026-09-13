using System;
using System.Collections.Generic;
using System.Linq;

namespace ForexPanel.Core;

/// <summary>
/// Real OHLC aggregation from an M1 (1-minute) baseline into any higher timeframe.
/// This is the actual candle calculation engine: Open = first bar's open, High = max high,
/// Low = min low, Close = last bar's close, Volume = sum of volumes in the bucket.
/// Deliberately has no dependency on any live data source (MT4 or otherwise) - it only
/// operates on whatever M1 candles it is given, so it works the same whether the M1 data
/// came from a generator, a CSV, a database, or a live feed later.
/// </summary>
public static class CandleResampler
{
    /// <summary>
    /// Resamples M1 candles into the requested timeframe.
    /// - 1 minute: returned as-is (passthrough).
    /// - Sub-day timeframes (M5/M15/M30/H1/H4, i.e. less than 1440 minutes): fixed-size buckets,
    ///   floored to the nearest multiple of targetMinutes within each hour boundary.
    /// - Daily and above (targetMinutes >= 1440): calendar-based buckets (one candle per calendar day),
    ///   since a "day" is not simply a fixed number of minutes once weekends/DST are involved.
    /// </summary>
    public static List<Candle> Resample(IReadOnlyList<Candle> m1Candles, int targetMinutes)
    {
        if (m1Candles == null || m1Candles.Count == 0)
            return new List<Candle>();

        if (targetMinutes <= 1)
            return m1Candles.ToList();

        return targetMinutes >= 1440
            ? ResampleByCalendarDay(m1Candles)
            : ResampleByFixedMinutes(m1Candles, targetMinutes);
    }

    private static List<Candle> ResampleByFixedMinutes(IReadOnlyList<Candle> source, int minutes)
    {
        List<Candle> result = new();
        List<Candle> bucket = new();
        DateTime? bucketStart = null;

        foreach (Candle c in source)
        {
            int flooredMinute = (c.Time.Minute / minutes) * minutes;
            DateTime thisBucketStart = new DateTime(
                c.Time.Year, c.Time.Month, c.Time.Day, c.Time.Hour, 0, 0)
                .AddMinutes(flooredMinute);

            if (bucketStart == null)
            {
                bucketStart = thisBucketStart;
            }
            else if (thisBucketStart != bucketStart.Value)
            {
                result.Add(BuildCandle(bucket, bucketStart.Value));
                bucket.Clear();
                bucketStart = thisBucketStart;
            }

            bucket.Add(c);
        }

        if (bucket.Count > 0 && bucketStart.HasValue)
            result.Add(BuildCandle(bucket, bucketStart.Value));

        return result;
    }

    private static List<Candle> ResampleByCalendarDay(IReadOnlyList<Candle> source)
    {
        List<Candle> result = new();
        List<Candle> bucket = new();
        DateTime? bucketDay = null;

        foreach (Candle c in source)
        {
            DateTime day = c.Time.Date;

            if (bucketDay == null)
            {
                bucketDay = day;
            }
            else if (day != bucketDay.Value)
            {
                result.Add(BuildCandle(bucket, bucketDay.Value));
                bucket.Clear();
                bucketDay = day;
            }

            bucket.Add(c);
        }

        if (bucket.Count > 0 && bucketDay.HasValue)
            result.Add(BuildCandle(bucket, bucketDay.Value));

        return result;
    }

    private static Candle BuildCandle(List<Candle> bucket, DateTime bucketTime)
    {
        return new Candle
        {
            Time = bucketTime,
            Open = bucket[0].Open,
            High = bucket.Max(c => c.High),
            Low = bucket.Min(c => c.Low),
            Close = bucket[^1].Close,
            Volume = bucket.Sum(c => c.Volume)
        };
    }
}
