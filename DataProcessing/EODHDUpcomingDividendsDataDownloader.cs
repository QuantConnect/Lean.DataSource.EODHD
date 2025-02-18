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
using System.Threading.Tasks;

namespace QuantConnect.DataProcessing;

/// <summary>
/// EODHDUpcomingDividendsDataDownloader implementation.
/// </summary>
public class EODHDUpcomingDividendsDataDownloader : EODHDBaseDataDownloader
{        
    private readonly IMapFileProvider _mapFileProvider;

    /// <summary>
    /// Dataset endpoint
    /// </summary>
    protected override string Endpoint => "https://eodhd.com/api/eod-bulk-last-day/US";

    /// <summary>
    /// Dataset name
    /// </summary>
    public override string VendorDataName => "upcomingdividends";

    /// <summary>
    /// Creates a new instance of <see cref="EODHDUpcomingDividendsDataDownloader"/>
    /// </summary>
    /// <param name="destinationFolder">The folder where the data will be saved</param>
    /// <param name="apiKey">The Vendor API key</param>
    public EODHDUpcomingDividendsDataDownloader(string destinationFolder, string apiKey, DateTime deplopymentDate)
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
        var entryDate = processDate;

        while (processDate <= DeploymentDate)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // We would like to get earnings for the upcoming 7 days
                var endDate = processDate.AddDays(7);
                Log.Trace($"EODHDUpcomingDividendsDataDownloader.Run(): Start processing dividends that will happen from {processDate:yyyyMMdd} to {endDate:yyyyMMdd}");

                var result = await HttpRequester($"?date={processDate:yyyy-MM-dd}&type=dividends&fmt=json");
                if (string.IsNullOrWhiteSpace(result))
                {
                    Log.Error($"EODHDUpcomingDividendsDataDownloader.Run(): No data received");
                    success = false;
                    continue;
                }

                var data = JsonConvert.DeserializeObject<List<EODHDUpcomingDividendsData>>(result, JsonSerializerSettings);
                var csvContents = new List<string>();

                foreach (var dividend in data)
                {
                    if (!TryNormalizeDefunctTicker(dividend.Ticker, out var nonDefunctTicker))
                    {
                        // If not valid ticker, skip
                        continue;
                    }
                    var dividendDate = dividend.DividendDate;
                    // If the dividend date is behind or dividend factor is invalid, skip
                    if (dividendDate < processDate)
                    {
                        continue;
                    }

                    var sid = SecurityIdentifier.GenerateEquity(nonDefunctTicker, Market.USA, true, _mapFileProvider, processDate);
                    // Skip unmapped equities
                    if (sid.Date.Year < 1998)
                    {
                        continue;
                    }

                    csvContents.Add($"{sid},{nonDefunctTicker},{dividendDate:yyyyMMdd},{dividend.Dividend},{dividend.DeclarationDate:yyyyMMdd},{dividend.RecordDate:yyyyMMdd},{dividend.PaymentDate:yyyyMMdd}");
                }

                if (csvContents.Count != 0)
                {
                    SaveContentToFile(string.Empty, entryDate, csvContents);
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
                success = false;
                continue;
            }

            Log.Trace($"EODHDUpcomingDividendsDataDownloader.Run(): Finished in {stopwatch.Elapsed.TotalSeconds:f3} seconds");
            processDate = processDate.AddDays(1);
        }
        return success;
    }

    /// <summary>
    /// Represents items of the list of upcoming dividends supported by EODHD
    /// </summary>
    private class EODHDUpcomingDividendsData
    {
        [JsonProperty("code")]
        public string Ticker { get; set; }
        
        [JsonProperty("date")]
        [JsonConverter(typeof(DateTimeJsonConverter), "yyyy-MM-dd")]
        public DateTime DividendDate { get; set; }
        
        [JsonProperty("declarationDate")]
        [JsonConverter(typeof(DateTimeJsonConverter), "yyyy-MM-dd")]
        public DateTime DeclarationDate { get; set; }
        
        [JsonProperty("recordDate")]
        [JsonConverter(typeof(DateTimeJsonConverter), "yyyy-MM-dd")]
        public DateTime RecordDate { get; set; }
        
        [JsonProperty("paymentDate")]
        [JsonConverter(typeof(DateTimeJsonConverter), "yyyy-MM-dd")]
        public DateTime PaymentDate { get; set; }
        
        [JsonProperty("dividend")]
        public decimal Dividend { get; set; }
    }
}