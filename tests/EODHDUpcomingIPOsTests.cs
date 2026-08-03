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
using NUnit.Framework;
using QuantConnect.Data;
using QuantConnect.DataSource;

namespace QuantConnect.DataLibrary.Tests;

[TestFixture]
public class EODHDUpcomingIPOsTests
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

    [Test]
    public void ReaderParsesValidLine()
    {
        var factory = new EODHDUpcomingIPOs();
        var line = "USNG YSLK5RJ61X7P,USNG,Amplify Samsung U.S. Natural Gas Infrastructure ETF,NYSE,20250520,20250520,20250520,12.62,12.62,,,Expected";
        var result = (EODHDUpcomingIPOs)factory.Reader(null, line, new DateTime(2025, 5, 13), false);

        Assert.IsNotNull(result);
        Assert.AreEqual("USNG", result.Symbol.Value);
        Assert.AreEqual("Amplify Samsung U.S. Natural Gas Infrastructure ETF", result.Name);
        Assert.AreEqual(Exchange.NYSE, result.Exchange);
        Assert.AreEqual(new DateTime(2025, 5, 20), result.IpoDate);
        Assert.AreEqual(new DateTime(2025, 5, 20), result.FilingDate);
        Assert.AreEqual(new DateTime(2025, 5, 20), result.AmendedDate);
        Assert.AreEqual(12.62m, result.LowestPrice);
        Assert.AreEqual(12.62m, result.HighestPrice);
        Assert.IsNull(result.OfferPrice);
        Assert.IsNull(result.Shares);
        Assert.AreEqual(EODHD.DealType.Expected, result.DealType);
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
        return new EODHDUpcomingIPOs
        {
            Symbol = Symbol.Empty,
            Time = DateTime.Today,
            DataType = MarketDataType.Base,
            Name = string.Empty,
            Exchange = Exchange.NYSE,
            IpoDate = new DateTime(2024, 1, 1),
            FilingDate = new DateTime(2024, 1, 1),
            AmendedDate = new DateTime(2024, 1, 1),
            LowestPrice = null,
            HighestPrice = null,
            OfferPrice = 10m,
            Shares = 1000000m,
            DealType = EODHD.DealType.Priced,
        };
    }
}