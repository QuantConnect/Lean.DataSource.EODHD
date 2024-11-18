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
### Example algorithm using the upcoming IPOs as a source of alpha
### </summary>
class EODHDUpcomingIPOsUniverseSelectionAlgorithm(QCAlgorithm):
    def initialize(self):
        ''' Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized. '''
        self.universe_settings.resolution = Resolution.DAILY

        self.set_start_date(2022, 2, 14)
        self.set_end_date(2022, 2, 18)
        self.set_cash(100000)

        self.add_universe(EODHDUpcomingIPOs, self.universe_selection)

    def universe_selection(self, data):
        ''' Selected the securities
        
        :param List of EODHDUpcomingIPOs data: List of EODHDUpcomingIPOs
        :return: List of Symbol objects '''

        return [d.symbol for d in data if d.exchange == Exchange.NYSE and d.deal_type != DealType.WITHDRAWN \
                and d.deal_type != DealType.POSTPONED and d.offer_price and d.offer_price >= 1]

    def on_securities_changed(self, changes):
        ''' Event fired each time that we add/remove securities from the data feed
		
        :param SecurityChanges changes: Security additions/removals for this time step
        '''
        self.log(str(changes))