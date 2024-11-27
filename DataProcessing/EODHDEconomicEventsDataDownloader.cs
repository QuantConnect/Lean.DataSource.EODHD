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
using System.Linq;
using System.Threading.Tasks;

namespace QuantConnect.DataProcessing;

/// <summary>
/// EODHDEconomicEventsDataDownloader implementation.
/// </summary>
public partial class EODHDEconomicEventsDataDownloader : EODHDBaseDataDownloader
{        
    private static readonly Dictionary<string, string> _isoAlpha23Conversion = new()
    {
        {"AF", "AFG"},
        {"AX", "ALA"},
        {"AL", "ALB"},
        {"DZ", "DZA"},
        {"AS", "ASM"},
        {"AD", "AND"},
        {"AO", "AGO"},
        {"AI", "AIA"},
        {"AQ", "ATA"},
        {"AG", "ATG"},
        {"AR", "ARG"},
        {"AM", "ARM"},
        {"AW", "ABW"},
        {"AU", "AUS"},
        {"AT", "AUT"},
        {"AZ", "AZE"},
        {"BS", "BHS"},
        {"BH", "BHR"},
        {"BD", "BGD"},
        {"BB", "BRB"},
        {"BY", "BLR"},
        {"BE", "BEL"},
        {"BZ", "BLZ"},
        {"BJ", "BEN"},
        {"BM", "BMU"},
        {"BT", "BTN"},
        {"BO", "BOL"},
        {"BQ", "BES"},
        {"BA", "BIH"},
        {"BW", "BWA"},
        {"BV", "BVT"},
        {"BR", "BRA"},
        {"IO", "IOT"},
        {"BN", "BRN"},
        {"BG", "BGR"},
        {"BF", "BFA"},
        {"BI", "BDI"},
        {"CV", "CPV"},
        {"KH", "KHM"},
        {"CM", "CMR"},
        {"CA", "CAN"},
        {"KY", "CYM"},
        {"CF", "CAF"},
        {"TD", "TCD"},
        {"CL", "CHL"},
        {"CN", "CHN"},
        {"CX", "CXR"},
        {"CC", "CCK"},
        {"CO", "COL"},
        {"KM", "COM"},
        {"CG", "COG"},
        {"CD", "COD"},
        {"CK", "COK"},
        {"CR", "CRI"},
        {"CI", "CIV"},
        {"HR", "HRV"},
        {"CU", "CUB"},
        {"CW", "CUW"},
        {"CY", "CYP"},
        {"CZ", "CZE"},
        {"DK", "DNK"},
        {"DJ", "DJI"},
        {"DM", "DMA"},
        {"DO", "DOM"},
        {"EC", "ECU"},
        {"EG", "EGY"},
        {"SV", "SLV"},
        {"GQ", "GNQ"},
        {"ER", "ERI"},
        {"EE", "EST"},
        {"SZ", "SWZ"},
        {"ET", "ETH"},
        {"FK", "FLK"},
        {"FO", "FRO"},
        {"FJ", "FJI"},
        {"FI", "FIN"},
        {"FR", "FRA"},
        {"GF", "GUF"},
        {"PF", "PYF"},
        {"TF", "ATF"},
        {"GA", "GAB"},
        {"GM", "GMB"},
        {"GE", "GEO"},
        {"DE", "DEU"},
        {"GH", "GHA"},
        {"GI", "GIB"},
        {"GR", "GRC"},
        {"GL", "GRL"},
        {"GD", "GRD"},
        {"GP", "GLP"},
        {"GU", "GUM"},
        {"GT", "GTM"},
        {"GG", "GGY"},
        {"GN", "GIN"},
        {"GW", "GNB"},
        {"GY", "GUY"},
        {"HT", "HTI"},
        {"HM", "HMD"},
        {"VA", "VAT"},
        {"HN", "HND"},
        {"HK", "HKG"},
        {"HU", "HUN"},
        {"IS", "ISL"},
        {"IN", "IND"},
        {"ID", "IDN"},
        {"IR", "IRN"},
        {"IQ", "IRQ"},
        {"IE", "IRL"},
        {"IM", "IMN"},
        {"IL", "ISR"},
        {"IT", "ITA"},
        {"JM", "JAM"},
        {"JP", "JPN"},
        {"JE", "JEY"},
        {"JO", "JOR"},
        {"KZ", "KAZ"},
        {"KE", "KEN"},
        {"KI", "KIR"},
        {"KP", "PRK"},
        {"KR", "KOR"},
        {"KW", "KWT"},
        {"KG", "KGZ"},
        {"LA", "LAO"},
        {"LV", "LVA"},
        {"LB", "LBN"},
        {"LS", "LSO"},
        {"LR", "LBR"},
        {"LY", "LBY"},
        {"LI", "LIE"},
        {"LT", "LTU"},
        {"LU", "LUX"},
        {"MO", "MAC"},
        {"MG", "MDG"},
        {"MW", "MWI"},
        {"MY", "MYS"},
        {"MV", "MDV"},
        {"ML", "MLI"},
        {"MT", "MLT"},
        {"MH", "MHL"},
        {"MQ", "MTQ"},
        {"MR", "MRT"},
        {"MU", "MUS"},
        {"YT", "MYT"},
        {"MX", "MEX"},
        {"FM", "FSM"},
        {"MD", "MDA"},
        {"MC", "MCO"},
        {"MN", "MNG"},
        {"ME", "MNE"},
        {"MS", "MSR"},
        {"MA", "MAR"},
        {"MZ", "MOZ"},
        {"MM", "MMR"},
        {"NA", "NAM"},
        {"NR", "NRU"},
        {"NP", "NPL"},
        {"NL", "NLD"},
        {"NC", "NCL"},
        {"NZ", "NZL"},
        {"NI", "NIC"},
        {"NE", "NER"},
        {"NG", "NGA"},
        {"NU", "NIU"},
        {"NF", "NFK"},
        {"MK", "MKD"},
        {"MP", "MNP"},
        {"NO", "NOR"},
        {"OM", "OMN"},
        {"PK", "PAK"},
        {"PW", "PLW"},
        {"PS", "PSE"},
        {"PA", "PAN"},
        {"PG", "PNG"},
        {"PY", "PRY"},
        {"PE", "PER"},
        {"PH", "PHL"},
        {"PN", "PCN"},
        {"PL", "POL"},
        {"PT", "PRT"},
        {"PR", "PRI"},
        {"QA", "QAT"},
        {"RE", "REU"},
        {"RO", "ROU"},
        {"RU", "RUS"},
        {"RW", "RWA"},
        {"BL", "BLM"},
        {"SH", "SHN"},
        {"KN", "KNA"},
        {"LC", "LCA"},
        {"MF", "MAF"},
        {"PM", "SPM"},
        {"VC", "VCT"},
        {"WS", "WSM"},
        {"SM", "SMR"},
        {"ST", "STP"},
        {"SA", "SAU"},
        {"SN", "SEN"},
        {"RS", "SRB"},
        {"SC", "SYC"},
        {"SL", "SLE"},
        {"SG", "SGP"},
        {"SX", "SXM"},
        {"SK", "SVK"},
        {"SI", "SVN"},
        {"SB", "SLB"},
        {"SO", "SOM"},
        {"ZA", "ZAF"},
        {"GS", "SGS"},
        {"SS", "SSD"},
        {"ES", "ESP"},
        {"LK", "LKA"},
        {"SD", "SDN"},
        {"SR", "SUR"},
        {"SJ", "SJM"},
        {"SE", "SWE"},
        {"CH", "CHE"},
        {"SY", "SYR"},
        {"TW", "TWN"},
        {"TJ", "TJK"},
        {"TZ", "TZA"},
        {"TH", "THA"},
        {"TL", "TLS"},
        {"TG", "TGO"},
        {"TK", "TKL"},
        {"TO", "TON"},
        {"TT", "TTO"},
        {"TN", "TUN"},
        {"TR", "TUR"},
        {"TM", "TKM"},
        {"TC", "TCA"},
        {"TV", "TUV"},
        {"UG", "UGA"},
        {"UA", "UKR"},
        {"AE", "ARE"},
        {"UK", "GBR"},
        {"GB", "GBR"},
        {"EU", "EUR"},
        {"UM", "UMI"},
        {"US", "USA"},
        {"UY", "URY"},
        {"UZ", "UZB"},
        {"VU", "VUT"},
        {"VE", "VEN"},
        {"VN", "VNM"},
        {"VG", "VGB"},
        {"VI", "VIR"},
        {"WF", "WLF"},
        {"EH", "ESH"},
        {"YE", "YEM"},
        {"ZM", "ZMB"},
        {"ZW", "ZWE"}
    };

