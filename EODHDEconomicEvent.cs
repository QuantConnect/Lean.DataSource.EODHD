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

using NodaTime;
using QuantConnect.Data;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace QuantConnect.DataSource;

/// <summary>
/// EODHDEconomicEvent data type
/// </summary>
public class EODHDEconomicEvent : BaseData
{
    /// <summary>
    /// The economic event
    /// </summary>
    public string EventType { get; set; }

    /// <summary>
    /// The representation period of the event announcement
    /// </summary>
    public string EventPeriod { get; set; }

    /// <summary>
    /// The country of the event. See https://en.wikipedia.org/wiki/ISO_3166-1_alpha-3.
    /// </summary>
    public string Country { get; set; }

    /// <summary>
    /// The event announcement or start time in UTC
    /// </summary>
    public DateTime EventTime { get; set; }

    /// <summary>
    /// The previous figure of the announcement if any
    /// </summary>
    public decimal? Previous { get; set; }

    /// <summary>
    /// The estimated figure of the announcement if any
    /// </summary>
    public decimal? Estimate { get; set; }

    /// <summary>
    /// Time the data became available
    /// </summary>
    public override DateTime EndTime => Time.AddDays(1);

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
        var eventType = csv[2];
        if (ticker.Length > 1 && eventType != ticker[1].ToLowerInvariant())
        {
            return null;
        }

        return new EODHDEconomicEvent
        {
            Symbol = config.Symbol,
            Country = ticker[0],
            EventTime = Parse.DateTimeExact(csv[0], "yyyyMMdd HH:mm:ss"),
            EventPeriod = csv[1],
            EventType = eventType,
            Previous = csv[3].IfNotNullOrEmpty<decimal?>(s => decimal.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture)),
            Estimate = csv[4].IfNotNullOrEmpty<decimal?>(s => decimal.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture)),
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
        return new EODHDEconomicEvent
        {
            Symbol = Symbol,
            Time = Time,
            EventType = EventType,
            EventPeriod = EventPeriod,
            Country = Country,
            EventTime = EventTime,
            Previous = Previous,
            Estimate = Previous
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
        return $"{EventTime} - {EventPeriod} - {EventType} - {Previous} - {Estimate}";
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

    /// <summary>
    /// Specifies the data time zone for this data type. This is useful for custom data types
    /// </summary>
    /// <returns>The <see cref="T:NodaTime.DateTimeZone" /> of this data type</returns>
    public override DateTimeZone DataTimeZone()
    {
        return DateTimeZone.Utc;
    }
}
