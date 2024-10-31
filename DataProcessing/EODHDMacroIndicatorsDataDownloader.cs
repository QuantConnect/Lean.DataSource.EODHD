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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using QuantConnect.Configuration;
using QuantConnect.Data.Auxiliary;
using QuantConnect.Interfaces;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Logging;
using QuantConnect.Util;

namespace QuantConnect.DataProcessing
{
    /// <summary>
    /// EODHDMacroIndicatorsDataDownloader implementation.
    /// </summary>
    public class EODHDMacroIndicatorsDataDownloader : IDisposable
    {
        public const string VendorName = "eodhd";
        public const string VendorDataName = "macroindicators";
        
        // 1-year warm up (some annual frequency indicators)
        private readonly DateTime _epochTime = new DateTime(1997, 1, 1);
        
        private readonly string _destinationFolder;
        private readonly string _apiToken;
        private readonly IMapFileProvider _mapFileProvider;
        private readonly int _maxRetries = 5;
        
        private readonly JsonSerializerSettings _jsonSerializerSettings = new()
        {
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            NullValueHandling = NullValueHandling.Ignore,
            Converters = new List<JsonConverter> { new ZeroDateTimeJsonConverter("yyyy-MM-dd") }
        };

        private static readonly List<string> _countries = new() {
            "ABW", "AFG", "AGO", "AIA", "ALA", "ALB", "AND", "ARE", "ARG", "ARM", "ASM", "ATA", "ATF", "ATG", "AUS", "AUT", "AZE", "BDI", "BEL", "BEN", "BES",
            "BFA", "BGD", "BGR", "BHR", "BHS", "BIH", "BLM", "BLR", "BLZ", "BMU", "BOL", "BRA", "BRB", "BRN", "BTN", "BVT", "BWA", "CAF", "CAN", "CCK", "CHE",
            "CHL", "CHN", "CIV", "CMR", "COD", "COG", "COK", "COL", "COM", "CPV", "CRI", "CUB", "CUW", "CXR", "CYM", "CYP", "CZE", "DEU", "DJI", "DMA", "DNK",
            "DOM", "DZA", "ECU", "EGY", "ERI", "ESH", "ESP", "EST", "ETH", "FIN", "FJI", "FLK", "FRA", "FRO", "FSM", "GAB", "GBR", "GEO", "GGY", "GHA", "GIB",
            "GIN", "GLP", "GMB", "GNB", "GNQ", "GRC", "GRD", "GRL", "GTM", "GUF", "GUM", "GUY", "HKG", "HMD", "HND", "HRV", "HTI", "HUN", "IDN", "IMN", "IND",
            "IOT", "IRL", "IRN", "IRQ", "ISL", "ISR", "ITA", "JAM", "JEY", "JOR", "JPN", "KAZ", "KEN", "KGZ", "KHM", "KIR", "KNA", "KOR", "KWT", "LAO", "LBN",
            "LBR", "LBY", "LCA", "LIE", "LKA", "LSO", "LTU", "LUX", "LVA", "MAC", "MAF", "MAR", "MCO", "MDA", "MDG", "MDV", "MEX", "MHL", "MKD", "MLI", "MLT",
            "MMR", "MNE", "MNG", "MNP", "MOZ", "MRT", "MSR", "MTQ", "MUS", "MWI", "MYS", "MYT", "NAM", "NCL", "NER", "NFK", "NGA", "NIC", "NIU", "NLD", "NOR",
            "NPL", "NRU", "NZL", "OMN", "PAK", "PAN", "PCN", "PER", "PHL", "PLW", "PNG", "POL", "PRI", "PRK", "PRT", "PRY", "PSE", "PYF", "QAT", "REU", "ROU",
            "RUS", "RWA", "SAU", "SDN", "SEN", "SGP", "SGS", "SHN", "SJM", "SLB", "SLE", "SLV", "SMR", "SOM", "SPM", "SRB", "SSD", "STP", "SUR", "SVK", "SVN",
            "SWE", "SWZ", "SXM", "SYC", "SYR", "TCA", "TCD", "TGO", "THA", "TJK", "TKL", "TKM", "TLS", "TON", "TTO", "TUN", "TUR", "TUV", "TWN", "TZA", "UGA",
            "UKR", "UMI", "URY", "USA", "UZB", "VAT", "VCT", "VEN", "VGB", "VIR", "VNM", "VUT", "WLF", "WSM", "YEM", "ZAF", "ZMB", "ZWE"
        };

