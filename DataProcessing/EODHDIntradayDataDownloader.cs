/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using Newtonsoft.Json;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Logging;
using QuantConnect.Securities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace QuantConnect.DataProcessing;

/// <summary>
/// EODHDIntradayDataDownloader implementation.
/// Downloads 1-minute intraday history for a configurable list of symbols
/// and writes it as LEAN minute quote data, e.g. forex/oanda/minute/usdkrw/20260802_quote.zip
/// </summary>
public class EODHDIntradayDataDownloader : EODHDBaseDataDownloader
{
    // The EODHD 1-minute endpoint allows at most 120 days per request
    private const int _maxChunkDays = 120;
    private const string _interval = "1m";

    // The vendor occasionally returns junk prints, e.g. an isolated 2.4 bar for USDKRW on 2024-01-02
    // while the pair traded around 1300. Bars this far away from the prevailing price level are dropped:
    // the widest genuine intraday deviation found in the whole USDKRW minute history is about 10%
    private const decimal _maxRelativeDeviation = 0.25m;

    // A day needs at least this many bars for its own median to be a meaningful price level reference.
    // Sparser days fall back to the median of the whole downloaded window
    private const int _minimumBarsForDailyReference = 10;

    private readonly string _dataFolder;
    private readonly string[] _symbols;

    /// <summary>
    /// Dataset name
    /// </summary>
    public override string VendorDataName => "intraday";

    /// <summary>
    /// Dataset endpoint. The symbol is appended to the path per request
    /// </summary>
    protected override string Endpoint => $"https://eodhd.com/api/{VendorDataName}/";

    /// <summary>
    /// Creates a new instance of <see cref="EODHDIntradayDataDownloader"/>
    /// </summary>
    /// <param name="destinationFolder">The folder where alternative data would be saved</param>
    /// <param name="dataFolder">The LEAN data folder where the minute quote data will be saved</param>
    /// <param name="apiKey">The Vendor API key</param>
    /// <param name="deploymentDate">The deployment date. Data is processed up to the end of the previous day</param>
    /// <param name="symbols">The EODHD symbols to download, e.g. USDKRW.FOREX</param>
    public EODHDIntradayDataDownloader(string destinationFolder, string dataFolder, string apiKey, DateTime deploymentDate, IEnumerable<string> symbols)
        : base(destinationFolder, apiKey, deploymentDate)
    {
        _dataFolder = dataFolder;
        _symbols = symbols.ToArray();
    }

    /// <summary>
    /// Runs the instance of the object.
    /// </summary>
    /// <param name="processDate">The first date of data to be processed</param>
    /// <returns>True if process all downloads successfully</returns>
    public override async Task<bool> Run(DateTime processDate)
    {
        var success = true;
        // The deployment date itself is still in progress: process up to the end of the previous day
        var endOfRange = DeploymentDate.Date.AddSeconds(-1);
        var marketHoursDatabase = MarketHoursDatabase.FromDataFolder();

        foreach (var vendorTicker in _symbols)
        {
            var stopwatch = Stopwatch.StartNew();
            var symbol = Symbol.Create(vendorTicker.Split('.')[0], SecurityType.Forex, Market.Oanda);
            var dataTimeZone = marketHoursDatabase.GetDataTimeZone(Market.Oanda, symbol, SecurityType.Forex);
            var writer = new LeanDataWriter(Resolution.Minute, symbol, _dataFolder, TickType.Quote, writePolicy: WritePolicy.Merge);
            var windowStart = processDate.Date;

            Log.Trace($"EODHDIntradayDataDownloader.Run(): Start processing {vendorTicker} {_interval} data from {windowStart:yyyyMMdd} to {endOfRange:yyyyMMdd}");

            while (windowStart <= endOfRange)
            {
                var windowEnd = windowStart.AddDays(_maxChunkDays).AddSeconds(-1);
                if (windowEnd > endOfRange)
                {
                    windowEnd = endOfRange;
                }

                // A failed window does not abort the rest: log it and continue, a re-run merges the hole back in
                try
                {
                    var from = new DateTimeOffset(windowStart, TimeSpan.Zero).ToUnixTimeSeconds();
                    var to = new DateTimeOffset(windowEnd, TimeSpan.Zero).ToUnixTimeSeconds();

                    var result = await HttpRequester($"{vendorTicker}?interval={_interval}&fmt=json&from={from}&to={to}");
                    if (string.IsNullOrWhiteSpace(result))
                    {
                        Log.Error($"EODHDIntradayDataDownloader.Run(): No data received for {vendorTicker} between {windowStart:yyyyMMdd} and {windowEnd:yyyyMMdd}");
                        success = false;
                        windowStart = windowEnd.AddSeconds(1);
                        continue;
                    }

                    // The vendor response may contain duplicate and out-of-window timestamps: deduplicate keyed
                    // by time, keeping the last occurrence like the LeanDataWriter merge policy does
                    var quoteBars = new Dictionary<DateTime, QuoteBar>();
                    foreach (var bar in JsonConvert.DeserializeObject<List<EODHDIntradayBar>>(result, JsonSerializerSettings)
                        .Where(x => x.Open.HasValue && x.High.HasValue && x.Low.HasValue && x.Close.HasValue))
                    {
                        // The vendor timestamps are documented as UTC but are actually New York wall-clock time:
                        // prices match true-UTC references at a +4h/+5h (EDT/EST) shift, and the FX weekly
                        // close/open anchors at 17:00 labeled time year-round. The request window bounds share
                        // the vendor convention, so filter before converting to the data time zone.
                        var vendorTime = DateTimeOffset.FromUnixTimeSeconds(bar.Timestamp).UtcDateTime;
                        if (vendorTime < windowStart || vendorTime > windowEnd)
                        {
                            continue;
                        }
                        var time = vendorTime.ConvertToUtc(TimeZones.NewYork).ConvertFromUtc(dataTimeZone);
                        quoteBars[time] = new QuoteBar(time, symbol,
                            new Bar(bar.Open.Value, bar.High.Value, bar.Low.Value, bar.Close.Value), 0m,
                            new Bar(bar.Open.Value, bar.High.Value, bar.Low.Value, bar.Close.Value), 0m,
                            Time.OneMinute);
                    }

                    // Save after every window so an interrupted run keeps the completed windows
                    var bars = RemoveOutliers([.. quoteBars.Values.OrderBy(x => x.Time)]);
                    if (bars.Count != 0)
                    {
                        writer.Write(bars);
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e, $"EODHDIntradayDataDownloader.Run(): Failed processing {vendorTicker} between {windowStart:yyyyMMdd} and {windowEnd:yyyyMMdd}");
                    success = false;
                }

                windowStart = windowEnd.AddSeconds(1);
            }

            Log.Trace($"EODHDIntradayDataDownloader.Run(): Finished processing {vendorTicker} in {stopwatch.Elapsed.TotalSeconds:f3} seconds");
        }

        return success;
    }

