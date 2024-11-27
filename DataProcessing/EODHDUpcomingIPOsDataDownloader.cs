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
using QuantConnect.DataSource;
using QuantConnect.Logging;
using QuantConnect.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace QuantConnect.DataProcessing;

/// <summary>
/// EODHDUpcomingIPOsDataDownloader implementation.
/// </summary>
public class EODHDUpcomingIPOsDataDownloader : EODHDBaseDataDownloader
{
    private static readonly List<string> _currenciesSupported = new() { "USD", "US", string.Empty, null };

    /// <summary>
    /// Dataset endpoint
    /// </summary>
    protected override string Endpoint => "https://eodhd.com/api/calendar/ipos";

    /// <summary>
    /// Dataset name
    /// </summary>
    public override string VendorDataName => "upcomingipos";

    /// <summary>
    /// Creates a new instance of <see cref="EODHDUpcomingIPOsDataDownloader"/>
    /// </summary>
    /// <param name="destinationFolder">The folder where the data will be saved</param>
    /// <param name="apiKey">The Vendor API key</param>
    public EODHDUpcomingIPOsDataDownloader(string destinationFolder, string apiKey, DateTime deplopymentDate)
        : base(destinationFolder, apiKey, deplopymentDate)
    {
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
                Log.Trace($"EODHDUpcomingIPOsDataDownloader.Run(): Start processing ipos that will start from {processDate:yyyyMMdd} to {endDate:yyyyMMdd}");

                var result = HttpRequester($"?from={processDate:yyyy-MM-dd}&to={endDate:yyyy-MM-dd}&fmt=json").SynchronouslyAwaitTaskResult();
                if (string.IsNullOrWhiteSpace(result))
                {
                    Log.Error($"EODHDUpcomingIPOsDataDownloader.Run(): No data received");
                    success = false;
                    continue;
                }

                EODHDUpcomingIPOsMetadata metadata = JsonConvert.DeserializeObject<EODHDUpcomingIPOsMetadata>(result, JsonSerializerSettings);
                var csvContents = new List<string>();

                // Include only the primary stocks traded in US exchanges
                foreach (var ipo in metadata.IPOs.Where(x => x.Ticker.EndsWith(".US") && _currenciesSupported.Contains(x.Currency)))
                {
                    var ticker = ipo.Ticker.Remove(ipo.Ticker.Length - 3);      // Remove the last 3 char ".US"
                    if (!TryNormalizeDefunctTicker(ticker, out var nonDefunctTicker))
                    {
                        // If not valid ticker, skip
                        continue;
                    }
                    var ipoDate = ipo.IpoDate;
                    // If the ipo date is behind, skip
                    if (ipoDate < processDate || ipoDate.Year < 1998)
                    {
                        continue;
                    }

                    var sid = SecurityIdentifier.GenerateEquity(ipoDate, nonDefunctTicker, Market.USA);
                    var name = ipo.Name.Replace(",", string.Empty);
                    var filingDate = DateTimeToString(ipo.FilingDate);
                    var amendedDate = DateTimeToString(ipo.AmendedDate);
                    var priceFrom = NumberToString(ipo.PriceFrom);
                    var priceTo = NumberToString(ipo.PriceTo);
                    var offerPrice = NumberToString(ipo.OfferPrice);
                    var shares = NumberToString(ipo.Shares);

                    csvContents.Add($"{sid},{nonDefunctTicker},{name},{ipo.Exchange},{DateTimeToString(ipoDate)},{filingDate},{amendedDate},{priceFrom},{priceTo},{offerPrice},{shares},{ipo.DealType}");
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

            Log.Trace($"EODHDUpcomingIPOsDataDownloader.Run(): Finished in {stopwatch.Elapsed.TotalSeconds:f3} seconds");
            processDate = processDate.AddDays(1);
        }
        return success;
    }

    /// <summary>
    /// Get the datetime to string that write in the CSV file
    /// </summary>
    /// <param name="datetime">The datetime to convert to string</param>
    /// <returns>The string that writes in the CSV</returns>
    private static string DateTimeToString(DateTime datetime)
    {
        return datetime == DateTime.MinValue ? string.Empty : $"{datetime:yyyyMMdd}";
    }

    /// <summary>
    /// Get the number to string that write in the CSV file
    /// </summary>
    /// <param name="number">The number to convert to string</param>
    /// <returns>The string that writes in the CSV</returns>
    private static string NumberToString(decimal number)
    {
        return number == 0m ? string.Empty : $"{number}";
    }

    /// <summary>
    /// Represents JSON output return of the raw request
    /// </summary>
    private class EODHDUpcomingIPOsMetadata
    {
        [JsonProperty("ipos")]
        public List<EODHDUpcomingIPOsData> IPOs { get; set; }
    }

    /// <summary>
    /// Represents items of the list of upcoming ipos supported by EODHD
    /// </summary>
    private class EODHDUpcomingIPOsData
    {
        [JsonProperty("code")]
        public string Ticker { get; set; }
        
        [JsonProperty("name")]
        public string Name { get; set; }
        
        [JsonProperty("exchange")]
        public string Exchange { get; set; }
        
        [JsonProperty("currency")]
        public string Currency { get; set; }
        
        [JsonProperty("start_date")]
        [JsonConverter(typeof(DateTimeJsonConverter), "yyyy-MM-dd")]
        public DateTime IpoDate { get; set; }
        
        [JsonProperty("filing_date")]
        [JsonConverter(typeof(DateTimeJsonConverter), "yyyy-MM-dd")]
        public DateTime FilingDate { get; set; }
        
        [JsonProperty("amended_date")]
        [JsonConverter(typeof(DateTimeJsonConverter), "yyyy-MM-dd")]
        public DateTime AmendedDate { get; set; }
        
        [JsonProperty("price_from")]
        public decimal PriceFrom { get; set; }

        [JsonProperty("price_to")]
        public decimal PriceTo { get; set; }
        
        [JsonProperty("offer_price")]
        public decimal OfferPrice { get; set; }

        [JsonProperty("shares")]
        public decimal Shares { get; set; }

        [JsonProperty("deal_type")]
        public EODHD.DealType DealType { get; set; }
    }
}
