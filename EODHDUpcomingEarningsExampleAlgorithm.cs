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
using QuantConnect.Algorithm;
using QuantConnect.DataSource;

namespace QuantConnect.DataLibrary.Tests
{
    /// <summary>
    /// Example algorithm using the upcoming earnings data as trade signal
    /// </summary>
    public class EODHDUpcomingEarningsDataAlgorithm : QCAlgorithm
    {
        private Symbol _equitySymbol;

        /// <summary>
        /// Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized.
        /// </summary>
        public override void Initialize()
        {
            SetStartDate(2021, 10, 25);  //Set Start Date
            SetEndDate(2021, 10, 30);    //Set End Date
            _equitySymbol = AddEquity("AAPL", Resolution.Daily).Symbol;
            AddData<EODHDUpcomingEarnings>("earnings");
        }

        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="slice">Slice object keyed by symbol containing the stock data</param>
        public override void OnData(Slice slice)
        {
            // Order based on the updated upcoming earnings data.
            var earnings = slice.Get<EODHDUpcomingEarnings>();
            if (earnings.TryGetValue(_equitySymbol, out var upcomingEarningsData))
            {
                // Open position 3 days before earnings to avoid hyped volatility close to report published.
                // Based on the upcoming report estimate earnings, we will buy the equity if the estimated earnings is positive.
                if (upcomingEarningsData.ReportDate <= slice.Time.AddDays(3)
                    && upcomingEarningsData.Estimate.HasValue
                    && upcomingEarningsData.Estimate > 0)
                {
                    SetHoldings(_equitySymbol, 1);
                }
                // While sell the equity if the estimated earnings is negative.
                else if (upcomingEarningsData.ReportDate <= slice.Time.AddDays(3)
                    && upcomingEarningsData.Estimate.HasValue
                    && upcomingEarningsData.Estimate < 0)
                {
                    SetHoldings(_equitySymbol, -1);
                }
            }
            // Close position 1 day after the earnings report published to capitalized the volatility.
            else if (Portfolio[_equitySymbol].Invested)
            {
                Liquidate(_equitySymbol);
            }
        }
    }
}