    /// <summary>
    /// Discards the bars whose prices are implausibly far from the prevailing price level.
    /// The vendor sporadically returns junk prints that would otherwise be written as real quotes,
    /// e.g. an isolated 2.4 bar for USDKRW on 2024-01-02, a day the pair traded around 1300.
    /// The reference level is the median close of the bar's own day, so a genuine trend across the
    /// downloaded window never triggers the filter. Days with too few bars to give a meaningful
    /// median fall back to the median of the whole window
    /// </summary>
    /// <param name="bars">The bars to filter, in chronological order</param>
    /// <returns>The bars within <see cref="_maxRelativeDeviation"/> of their reference price level</returns>
    public static List<QuoteBar> RemoveOutliers(List<QuoteBar> bars)
    {
        if (bars.Count == 0)
        {
            return bars;
        }

        var windowReference = GetMedian([.. bars.Select(x => x.Close)]);
        var result = new List<QuoteBar>(bars.Count);

        foreach (var dailyBars in bars.GroupBy(x => x.Time.Date))
        {
            List<decimal> closes = [.. dailyBars.Select(x => x.Close)];
            var reference = closes.Count >= _minimumBarsForDailyReference ? GetMedian(closes) : windowReference;
            if (reference <= 0)
            {
                // Without a usable reference the bars are kept: dropping a whole day on bad input would be worse
                result.AddRange(dailyBars);
                continue;
            }

            var upperBound = reference * (1 + _maxRelativeDeviation);
            var lowerBound = reference * (1 - _maxRelativeDeviation);

            foreach (var bar in dailyBars)
            {
                // Open and close are not guaranteed to sit within the high/low of a malformed bar
                var highest = Math.Max(Math.Max(bar.Open, bar.High), Math.Max(bar.Low, bar.Close));
                var lowest = Math.Min(Math.Min(bar.Open, bar.High), Math.Min(bar.Low, bar.Close));

                if (highest > upperBound || lowest < lowerBound)
                {
                    Log.Error($"EODHDIntradayDataDownloader.RemoveOutliers(): Discarding {bar.Symbol.Value} outlier bar " +
                        $"{bar.Time:yyyyMMdd HH:mm:ss} O={bar.Open} H={bar.High} L={bar.Low} C={bar.Close}. " +
                        $"Reference price level: {reference}");
                    continue;
                }

                result.Add(bar);
            }
        }

        return result;
    }

    /// <summary>
    /// Gets the median of the given values. The list is sorted in place
    /// </summary>
    private static decimal GetMedian(List<decimal> values)
    {
        values.Sort();
        var middle = values.Count / 2;
        return values.Count % 2 != 0 ? values[middle] : (values[middle - 1] + values[middle]) / 2m;
    }

    /// <summary>
    /// Represents items of the intraday bar list returned by EODHD
    /// </summary>
    private class EODHDIntradayBar
    {
        [JsonProperty("timestamp")]
        public long Timestamp { get; set; }

        [JsonProperty("open")]
        public decimal? Open { get; set; }

        [JsonProperty("high")]
        public decimal? High { get; set; }

        [JsonProperty("low")]
        public decimal? Low { get; set; }

        [JsonProperty("close")]
        public decimal? Close { get; set; }
    }
}
