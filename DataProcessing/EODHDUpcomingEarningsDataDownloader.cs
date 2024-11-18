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
using QuantConnect.Data.Auxiliary;
using QuantConnect.DataSource;
using QuantConnect.Interfaces;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Logging;
using QuantConnect.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace QuantConnect.DataProcessing;

/// <summary>
/// EODHDUpcomingEarningsDataDownloader implementation.
/// </summary>
public class EODHDUpcomingEarningsDataDownloader : EODHDBaseDataDownloader
{
    private readonly IMapFileProvider _mapFileProvider;

    /// <summary>
    /// Dataset endpoint
    /// </summary>
    protected override string Endpoint => "https://eodhd.com/api/calendar/earnings";

    /// <summary>
    /// Dataset name
    /// </summary>
    public override string VendorDataName => "upcomingearnings";

    /// <summary>
    /// Creates a new instance of <see cref="EODHDUpcomingEarningsDataDownloader"/>
    /// </summary>
    /// <param name="destinationFolder">The folder where the data will be saved</param>
    /// <param name="apiKey">The Vendor API key</param>
    public EODHDUpcomingEarningsDataDownloader(string destinationFolder, string apiKey, DateTime deplopymentDate)
        : base(destinationFolder, apiKey, deplopymentDate)
    {
        _mapFileProvider = new LocalZipMapFileProvider();
        _mapFileProvider.Initialize(new DefaultDataProvider());
    }

    /// <summary>
    /// Runs the instance of the object.
    /// </summary>
    /// <param name="processDate">The date of data to be processed</param>
    /// <returns>True if process all downloads successfully</returns>
    public override async Task<bool> Run(DateTime processDate)
    {
        var success = true;
        
        while (processDate <= DeploymentDate)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // We would like to get earnings for the upcoming 7 days
                var endDate = processDate.AddDays(7);
                Log.Trace($"EODHDUpcomingEarningsDataDownloader.Run(): Start processing earnings that will report from {processDate:yyyyMMdd} to {endDate:yyyyMMdd}");

                var result = await HttpRequester($"?from={processDate:yyyy-MM-dd}&to={endDate:yyyy-MM-dd}&fmt=json");

                if (string.IsNullOrWhiteSpace(result))
                {
                    Log.Error($"EODHDUpcomingEarningsDataDownloader.Run(): No data received");
                    success = false;
                    continue;
                }

                var metadata = JsonConvert.DeserializeObject<EODHDUpcomingEarningsMetadata>(result, JsonSerializerSettings);
                var csvContents = new List<string>();

                // Only primary stocks traded in US
                foreach (var earning in metadata.Earnings.Where(x => x.Ticker.EndsWith(".US") && x.Currency?.Trim() == "USD"))
                {
                    var ticker = earning.Ticker.Remove(earning.Ticker.Length - 3);      // Remove the last 3 char ".US"
                    if (!TryNormalizeDefunctTicker(ticker, out var nonDefunctTicker))
                    {
                        // If not valid ticker, skip
                        continue;
                    }
                    var reportDate = earning.ReportDate;
                    // If the report date is behind, skip
                    if (reportDate < processDate)
                    {
                        continue;
                    }

                    var sid = SecurityIdentifier.GenerateEquity(nonDefunctTicker, Market.USA, true, _mapFileProvider, processDate);
                    // Skip unmapped equities (date prior to 1998)
                    if (sid.Date.Year < 1998)
                    {
                        continue;
                    }

                    var estimate = earning.Estimate.HasValue ? $"{earning.Estimate}" : string.Empty;

                    csvContents.Add($"{sid},{nonDefunctTicker},{reportDate:yyyyMMdd},{earning.ReportTime},{estimate}");
                }

                if (csvContents.Count != 0)
                {
                    SaveContentToFile(string.Empty, $"{processDate.AddDays(-1):yyyyMMdd}", csvContents);
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
                success = false;
                continue;
            }

            Log.Trace($"EODHDUpcomingEarningsDataDownloader.Run(): Finished in {stopwatch.Elapsed.ToStringInvariant(null)}");
            processDate = processDate.AddDays(1);
        }
        return success;
    }

    /// <summary>
    /// Represents JSON output return of the raw request
    /// </summary>
    private class EODHDUpcomingEarningsMetadata
    {
        [JsonProperty("earnings")]
        public List<EODHDUpcomingEarningsData> Earnings { get; set; }
    }

    /// <summary>
    /// Represents items of the list of upcoming earnings supported by EODHD
    /// </summary>
    private class EODHDUpcomingEarningsData
    {
        [JsonProperty("code")]
        public string Ticker { get; set; }
        
        [JsonProperty("report_date")]
        [JsonConverter(typeof(DateTimeJsonConverter), "yyyy-MM-dd")]
        public DateTime ReportDate { get; set; }
        
        [JsonProperty("before_after_market")]
        public EODHD.ReportTime ReportTime { get; set; }
        
        [JsonProperty("currency")]
        public string Currency { get; set; }

        [JsonProperty("estimate")]
        public decimal? Estimate { get; set; }
    }
}