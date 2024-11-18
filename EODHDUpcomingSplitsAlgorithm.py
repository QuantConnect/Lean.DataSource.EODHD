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
### Example algorithm using the upcoming splits data as trade signal
### </summary>
class EODHDUpcomingSplitsDataAlgorithm(QCAlgorithm):

    def Initialize(self):
        ''' Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized.'''
        
        self.set_start_Date(2021, 10, 25)   #Set Start Date
        self.set_end_date(2021, 10, 30)    #Set End Date
        self.equity_symbol = self.add_equity("AAPL", Resolution.DAILY).symbol
        self.add_data(EODHDUpcomingSplits, "splits")

    def OnData(self, slice):
        ''' OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.

        :param Slice slice: Slice object keyed by symbol containing the stock data
        '''
        # Order based on the updated upcoming splits data.
        data = slice.get(EODHDUpcomingSplits)
        if data and self.equity_symbol in data:
            # Open position 3 days before splits when the popularity is not developed yet.
            # Increasing price will make unit share less liquid, so a company split its shares to lower price.
            # We buy the stock if it splits into lower price shares.
            upcoming_splits_data = data[self.equity_symbol]
            if upcoming_splits_data.split_date <= slice.time + timedelta(3) \
            and upcoming_splits_data.split_factor < 1:
                self.set_holdings(self.equity_symbol, 1)
            # Decreasing price will make the equity becomes penny stock, so a company merges its shares to higher price.
            # We sell the stock if it merges into higher price shares.
            elif upcoming_splits_data.split_date <= slice.time + timedelta(3) \
            and upcoming_splits_data.split_factor > 1:
                self.set_holdings(self.equity_symbol, -1)
        # Close position 1 day after the splits realized to capitalized the popularity trend.   
        elif self.portfolio[self.equity_symbol].invested:
            self.liquidate(self.equity_symbol)