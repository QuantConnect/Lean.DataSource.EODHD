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
### Example algorithm using the upcoming dividends data as trade signal
### </summary>
class EODHDUpcomingDividendsDataAlgorithm(QCAlgorithm):

    def Initialize(self):
        ''' Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized.'''
        
        self.set_start_Date(2021, 10, 25)   #Set Start Date
        self.set_end_date(2021, 10, 30)    #Set End Date
        self.equity_symbol = self.add_equity("AAPL", Resolution.DAILY).symbol
        self.add_data(EODHDUpcomingDividends, "dividends")

    def OnData(self, slice):
        ''' OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.

        :param Slice slice: Slice object keyed by symbol containing the stock data
        '''
        # Order based on the updated upcoming dividends data.
        data = slice.get(EODHDUpcomingDividends)
        if data and self.equity_symbol in data:
            # Open position 3 days before dividends being paid when the popularity is not developed yet.
            upcoming_dividends_data = data[self.equity_symbol]
            if upcoming_dividends_data.dividend_date <= slice.time + timedelta(3) \
            and upcoming_dividends_data.dividend > 0.05:
                self.set_holdings(self.equity_symbol, 1)
        # Close position 1 day after the dividends being delivered to capitalized the popularity.
        elif self.portfolio[self.equity_symbol].invested:
            self.liquidate(self.equity_symbol)