        private static readonly List<string> _indicators = new() {
            "real_interest_rate",
            "population_total",
            "population_growth_annual",
            "inflation_consumer_prices_annual",
            "consumer_price_index",
            "gdp_current_usd",
            "gdp_per_capita_usd",
            "gdp_growth_annual",
            "debt_percent_gdp",
            "net_trades_goods_services",
            "inflation_gdp_deflator_annual",
            "agriculture_value_added_percent_gdp",
            "industry_value_added_percent_gdp",
            "services_value_added_percent_gdp",
            "exports_of_goods_services_percent_gdp",
            "imports_of_goods_services_percent_gdp",
            "gross_capital_formation_percent_gdp",
            "net_migration",
            "gni_usd",
            "gni_per_capita_usd",
            "gni_ppp_usd",
            "gni_per_capita_ppp_usd",
            "income_share_lowest_twenty",
            "life_expectancy",
            "fertility_rate",
            "prevalence_hiv_total",
            "co2_emissions_tons_per_capita",
            "surface_area_km",
            "poverty_poverty_lines_percent_population",
            "revenue_excluding_grants_percent_gdp",
            "cash_surplus_deficit_percent_gdp",
            "startup_procedures_register",
            "market_cap_domestic_companies_percent_gdp",
            "mobile_subscriptions_per_hundred",
            "internet_users_per_hundred",
            "high_technology_exports_percent_total",
            "merchandise_trade_percent_gdp",
            "total_debt_service_percent_gni",
            "unemployment_total_percent"
        };

        /// <summary>
        /// Control the rate of download per unit of time.
        /// </summary>
        private readonly RateGate _indexGate;

        /// <summary>
        /// Creates a new instance of <see cref="EODHDMacroIndicatorsDataDownloader"/>
        /// </summary>
        /// <param name="destinationFolder">The folder where the data will be saved</param>
        /// <param name="apiKey">The Vendor API key</param>
        public EODHDMacroIndicatorsDataDownloader(string destinationFolder, string apiKey = null)
        {
            _destinationFolder = Path.Combine(destinationFolder, VendorName, VendorDataName);
            _apiToken = apiKey ?? Config.Get("vendor-auth-token");

            _mapFileProvider = new LocalZipMapFileProvider();
            _mapFileProvider.Initialize(new DefaultDataProvider());

            // Represents rate limits of 10 requests per 1.1 second
            _indexGate = new RateGate(10, TimeSpan.FromSeconds(1.1));

            Directory.CreateDirectory(_destinationFolder);
        }

        /// <summary>
        /// Runs the instance of the object.
        /// </summary>
        /// <returns>True if process all downloads successfully</returns>
        public bool Run()
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var tasks = new List<Task>();

                foreach (var country in _countries)
                {
                    var csvContents = new List<string>();

                    foreach (var indicator in _indicators)
                    {
                        Log.Trace(
                            $"EODHDMacroIndicatorsDataDownloader.Run(): Start processing macro indicators of {country} - {indicator}");

                        tasks.Add(
                            HttpRequester($"{country}?indicator={indicator}&api_token={_apiToken}&fmt=json")
                                .ContinueWith(
                                    y =>
                                    {
                                        if (y.IsFaulted)
                                        {
                                            Log.Error(
                                                $"EODHDMacroIndicatorsDataDownloader.Run(): Failed to get data for {country} - {indicator}");
                                            return;
                                        }

                                        var result = y.Result;
                                        if (string.IsNullOrEmpty(result))
                                        {
                                            // We've already logged inside HttpRequester
                                            return;
                                        }

                                        List<EODHDMacroIndicatorsData> data = JsonConvert.DeserializeObject<List<EODHDMacroIndicatorsData>>(result, _jsonSerializerSettings);

                                        foreach (var datum in data.Where(x => x.Date >= _epochTime && x.Value != 0m))
                                        {
                                            csvContents.Add($"{datum.Date:yyyyMMdd},{country},{indicator},{datum.Period},{datum.Value}");
                                        }
                                    }
                                )
                        );

                        if (tasks.Count == 10)
                        {
                            Task.WaitAll(tasks.ToArray());
                            tasks.Clear();
                        }
                    }

                    if (tasks.Count != 0)
                    {
                        Task.WaitAll(tasks.ToArray());
                        tasks.Clear();
                    }

                    if (csvContents.Count > 0)
                    {
                        SaveContentToFile(_destinationFolder, country, csvContents);
                    }
                }
                
            }
            catch (Exception e)
            {
                Log.Error(e);
                return false;
            }

