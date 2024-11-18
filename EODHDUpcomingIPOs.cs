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
 *
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using QuantConnect.Data;

namespace QuantConnect.DataSource;

/// <summary>
/// EODHDUpcomingIPOs data type
/// </summary>
public class EODHDUpcomingIPOs : BaseData
{
    // List of exchanges for supported US equities, may be expanded
    private static readonly Dictionary<string, Exchange> _exchangeMap = new()
    {
        {"NYSE", Exchange.NYSE},
        {"Nasdaq", Exchange.NASDAQ},
        {"NASDQ", Exchange.NASDAQ},
        {"NasdaqCM", Exchange.NASDAQ},
        {"NASDAQ Capital", Exchange.NASDAQ},
        {"NasdaqGM", Exchange.ISE},
        {"NasdaqGS", Exchange.ISE},
        {"NASDAQ Global", Exchange.ISE},
        {"NASDAQ Global Select", Exchange.ISE},
        {"NYSE American", Exchange.AMEX},
        {"NYSE ARCA", Exchange.ARCA},
        {"NYSEArca", Exchange.ARCA},
        {"BATS", Exchange.BATS},
        {"CBOE", Exchange.CBOE},
        {"OTC", Exchange.OTCX},
        {"OTC BB", Exchange.OTCX},
        {"Other OTC", Exchange.OTCX},
        {"OTC Markets OTCPK", Exchange.OTCX},
    };

    /// <summary>
    /// The name of the company of the IPO
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// The exchange that the IPO takes place
    /// </summary>
    public Exchange Exchange { get; set; }

    /// <summary>
    /// The date of the IPO takes place
    /// </summary>
    public DateTime? IpoDate { get; set; }

    /// <summary>
    /// The date of the IPO registration submission
    /// </summary>
    public DateTime? FilingDate { get; set; }

    /// <summary>
    /// The date of the last change of the IPO arrangement if any
    /// </summary>
    public DateTime? AmendedDate { get; set; }

    /// <summary>
    /// The set lower bound of the IPO price
    /// </summary>
    public decimal? LowestPrice { get; set; }

    /// <summary>
    /// The set upper bound of the IPO price
    /// </summary>
    public decimal? HighestPrice { get; set; }

    /// <summary>
    /// The exact price set for the IPO
    /// </summary>
    public decimal? OfferPrice { get; set; }

    /// <summary>
    /// The number of shares of the IPO
    /// </summary>
    public decimal? Shares { get; set; }

    /// <summary>
    /// The deal type of the IPO
    /// </summary>
    public EODHD.DealType DealType { get; set; }

    /// <summary>
    /// Time the data became available
    /// </summary>
    public override DateTime EndTime => Time.AddDays(1);

    /// <summary>
    /// Return the URL string source of the file. This will be converted to a stream
    /// </summary>
    /// <param name="config">Configuration object</param>
    /// <param name="date">Date of this source file</param>
    /// <param name="isLiveMode">true if we're in live mode, false for backtesting mode</param>
    /// <returns>String URL of source file.</returns>
    public override SubscriptionDataSource GetSource(SubscriptionDataConfig config, DateTime date, bool isLiveMode)
    {
        return new SubscriptionDataSource(
            Path.Combine(
                Globals.DataFolder,
                "alternative",
                "eodhd",
                "upcomingipos",
                $"{date:yyyyMMdd}.csv"
            ),
            SubscriptionTransportMedium.LocalFile
        );
    }

    /// <summary>
    /// Parses the data from the line provided and loads it into LEAN
    /// </summary>
    /// <param name="config">Subscription configuration</param>
    /// <param name="line">Line of data</param>
    /// <param name="date">Date</param>
    /// <param name="isLiveMode">Is live mode</param>
    /// <returns>New instance</returns>
    public override BaseData Reader(SubscriptionDataConfig config, string line, DateTime date, bool isLiveMode)
    {
        var csv = line.Split(',');

        if (!_exchangeMap.TryGetValue(csv[3], out var exchange))
        {
            exchange = Exchange.UNKNOWN;
        }

        return new EODHDUpcomingIPOs
        {
            Symbol = new Symbol(SecurityIdentifier.Parse(csv[0]), csv[1]),
            Name = csv[2],
            Exchange = exchange,
            IpoDate = csv[4].IfNotNullOrEmpty<DateTime?>(s => Parse.DateTimeExact(s, "yyyyMMdd")),
            FilingDate = csv[5].IfNotNullOrEmpty<DateTime?>(s => Parse.DateTimeExact(s, "yyyyMMdd")),
            AmendedDate = csv[6].IfNotNullOrEmpty<DateTime?>(s => Parse.DateTimeExact(s, "yyyyMMdd")),
            LowestPrice = csv[7].IfNotNullOrEmpty<decimal?>(s => decimal.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture)),
            HighestPrice = csv[8].IfNotNullOrEmpty<decimal?>(s => decimal.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture)),
            OfferPrice = csv[9].IfNotNullOrEmpty<decimal?>(s => decimal.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture)),
            Shares = csv[10].IfNotNullOrEmpty<decimal?>(s => decimal.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture)),
            DealType = (EODHD.DealType)Enum.Parse(typeof(EODHD.DealType), csv[11]),
            Time = date,
        };
    }

    /// <summary>
    /// Clones the data
    /// </summary>
    /// <returns>A clone of the object</returns>
    public override BaseData Clone()
    {
        return new EODHDUpcomingIPOs
        {
            Symbol = Symbol,
            Time = Time,
            Name = Name,
            Exchange = Exchange,
            IpoDate = IpoDate,
            FilingDate = FilingDate,
            AmendedDate = AmendedDate,
            LowestPrice = LowestPrice,
            HighestPrice = HighestPrice,
            OfferPrice = OfferPrice,
            Shares = Shares,
            DealType = DealType
        };
    }

    /// <summary>
    /// Indicates whether the data source is tied to an underlying symbol and requires that corporate events be applied to it as well, such as renames and delistings
    /// </summary>
    /// <returns>false</returns>
    public override bool RequiresMapping()
    {
        return false;
    }

    /// <summary>
    /// Indicates whether the data is sparse.
    /// If true, we disable logging for missing files
    /// </summary>
    /// <returns>true</returns>
    public override bool IsSparseData()
    {
        return true;
    }

    /// <summary>
    /// Converts the instance to string
    /// </summary>
    public override string ToString()
    {
        return $"{EndTime} - {Symbol} - {DealType} - {Exchange} - {IpoDate} - {FilingDate} - {AmendedDate} - {LowestPrice} - {HighestPrice} - {OfferPrice} - {Shares}";
    }

    /// <summary>
    /// Gets the default resolution for this data and security type
    /// </summary>
    public override Resolution DefaultResolution()
    {
        return Resolution.Daily;
    }

    /// <summary>
    /// Gets the supported resolution for this data and security type
    /// </summary>
    public override List<Resolution> SupportedResolutions()
    {
        return DailyResolution;
    }
}
