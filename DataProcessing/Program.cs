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
using System.Collections.Generic;
using System.IO;

namespace QuantConnect.DataProcessing;

/// <summary>
/// Entrypoint for the data downloader/converter
/// </summary>
public class Program
{
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
        var destinationDirectory = Path.Combine(Config.Get("temp-output-directory", "/temp-output-directory"), "alternative");
        var deploymentDateValue = Environment.GetEnvironmentVariable("QC_DATAFLEET_DEPLOYMENT_DATE");
        var deploymentDate = string.IsNullOrWhiteSpace(deploymentDateValue) ? DateTime.UtcNow.Date : Parse.DateTimeExact(deploymentDateValue, "yyyyMMdd");
        var processDate = Parse.DateTimeExact(Config.Get("process-start-date", $"{deploymentDate:yyyyMMdd}"), "yyyyMMdd");
        var downloaders = new List<EODHDBaseDataDownloader>();

        try
        {
            // Pass in the values we got from the configuration into the downloader/converter.
            if (Config.GetBool("process-macro-indicators"))
            {
                downloaders.Add(new EODHDMacroIndicatorsDataDownloader(destinationDirectory, apiKey));
            }
            else
            {
                downloaders.Add(new EODHDUpcomingEarningsDataDownloader(destinationDirectory, apiKey, deploymentDate));
                downloaders.Add(new EODHDUpcomingIPOsDataDownloader(destinationDirectory, apiKey, deploymentDate));
                downloaders.Add(new EODHDUpcomingSplitsDataDownloader(destinationDirectory, apiKey, deploymentDate));
                // Upcoming 7 days data.
                downloaders.Add(new EODHDUpcomingDividendsDataDownloader(destinationDirectory, apiKey, deploymentDate.AddDays(7)));
                downloaders.Add(new EODHDEconomicEventsDataDownloader(destinationDirectory, apiKey, deploymentDate));
            }   
        }
        catch (Exception err)
        {
            Log.Error(err, $"QuantConnect.DataProcessing.Program.Main(): Failed to construct downloader/converter");
            Environment.Exit(1);
        }

        // No need to edit anything below here for most use cases.
        // The downloader/converter is ran and cleaned up for you safely here.
        var success = true;
        foreach (var downloader in downloaders)
        {
            try
            {
                if (!downloader.Run(processDate).Result)
                {
                    Log.Error($"QuantConnect.DataProcessing.Program.Main(): Failed to download/process {downloader.VendorName} {downloader.VendorDataName} data");
                    success = false;
                }
            }
            catch (Exception err)
            {
                Log.Error(err, $"QuantConnect.DataProcessing.Program.Main(): The downloader/converter for {downloader.VendorDataName} {downloader.VendorDataName} data exited unexpectedly");
                success = false;
            }
            // Run cleanup of the downloader/converter once it has finished or crashed.
            downloader.DisposeSafely();
        }

        // The downloader/converter was successful
        Environment.Exit(success ? 0 : 1);
    }
}