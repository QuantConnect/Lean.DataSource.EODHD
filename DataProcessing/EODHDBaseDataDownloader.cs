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

using Newtonsoft.Json;
using QuantConnect.Configuration;
using QuantConnect.Logging;
using QuantConnect.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace QuantConnect.DataProcessing;

/// <summary>
/// EODHDBaseDataDownloader implementation.
/// </summary>
public partial class EODHDBaseDataDownloader : IDisposable
{
    public static readonly List<string> SupportedCurrencies = ["USD", "US", string.Empty, null];

    private readonly string _apiToken;
    private readonly string _destinationFolder;
    private readonly int _maxRetries = 5;
    private readonly List<char> _defunctDelimiters = ['-', '_'];
    private readonly RateGate _indexGate = new(
        Config.GetInt("rate-limit-requests", 10),
        TimeSpan.FromSeconds(Config.GetDouble("rate-limit", 1.1)));

    protected static readonly JsonSerializerSettings JsonSerializerSettings = new()
    {
        DateTimeZoneHandling = DateTimeZoneHandling.Utc,
        NullValueHandling = NullValueHandling.Ignore
    };

    /// <summary>
    /// Dataset endpoint
    /// </summary>
    protected virtual DateTime DeploymentDate { get; }

    /// <summary>
    /// Dataset endpoint
    /// </summary>
    protected virtual string Endpoint { get; }

    /// <summary>
    /// Vendor name
    /// </summary>
    public virtual string VendorName { get; } = "eodhd";

    /// <summary>
    /// Dataset name
    /// </summary>
    public virtual string VendorDataName { get; }

    protected EODHDBaseDataDownloader(string destinationFolder, string apiKey, DateTime deploymentDate, List<char> defunctDelimiters = null)
    {
        _apiToken = apiKey;
        _defunctDelimiters = defunctDelimiters ?? _defunctDelimiters;
        _destinationFolder = Path.Combine(destinationFolder, VendorName, VendorDataName);
        DeploymentDate = deploymentDate;
        Directory.CreateDirectory(_destinationFolder);
    }

    /// <summary>
    /// Runs the instance of the object.
    /// </summary>
    /// <param name="processDate">The date of data to be processed</param>
    /// <returns>True if process all downloads successfully</returns>
    public virtual async Task<bool> Run(DateTime processDate) => default;

    /// <summary>
    /// Sends a GET request for the provided URL
    /// </summary>
    /// <param name="url">URL to send GET request for</param>
    /// <returns>Content as string</returns>
    /// <exception cref="Exception">Failed to get data after exceeding retries</exception>
    protected async Task<string> HttpRequester(string url)
    {
        url = Uri.EscapeUriString($"{url}&api_token={_apiToken}");

        for (var retries = 1; retries <= _maxRetries; retries++)
        {
            try
            {
                using var client = new HttpClient();
                client.BaseAddress = new Uri(Endpoint);
                client.DefaultRequestHeaders.Clear();

                // Makes sure we don't overrun eodhd rate limits accidentally
                _indexGate.WaitToProceed();

                var response = await client.GetAsync(url);
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    Log.Error($"HttpRequester({Endpoint}): Files not found at url: {url}");
                    response.DisposeSafely();
                    return string.Empty;
                }

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    var finalRequestUri = response.RequestMessage.RequestUri; // contains the final location after following the redirect.
                    response = client.GetAsync(finalRequestUri).Result; // Reissue the request. The DefaultRequestHeaders configured on the client will be used, so we don't have to set them again.
                }

                if (response.StatusCode == HttpStatusCode.PaymentRequired)
                {
                    Log.Error($"HttpRequester({Endpoint}): {response.ReasonPhrase}");
                    response.DisposeSafely();
                    return string.Empty;
                }

                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadAsStringAsync();
                response.DisposeSafely();

                return result;
            }
            catch (Exception e)
            {
                Log.Error(e, $"HttpRequester({Endpoint}): Error at HttpRequester. (retry {retries}/{_maxRetries})");
                Thread.Sleep(1000);
            }
        }

        throw new Exception($"Request failed with no more retries remaining (retry {_maxRetries}/{_maxRetries})");
    }

    /// <summary>
    /// Disposes of unmanaged resources
    /// </summary>
    public virtual void Dispose()
    {
        _indexGate?.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Saves contents to disk, deleting existing zip files
    /// </summary>
    /// <param name="destinationFolder">Final destination of the data</param>
    /// <param name="name">file name</param>
    /// <param name="contents">Contents to write</param>
    protected virtual void SaveContentToFile(string destinationFolder, string name, IEnumerable<string> newContents)
    {
        var path = Path.Combine(_destinationFolder, destinationFolder);
        if (!string.IsNullOrWhiteSpace(destinationFolder))
        {
            Directory.CreateDirectory(path);
        }
        path = Path.Combine(path, $"{name.ToLowerInvariant()}.csv");

        var content = new HashSet<string>();
        if (File.Exists(path))
        {
            content.UnionWith(File.ReadAllLines(path));
        }
        content.UnionWith(newContents);
        File.WriteAllLines(path, content.OrderBy(x => x.Split(',').First()));
    }

    /// <summary>
    /// Saves contents to disk, deleting existing zip files
    /// </summary>
    /// <param name="destinationFolder">Final destination of the data</param>
    /// <param name="date">Date used to name the output file. E.g. 20241127.csv</param>
    /// <param name="contents">Contents to write</param>
    protected virtual void SaveContentToFile(string destinationFolder, DateTime date, IEnumerable<string> contents)
    {
        SaveContentToFile(destinationFolder, $"{date:yyyyMMdd}", contents);
    }

    /// <summary>
    /// Tries to normalize a potentially defunct ticker into a normal ticker.
    /// </summary>
    /// <param name="ticker">Ticker as received from Estimize</param>
    /// <param name="nonDefunctTicker">Set as the non-defunct ticker</param>
    /// <returns>true for success, false for failure</returns>
    protected bool TryNormalizeDefunctTicker(string ticker, out string nonDefunctTicker)
    {
        // The "defunct" indicator can be in any capitalization/case
        if (ticker.IndexOf("defunct", StringComparison.OrdinalIgnoreCase) > 0)
        {
            foreach (var delimChar in _defunctDelimiters)
            {
                var length = ticker.IndexOf(delimChar);

                // Continue until we exhaust all delimiters
                if (length == -1)
                {
                    continue;
                }

                nonDefunctTicker = ticker[..length].Trim();
                return true;
            }

            nonDefunctTicker = string.Empty;
            return false;
        }

        nonDefunctTicker = ticker;
        return true;
    }
}