            Log.Trace($"EODHDMacroIndicatorsDataDownloader.Run(): Finished in {stopwatch.Elapsed.ToStringInvariant(null)}");
            return true;
        }

        /// <summary>
        /// Sends a GET request for the provided URL
        /// </summary>
        /// <param name="url">URL to send GET request for</param>
        /// <returns>Content as string</returns>
        /// <exception cref="Exception">Failed to get data after exceeding retries</exception>
        private async Task<string> HttpRequester(string url)
        {
            for (var retries = 1; retries <= _maxRetries; retries++)
            {
                try
                {
                    using (var client = new HttpClient())
                    {
                        client.BaseAddress = new Uri("https://eodhd.com/api/macro-indicator/");
                        client.DefaultRequestHeaders.Clear();
                        
                        // Makes sure we don't overrun eodhd rate limits accidentally
                        _indexGate.WaitToProceed();

                        var response = await client.GetAsync(Uri.EscapeUriString(url));
                        if (response.StatusCode == HttpStatusCode.NotFound)
                        {
                            Log.Error($"EODHDMacroIndicatorsDataDownloader.HttpRequester(): Files not found at url: {Uri.EscapeUriString(url)}");
                            response.DisposeSafely();
                            return string.Empty;
                        }

                        if (response.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            var finalRequestUri = response.RequestMessage.RequestUri; // contains the final location after following the redirect.
                            response = client.GetAsync(finalRequestUri).Result; // Reissue the request. The DefaultRequestHeaders configured on the client will be used, so we don't have to set them again.
                        }

                        response.EnsureSuccessStatusCode();

                        var result =  await response.Content.ReadAsStringAsync();
                        response.DisposeSafely();

                        return result;
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e, $"EODHDMacroIndicatorsDataDownloader.HttpRequester(): Error at HttpRequester. (retry {retries}/{_maxRetries})");
                    Thread.Sleep(1000);
                }
            }

            throw new Exception($"Request failed with no more retries remaining (retry {_maxRetries}/{_maxRetries})");
        }

        /// <summary>
        /// Saves contents to disk, deleting existing zip files
        /// </summary>
        /// <param name="destinationFolder">Final destination of the data</param>
        /// <param name="name">file name</param>
        /// <param name="contents">Contents to write</param>
        private void SaveContentToFile(string destinationFolder, string name, IEnumerable<string> contents)
        {
            Directory.CreateDirectory(destinationFolder);
            name = name.ToLowerInvariant();
            var finalPath = Path.Combine(destinationFolder, $"{name}.csv");
            var finalFileExists = File.Exists(finalPath);

            var lines = new HashSet<string>(contents);
            if (finalFileExists)
            {
                foreach (var line in File.ReadAllLines(finalPath))
                {
                    lines.Add(line);
                }
            }

            var finalLines = lines
                .OrderBy(x => DateTime.ParseExact(x.Split(',').First(), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal))
                .ToList();

            File.WriteAllLines(finalPath, finalLines);
        }

        /// <summary>
        /// Represents items of the list of macro indicators supported by EODHD
        /// </summary>
        /// <remarks>actual, change, change_percentage are not deserialized since only forward data are processed</remarks>
        private class EODHDMacroIndicatorsData
        {
            [JsonProperty("Date")]
            [JsonConverter(typeof(ZeroDateTimeJsonConverter), "yyyy-MM-dd")]
            public DateTime Date { get; set; }
            
            [JsonProperty("Period")]
            public string Period { get; set; }

            [JsonProperty("Value")]
            public decimal Value { get; set; }
        }

        /// <summary>
        /// Disposes of unmanaged resources
        /// </summary>
        public void Dispose()
        {
            _indexGate?.Dispose();
        }
    }
}