    /// <summary>
    /// Dataset endpoint
    /// </summary>
    protected override string Endpoint => "https://eodhd.com/api/economic-events";

    /// <summary>
    /// Dataset name
    /// </summary>
    public override string VendorDataName => "economicevents";

    /// <summary>
    /// Creates a new instance of <see cref="EODHDEconomicEventsDataDownloader"/>
    /// </summary>
    /// <param name="destinationFolder">The folder where the data will be saved</param>
    /// <param name="apiKey">The Vendor API key</param>
    public EODHDEconomicEventsDataDownloader(string destinationFolder, string apiKey, DateTime deplopymentDate) 
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

        // This dataset starts in 2019
        if (processDate.Year < 2019) processDate = new(2019, 1, 1);

        while (processDate <= DeploymentDate)
        {
            var stopwatch = Stopwatch.StartNew();
            var eventsByCountry = new Dictionary<string, List<string>>();

            try
            {
                // We would like to get earnings for the upcoming 7 days
                var endDate = processDate.AddDays(7);
                Log.Trace($"EODHDEconomicEventsDataDownloader.Run(): Start processing events that will happen from {processDate:yyyyMMdd} to {endDate:yyyyMMdd}");

                var result = await HttpRequester($"?from={processDate:yyyy-MM-dd}&to={endDate:yyyy-MM-dd}&limit=1000&fmt=json");
                if (string.IsNullOrWhiteSpace(result))
                {
                    Log.Error($"EODHDEconomicEventsDataDownloader.Run(): No data received");
                    success = false;
                    continue;
                }

                var data = JsonConvert.DeserializeObject<List<EODHDEconomicEventsData>>(result, JsonSerializerSettings);

                foreach (var datum in data.Where(x => x.EventDateTime >= processDate))
                {
                    var eventType = datum.EventType?.Replace(",", string.Empty);
                    var eventPeriod = datum.Period?.Replace(",", string.Empty);
                    if (!_isoAlpha23Conversion.TryGetValue(datum.Country, out var country))
                    {
                        Log.Error($"EODHDEconomicEventsDataDownloader.Run(): Unknown country code - {datum.Country}, skipping...");
                        continue;
                    }

                    if (!eventsByCountry.TryGetValue(country, out var countryEvents))
                    {
                        countryEvents = eventsByCountry[country] = new();
                    }
                    countryEvents.Add($"{datum.EventDateTime:yyyyMMdd HH:mm:ss},{eventPeriod},{EventTypeFilter.FilterEvent(eventType)},{datum.Previous},{datum.Estimate}");
                }

                foreach (var (country, events) in eventsByCountry)
                {
                    SaveContentToFile(country.ToLowerInvariant(), processDate, events);
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
                success = false;
                continue;
            }

            Log.Trace($"EODHDEconomicEventsDataDownloader.Run(): Finished in {stopwatch.Elapsed.TotalSeconds:f3} seconds");
            processDate = processDate.AddDays(1);
        }
        return success;
    }

    /// <summary>
    /// Represents items of the list of upcoming events supported by EODHD
    /// </summary>
    /// <remarks>actual, change, change_percentage are not deserialized since only forward data are processed</remarks>
    private class EODHDEconomicEventsData
    {
        [JsonProperty("type")]
        public string EventType { get; set; }
        
        [JsonProperty("period")]
        public string Period { get; set; }
        
        [JsonProperty("country")]
        public string Country { get; set; }
        
        [JsonProperty("date")]
        [JsonConverter(typeof(DateTimeJsonConverter), "yyyy-MM-dd HH:mm:ss")]
        public DateTime EventDateTime { get; set; }
        
        [JsonProperty("previous")]
        public decimal? Previous { get; set; }
        
        [JsonProperty("estimate")]
        public decimal? Estimate { get; set; }
    }
}