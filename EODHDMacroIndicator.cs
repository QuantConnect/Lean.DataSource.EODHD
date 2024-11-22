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

using QuantConnect.Data;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace QuantConnect.DataSource;

/// <summary>
/// EODHDMacroIndicator data type
/// </summary>
public class EODHDMacroIndicator : BaseData
{
    /// <summary>
    /// The macro indicator
    /// </summary>
    public string Indicator { get; set; }

    /// <summary>
    /// The country of the indicator. See https://en.wikipedia.org/wiki/ISO_3166-1_alpha-3.
    /// </summary>
    public string Country { get; set; }

    /// <summary>
    /// The representation period of the indicator
    /// </summary>
    public EODHD.Frequency Frequency { get; set; }

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

        var ticker = config.Symbol.Value.Split('/');
        var indicatorType = csv[1];
        if (ticker.Length > 1 && indicatorType != ticker[1].ToLowerInvariant())
        {
            return null;
        }

        if (!Enum.TryParse<EODHD.Frequency>(csv[2], true, out var frequency))
        {
            frequency = EODHD.Frequency.Unknown;
        }

        return new EODHDMacroIndicator
        {
            Symbol = config.Symbol,
            Time = Parse.DateTimeExact(csv[0], "yyyyMMdd").AddDays(1),
            Country = ticker[0],
            Indicator = indicatorType,
            Frequency = frequency,
            Value = decimal.Parse(csv[3], NumberStyles.Any, CultureInfo.InvariantCulture)
        };
    }

    /// <summary>
    /// Clones the data
    /// </summary>
    /// <returns>A clone of the object</returns>
    public override BaseData Clone()
    {
        return new EODHDMacroIndicator
        {
            Symbol = Symbol,
            Time = Time,
            Country = Country,
            Indicator = Indicator,
            Frequency = Frequency,
            Value = Value
        };
    }

    /// <summary>
    /// Indicates whether the data source is tied to an underlying symbol and requires that corporate indicators be applied to it as well, such as renames and delistings
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
        return $"{EndTime} - {Indicator} - {Frequency} - {Value}";
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