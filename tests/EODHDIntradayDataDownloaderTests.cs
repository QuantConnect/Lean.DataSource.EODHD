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
using System.Linq;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using QuantConnect.Data.Market;
using QuantConnect.DataProcessing;

namespace QuantConnect.DataLibrary.Tests;

[TestFixture]
public class EODHDIntradayDataDownloaderTests
{
    private static readonly Symbol _symbol = Symbol.Create("USDKRW", SecurityType.Forex, Market.Oanda);
    private static readonly DateTime _referenceDate = new(2024, 1, 2);

    [Test]
    public void RemoveOutliersKeepsRegularBars()
    {
        var bars = CreateDay(_referenceDate, 1300m, 500).ToList();

        var result = EODHDIntradayDataDownloader.RemoveOutliers(bars);

        CollectionAssert.AreEqual(bars, result);
    }

    [Test]
    public void RemoveOutliersDiscardsJunkPrint()
    {
        // The actual USDKRW 2024-01-02 case: an isolated 2.4 print on a day the pair traded around 1300
        var junk = CreateBar(_referenceDate.AddMinutes(324), 2.4m);
        var bars = CreateDay(_referenceDate, 1300m, 500).Prepend(junk).ToList();

        var result = EODHDIntradayDataDownloader.RemoveOutliers(bars);

        Assert.AreEqual(bars.Count - 1, result.Count);
        CollectionAssert.DoesNotContain(result, junk);
    }

    [TestCase(0)]
    [TestCase(-1300)]
    public void RemoveOutliersDiscardsNonPositivePrices(decimal price)
    {
        var junk = CreateBar(_referenceDate.AddMinutes(324), price);
        var bars = CreateDay(_referenceDate, 1300m, 500).Prepend(junk).ToList();

        var result = EODHDIntradayDataDownloader.RemoveOutliers(bars);

        Assert.AreEqual(bars.Count - 1, result.Count);
        CollectionAssert.DoesNotContain(result, junk);
    }

    [Test]
    public void RemoveOutliersKeepsGenuineIntradayMove()
    {
        // The widest genuine intraday move in the USDKRW history is about 10%, from 1121 to 1192 on 2010-05-20
        var bars = new List<QuoteBar>();
        for (var i = 0; i < 500; i++)
        {
            bars.Add(CreateBar(_referenceDate.AddMinutes(i), 1121m + i * 0.142m));
        }

        var result = EODHDIntradayDataDownloader.RemoveOutliers(bars);

        CollectionAssert.AreEqual(bars, result);
    }

    [Test]
    public void RemoveOutliersKeepsTrendAcrossTheWindow()
    {
        // A 120 day window can hold a large drift: each day is judged against its own median, not the window's
        var bars = new List<QuoteBar>();
        for (var day = 0; day < 120; day++)
        {
            bars.AddRange(CreateDay(_referenceDate.AddDays(day), 1000m + day * 5m, 100));
        }

        var result = EODHDIntradayDataDownloader.RemoveOutliers(bars);

        CollectionAssert.AreEqual(bars, result);
    }

    [Test]
    public void RemoveOutliersFallsBackToWindowMedianOnSparseDays()
    {
        // A junk print alone on a day has no daily median to be judged against: the window median is used
        var junk = CreateBar(_referenceDate.AddDays(1).AddMinutes(30), 2.4m);
        var bars = CreateDay(_referenceDate, 1300m, 500).Append(junk).ToList();

        var result = EODHDIntradayDataDownloader.RemoveOutliers(bars);

        Assert.AreEqual(bars.Count - 1, result.Count);
        CollectionAssert.DoesNotContain(result, junk);
    }

    [Test]
    public void RemoveOutliersKeepsChronologicalOrder()
    {
        var junk = CreateBar(_referenceDate.AddMinutes(324), 2.4m);
        var bars = CreateDay(_referenceDate, 1300m, 300)
            .Concat(CreateDay(_referenceDate.AddDays(1), 1310m, 300))
            .Prepend(junk)
            .ToList();

        var result = EODHDIntradayDataDownloader.RemoveOutliers(bars);

        CollectionAssert.AreEqual(result.OrderBy(x => x.Time).ToList(), result);
    }

    [Test]
    public void RemoveOutliersHandlesEmptyInput()
    {
        Assert.IsEmpty(EODHDIntradayDataDownloader.RemoveOutliers(new List<QuoteBar>()));
    }

    private static IEnumerable<QuoteBar> CreateDay(DateTime date, decimal price, int count)
    {
        // Alternating one tick around the given price keeps the median at it
        return Enumerable.Range(0, count).Select(i => CreateBar(date.AddMinutes(i), price + (i % 2 == 0 ? 0.01m : -0.01m)));
    }

    private static QuoteBar CreateBar(DateTime time, decimal price)
    {
        var bar = new Bar(price, price, price, price);
        return new QuoteBar(time, _symbol, bar, 0m, bar, 0m, Time.OneMinute);
    }
}
