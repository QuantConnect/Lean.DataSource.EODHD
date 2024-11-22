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
using System.Linq;
using Newtonsoft.Json;
using NodaTime;
using NUnit.Framework;
using QuantConnect.Data;
using QuantConnect.DataSource;

namespace QuantConnect.DataLibrary.Tests;

[TestFixture]
public class EODHDMacroIndicatorsTests
{
    [Test]
    public void JsonRoundTrip()
    {
        var expected = CreateNewInstance();
        var type = expected.GetType();
        var serialized = JsonConvert.SerializeObject(expected);
        var result = JsonConvert.DeserializeObject(serialized, type);

        AssertAreEqual(expected, result);
    }

    [Test]
    public void Clone()
    {
        var expected = CreateNewInstance();
        var result = expected.Clone();

        AssertAreEqual(expected, result);
    }

    [TestCase("20240131,merchandise_trade_percent_gdp,Annual,18.9762")]
    [TestCase("20240131,unemployment_total_percent,Annual,3.638")]
    public void ReaderCountryTest(string line)
    {
        var config = new SubscriptionDataConfig(typeof(EODHDMacroIndicators),
            symbol: Symbol.CreateBase(typeof(EODHDMacroIndicators), Country.UnitedStates),
            Resolution.Daily, DateTimeZone.Utc, DateTimeZone.Utc, false, false, false);
        Assert.IsNotNull(CreateNewInstance().Reader(config, line, DateTime.UtcNow, false));
    }

    [TestCase("20240131,merchandise_trade_percent_gdp,Annual,18.9762", true)]
    [TestCase("20240131,unemployment_total_percent,Annual,3.638", false)]
    public void ReaderEventTest(string line, bool isNull)
    {
        var config = new SubscriptionDataConfig(typeof(EODHDMacroIndicators),
            symbol: Symbol.CreateBase(typeof(EODHDMacroIndicators), EODHD.MacroIndicators.UnitedStates.UnemploymentTotalPercent),
            Resolution.Daily, DateTimeZone.Utc, DateTimeZone.Utc, false, false, false);
        var data = CreateNewInstance().Reader(config, line, DateTime.UtcNow, false);
        if (isNull) { Assert.IsNull(data); } else { Assert.IsNotNull(data); }
    }


    private static void AssertAreEqual(object expected, object result, bool filterByCustomAttributes = false)
    {
        foreach (var propertyInfo in expected.GetType().GetProperties())
        {
            // we skip Symbol which isn't protobuffed
            if (filterByCustomAttributes && propertyInfo.CustomAttributes.Any())
            {
                Assert.AreEqual(propertyInfo.GetValue(expected), propertyInfo.GetValue(result));
            }
        }
        foreach (var fieldInfo in expected.GetType().GetFields())
        {
            Assert.AreEqual(fieldInfo.GetValue(expected), fieldInfo.GetValue(result));
        }
    }

    private static BaseData CreateNewInstance()
    {
        return new EODHDMacroIndicator
        {
            Symbol = Symbol.Empty,
            Time = DateTime.Today,
            DataType = MarketDataType.Base,
            Country = "USA",
            Indicator = "gdp_growth_annual",
            Frequency = EODHD.Frequency.Annual,
            Value = 0m
        };
    }
}