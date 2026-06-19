from datetime import datetime, timedelta
import pandas as pd
import requests
import time

BASE = "https://api.nasdaq.com/api/calendar/earnings"
OUTPUT_PATH = "reconciliation/earnings_reference.csv"
START_DATE = datetime(2015, 1, 1)
END_DATE = datetime(2025, 2, 20)

df = pd.DataFrame()
date = START_DATE
headers = {
    'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36 Edg/133.0.0.0',
}

while date < END_DATE:
    date += timedelta(1)
    url = f"{BASE}?date={date:%Y-%m-%d}"
    
    try:
        request = requests.get(url, headers=headers)
        time.sleep(0.05)
        content = request.json()
        
        if not content["data"]["rows"]:
            continue
        
        ticker_list = []
        estimates = []
        for datum in content["data"]["rows"]:
            ticker_list.append(datum["symbol"])
            estimates.append(
                float(datum["epsForecast"].translate({ord(x): '' for x in [',', '(', ')', '$']}))\
                if datum["epsForecast"]\
                else ''
            )
            
        indices = pd.MultiIndex.from_tuples([(ticker, date) for ticker in ticker_list])
        df_ = pd.DataFrame({"estimate": estimates}, index=indices)
        
        df = pd.concat([df, df_])
    
    except Exception as e:
        print(f"Failed to request URL: {url} - {e}")

df = df.sort_index()
df.to_csv(OUTPUT_PATH)