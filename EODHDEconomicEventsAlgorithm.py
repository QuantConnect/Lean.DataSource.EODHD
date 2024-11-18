﻿# QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
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
### Example algorithm using the economic events as a source of alpha
### </summary>
class EODHDEconomicEventsAlgorithm(QCAlgorithm):
    def initialize(self):
        ''' Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized.'''
        
        self.set_start_date(2020, 10, 7)   #Set Start Date
        self.set_end_date(2020, 10, 11)    #Set End Date
        self.equity_symbol = self.add_equity("SPY", Resolution.DAILY).symbol
        self.dataset_symbol = self.add_data(EODHDEconomicEvents, EODHD.Events.UnitedStates.MARKIT_MANUFACTURING__PURCHASING_MANAGERS_INDEX).symbol

    def on_data(self, slice):
        ''' OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.

        :param Slice slice: Slice object keyed by symbol containing the stock data
        '''
        for _, event in slice.get(EODHDEconomicEvents).items():
            if event.previous and event.estimate:
                if event.previous < event.estimate:
                    self.SetHoldings(self.equity_symbol, 1)
                else:
                    self.SetHoldings(self.equity_symbol, -1)