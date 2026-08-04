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
*/

using QuantConnect.Configuration;
using QuantConnect.Logging;
using QuantConnect.Util;
using System;
using System.IO;

namespace QuantConnect.DataProcessing;

/// <summary>
/// Entrypoint for the data downloader/converter
/// </summary>
public class Program
{
    private const string VendorName = "eodhd";

    /// <summary>
    /// Entrypoint of the program
    /// </summary>
    /// <returns>Exit code. 0 equals successful, and any other value indicates the downloader/converter failed.</returns>
    public static void Main()
    {
        var apiKey = Config.Get("vendor-auth-token");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Log.Error($"QuantConnect.DataProcessing.Program.Main(): \"vendor-auth-token\" is null or empty.");
            Environment.Exit(1);
        }

        // Get the config values first before running. These values are set for us
        // automatically to the value set on the website when defining this data type
        var dataset = Config.Get("dataset-name", "intraday").Trim().ToLowerInvariant();
        var destinationDirectory = Path.Combine(Config.Get("temp-output-directory", "/temp-output-directory"), "alternative");
        var deploymentDateValue = Environment.GetEnvironmentVariable("QC_DATAFLEET_DEPLOYMENT_DATE");
        var deploymentDate = string.IsNullOrWhiteSpace(deploymentDateValue) ? DateTime.UtcNow.Date : Parse.DateTimeExact(deploymentDateValue, "yyyyMMdd");
        var processDate = Parse.DateTimeExact(Config.Get("process-start-date", $"{deploymentDate:yyyyMMdd}"), "yyyyMMdd");

        switch (dataset)
        {
            case "upcomingearnings":
                RunDownloader(dataset, processDate, () => new EODHDUpcomingEarningsDataDownloader(destinationDirectory, apiKey, deploymentDate));
                break;

            case "upcomingipos":
                RunDownloader(dataset, processDate, () => new EODHDUpcomingIPOsDataDownloader(destinationDirectory, apiKey, deploymentDate));
                break;

            case "upcomingsplits":
                RunDownloader(dataset, processDate, () => new EODHDUpcomingSplitsDataDownloader(destinationDirectory, apiKey, deploymentDate));
                break;

            case "upcomingdividends":
                // Upcoming 7 days data.
                RunDownloader(dataset, processDate, () => new EODHDUpcomingDividendsDataDownloader(destinationDirectory, apiKey, deploymentDate.AddDays(7)));
                break;

            case "economicevents":
                RunDownloader(dataset, processDate, () => new EODHDEconomicEventsDataDownloader(destinationDirectory, apiKey, deploymentDate));
                break;

            case "macroindicators":
                RunDownloader(dataset, processDate, () => new EODHDMacroIndicatorsDataDownloader(destinationDirectory, apiKey));
                break;

            case "intraday":
                // Comma-separated EODHD symbols, e.g. "USDKRW.FOREX,EURUSD.FOREX"
                var symbols = Config.Get("symbols").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (symbols.Length == 0)
                {
                    Log.Error($"QuantConnect.DataProcessing.Program.Main(): The '{dataset}' dataset requires the \"symbols\" configuration, e.g. \"USDKRW.FOREX,EURUSD.FOREX\"");
                    Environment.Exit(1);
                }
                RunDownloader(dataset, processDate, () => new EODHDIntradayDataDownloader(destinationDirectory, Globals.DataFolder, apiKey, deploymentDate, symbols));
                break;

            default:
                var validDatasets = string.Join(", ", "upcomingearnings", "upcomingipos", "upcomingsplits",
                    "upcomingdividends", "economicevents", "macroindicators", "intraday");
                Log.Error($"QuantConnect.DataProcessing.Program.Main(): Unknown dataset '{dataset}'. Valid \"dataset-name\" options: {validDatasets}");
                Environment.Exit(1);
                break;
        }

        // The downloader/converter was successful
        Environment.Exit(0);
    }

    private static void RunDownloader(string vendorDataName, DateTime processDate, Func<EODHDBaseDataDownloader> factory)
    {
        EODHDBaseDataDownloader instance = null;
        try
        {
            instance = factory();
        }
        catch (Exception err)
        {
            Log.Error(err, $"QuantConnect.DataProcessing.Program.Main(): The downloader/converter for {VendorName} {vendorDataName} data failed to be constructed");
            Environment.Exit(1);
        }

        try
        {
            if (!instance.Run(processDate).Result)
            {
                Log.Error($"QuantConnect.DataProcessing.Program.Main(): Failed to download/process {VendorName} {vendorDataName} data");
                Environment.Exit(1);
            }
        }
        catch (Exception err)
        {
            Log.Error(err, $"QuantConnect.DataProcessing.Program.Main(): The downloader/converter for {VendorName} {vendorDataName} data exited unexpectedly");
            Environment.Exit(1);
        }
        finally
        {
            instance.DisposeSafely();
        }
    }
}
