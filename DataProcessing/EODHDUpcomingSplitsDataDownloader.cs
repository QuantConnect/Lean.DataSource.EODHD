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
/// EODHDUpcomingSplitsDataDownloader implementation.
/// </summary>
public class EODHDUpcomingSplitsDataDownloader : EODHDBaseDataDownloader
{        
    private readonly IMapFileProvider _mapFileProvider;

    /// <summary>
    /// Dataset endpoint
    /// </summary>
    protected override string Endpoint => "https://eodhd.com/api/calendar/splits";

    /// <summary>
    /// Dataset name
    /// </summary>
    public override string VendorDataName => "upcomingsplits";

    /// <summary>
    /// Creates a new instance of <see cref="EODHDUpcomingSplitsDataDownloader"/>
    /// </summary>
    /// <param name="destinationFolder">The folder where the data will be saved</param>
    /// <param name="apiKey">The Vendor API key</param>
    public EODHDUpcomingSplitsDataDownloader(string destinationFolder, string apiKey, DateTime deplopymentDate)
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
                Log.Trace($"EODHDUpcomingSplitsDataDownloader.Run(): Start processing splits that will happen from {processDate:yyyyMMdd} to {endDate:yyyyMMdd}");

                var result = await HttpRequester($"?from={processDate:yyyy-MM-dd}&to={endDate:yyyy-MM-dd}&fmt=json");
                if (string.IsNullOrWhiteSpace(result))
                {
                    Log.Error($"EODHDUpcomingSplitsDataDownloader.Run(): No data received");
                    success = false;
                    continue;
                }

                var metadata = JsonConvert.DeserializeObject<EODHDUpcomingSplitsMetadata>(result, JsonSerializerSettings);
                var csvContents = new List<string>();

                foreach (var split in metadata.Splits.Where(x => x.Ticker.EndsWith(".US")))
                {
                    var ticker = split.Ticker.Remove(split.Ticker.Length - 3);      // Remove the last 3 char ".US"
                    if (!TryNormalizeDefunctTicker(ticker, out var nonDefunctTicker))
                    {
                        // If not valid ticker, skip
                        continue;
                    }
                    var splitDate = split.SplitDate;
                    // If the split date is behind or split factor is invalid, skip
                    if (splitDate < processDate || split.NewShares == 0)
                    {
                        continue;
                    }

                    var sid = SecurityIdentifier.GenerateEquity(nonDefunctTicker, Market.USA, true, _mapFileProvider, processDate);
                    // Skip unmapped equities
                    if (sid.Date.Year < 1998)
                    {
                        continue;
                    }

                    csvContents.Add($"{sid},{nonDefunctTicker},{splitDate:yyyyMMdd},{split.Optionable},{split.OldShares / split.NewShares}");
                }

                if (csvContents.Count != 0)
                {
                    SaveContentToFile(string.Empty, processDate, csvContents);
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
                success = false;
                continue;
            }

            Log.Trace($"EODHDUpcomingSplitsDataDownloader.Run(): Finished in {stopwatch.Elapsed.TotalSeconds:f3} seconds");
            processDate = processDate.AddDays(1);
        }
        return success;
    }

    /// <summary>
    /// Represents JSON output return of the raw request
    /// </summary>
    private class EODHDUpcomingSplitsMetadata
    {
        [JsonProperty("splits")]
        public List<EODHDUpcomingSplitsData> Splits { get; set; }
    }

    /// <summary>
    /// Represents items of the list of upcoming splits supported by EODHD
    /// </summary>
    private class EODHDUpcomingSplitsData
    {
        [JsonProperty("code")]
        public string Ticker { get; set; }
        
        [JsonProperty("split_date")]
        [JsonConverter(typeof(DateTimeJsonConverter), "yyyy-MM-dd")]
        public DateTime SplitDate { get; set; }
        
        [JsonProperty("optionable")]
        public string Optionable { get; set; }
        
        [JsonProperty("old_shares")]
        public decimal OldShares { get; set; }

        [JsonProperty("new_shares")]
        public decimal NewShares { get; set; }
    }
}