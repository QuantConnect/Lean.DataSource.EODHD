# QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
# Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

from AlgorithmImports import *

### <summary>
### Example algorithm using the upcoming splits for universe filtering.
### </summary>
class EODHDUpcomingSplitsUniverseExampleAlgorithm(QCAlgorithm):

    def initialize(self):
        ''' Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized. '''

        # Data ADDED via universe selection is added with Daily resolution.
        self.universe_settings.resolution = Resolution.DAILY

        self.set_start_date(2021, 10, 25)
        self.set_end_date(2021, 10, 30)
        self.set_cash(100000)

        # Add a upcoming-splits universe data source.
        universe = self.add_universe(EODHDUpcomingSplits, self.selection)

        history = self.history(universe, TimeSpan(1, 0, 0, 0))
        if len(history) != 1:
            raise ValueError(f"Unexpected history count {len(history)}! Expected 1")

    def selection(self, data):
        ''' Selected the securities
        
        :param data: List of EODHDUpcomingSplits
        :return: List of Symbol objects '''

        # Select the ones close to the split date with split sizing up the share outstanding.
        return [d.symbol for d in data
                if d.split_date <= self.time + timedelta(3) and d.split_factor < 1]

    def OnSecuritiesChanged(self, changes):
        ''' Event fired each time that we add/remove securities from the data feed
		
        :param SecurityChanges changes: Security additions/removals for this time step
        '''
        self.Log(changes.ToString())