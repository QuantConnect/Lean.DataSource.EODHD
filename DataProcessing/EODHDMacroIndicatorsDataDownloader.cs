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
using QuantConnect.Logging;
using QuantConnect.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace QuantConnect.DataProcessing;

/// <summary>
/// EODHDMacroIndicatorsDataDownloader implementation.
/// </summary>
public class EODHDMacroIndicatorsDataDownloader : EODHDBaseDataDownloader
{
    // 1960-1961 data are not reliable (most are all 0s)
    private readonly DateTime _epochTime = new(1962, 1, 1);

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

    private readonly string _destinationFolder;

    /// <summary>
    /// Dataset endpoint
    /// </summary>
    protected override string Endpoint => "https://eodhd.com/api/macro-indicator/";

    /// <summary>
    /// Dataset name
    /// </summary>
    public override string VendorDataName => "macroindicators";

    /// <summary>
    /// Creates a new instance of <see cref="EODHDMacroIndicatorsDataDownloader"/>
    /// </summary>
    /// <param name="destinationFolder">The folder where the data will be saved</param>
    /// <param name="apiKey">The Vendor API key</param>
    public EODHDMacroIndicatorsDataDownloader(string destinationFolder, string apiKey)
        : base(destinationFolder, apiKey, DateTime.MinValue)
    {
        _destinationFolder = Path.Combine(destinationFolder, VendorName, VendorDataName);
        JsonSerializerSettings.Converters.Add(new ZeroDateTimeJsonConverter("yyyy-MM-dd"));
    }

    /// <summary>
    /// Runs the instance of the object.
    /// </summary>
    /// <returns>True if process all downloads successfully</returns>
    public override async Task<bool> Run(DateTime _)
    {
        var success = true;

        foreach (var country in _countries)
        {
            var stopwatch = Stopwatch.StartNew();
            var csvContents = new List<string>();

            try
            {
                foreach (var indicator in _indicators)
                {
                    Log.Trace($"EODHDMacroIndicatorsDataDownloader.Run(): Start processing macro indicators of {country} - {indicator}");

                    var result = await HttpRequester($"{country}?indicator={indicator}&fmt=json");
                    if (string.IsNullOrEmpty(result))
                    {
                        continue;
                    }

                    var data = JsonConvert.DeserializeObject<List<EODHDMacroIndicatorsData>>(result, JsonSerializerSettings);

                    foreach (var datum in data.Where(x => x.Date >= _epochTime && x.Value != 0m))
                    {
                        // Offset a month since the annual data is not deliever immediately at year-end
                        csvContents.Add($"{datum.Date.AddMonths(1):yyyyMMdd},{indicator},{datum.Period},{datum.Value}");
                    }
                }
                if (csvContents.Count > 0)
                {
                    SaveContentToFile(string.Empty, country, csvContents);
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
                success = false;
                continue;
            }
            Log.Trace($"EODHDMacroIndicatorsDataDownloader.Run(): Finished in {stopwatch.Elapsed.TotalSeconds:f3} seconds");
        }
        return success;
    }

    /// <summary>
    /// Saves contents to disk, deleting existing zip files
    /// </summary>
    /// <param name="destinationFolder">Final destination of the data</param>
    /// <param name="name">file name</param>
    /// <param name="contents">Contents to write</param>
    protected override void SaveContentToFile(string destinationFolder, string name, IEnumerable<string> contents)
    {
        var finalPath = Path.Combine(_destinationFolder, $"{name.ToLowerInvariant()}.csv");
        var lines = File.Exists(finalPath) ? File.ReadAllLines(finalPath).ToList() : new List<string>();
        lines.AddRange(contents);
        base.SaveContentToFile(destinationFolder, name, lines);
    }

    /// <summary>
    /// Represents items of the list of macro indicators supported by EODHD
    /// </summary>
    /// <remarks>actual, change, change_percentage are not deserialized since only forward data are processed</remarks>
    private class EODHDMacroIndicatorsData
    {
        [JsonProperty(nameof(Date))]
        [JsonConverter(typeof(ZeroDateTimeJsonConverter), "yyyy-MM-dd")]
        public DateTime Date { get; set; }
        
        [JsonProperty(nameof(Period))]
        public string Period { get; set; }

        [JsonProperty(nameof(Value))]
        public decimal Value { get; set; }
    }
}