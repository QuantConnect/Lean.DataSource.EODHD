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

using System.Linq;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.DataSource;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// Example algorithm using the upcoming earnings for universe filtering.
    /// </summary>
    public class EODHDUpcomingEarningsUniverseExampleAlgorithm : QCAlgorithm
    {
        /// <summary>
        /// Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized.
        /// </summary>
        public override void Initialize()
        {
            // Data ADDED via universe selection is added with Daily resolution.
            UniverseSettings.Resolution = Resolution.Daily;

            SetStartDate(2021, 10, 25);
            SetEndDate(2021, 10, 30);
            SetCash(100000);

            // Add a upcoming-earnings universe data source.
            var universe = AddUniverse<EODHDUpcomingEarnings>(data =>
            {
                // Select the ones close to the earnings report date with reporting time after market closed.
                return from EODHDUpcomingEarnings d in data
                       where d.ReportDate <= Time.AddDays(3) && d.ReportTime == EODHD.ReportTime.AfterMarket
                       select d.Symbol;
            });

            var history = History(universe, 1).ToList();
            if (history.Count != 1)
            {
                throw new System.Exception($"Unexpected historical data count!");
            }
            foreach (var dataForDate in history)
            {
                var coarseData = dataForDate.ToList();
                if (coarseData.Count < 300)
                {
                    throw new System.Exception($"Unexpected historical universe data!");
                }
            }
        }

        /// <summary>
        /// Event fired each time that we add/remove securities from the data feed
        /// </summary>
        /// <param name="changes">Security additions/removals for this time step</param>
        public override void OnSecuritiesChanged(SecurityChanges changes)
        {
            Log(changes.ToString());
        }
    }
}