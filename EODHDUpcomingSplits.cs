﻿/*
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
/// EODHDUpcomingSplits data type.
/// </summary>
public class EODHDUpcomingSplits : BaseData
{
    /// <summary>
    /// Date of the split will happen
    /// </summary>
    public DateTime SplitDate { get; set; }

    /// <summary>
    /// If this is split optionable for shareholders.
    /// </summary>
    public bool Optionable { get; set; }

    /// <summary>
    /// Ratio of old shares / new shares.
    /// </summary>
    public decimal SplitFactor => Value;

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
                "upcomingsplits",
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

        return new EODHDUpcomingSplits
        {
            Symbol = new Symbol(SecurityIdentifier.Parse(csv[0]), csv[1]),
            SplitDate = Parse.DateTimeExact(csv[2], "yyyyMMdd"),
            Optionable = csv[3] != "N",
            Value = decimal.Parse(csv[4], NumberStyles.Any, CultureInfo.InvariantCulture),
            // `date` represents the end of the period while Time the start
            Time = date.AddDays(-1),
        };
    }

    /// <summary>
    /// Clones the data
    /// </summary>
    /// <returns>A clone of the object</returns>
    public override BaseData Clone()
    {
        return new EODHDUpcomingSplits
        {
            Symbol = Symbol,
            Time = Time,
            SplitDate = SplitDate,
            Optionable = Optionable,
            Value = Value,
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
        return $"{Symbol} - {SplitDate} - {Optionable} - {SplitFactor}";
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
