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

using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
namespace QuantConnect.DataSource;

/// <summary>
/// EODHD static class contains shortcut definitions
/// </summary>
public static partial class EODHD
{
    /// <summary>
    /// The earnings report time.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum DealType
    {
        /// <summary>
        /// The IPO is anticipated to happen soon, but the company has not yet finalized a price or date
        /// </summary>
        [EnumMember(Value = "Expected")]
        Expected,

        /// <summary>
        /// The company has set the final price for its shares, and the offering is ready to occur.
        /// </summary>
        [EnumMember(Value = "Priced")]
        Priced,

        /// <summary>
        /// The company has submitted its registration statement to the regulatory body, but has not yet started the offering process.
        /// </summary>
        [EnumMember(Value = "Filed")]
        Filed,

        /// <summary>
        /// The company has made changes to its previously filed registration statement, often in response to feedback from regulators or to update financial information.
        /// </summary>
        [EnumMember(Value = "Amended")]
        Amended,

        /// <summary>
        /// The company has decided to cancel the IPO process before it becomes effective.
        /// </summary>
        [EnumMember(Value = "Withdrawn")]
        Withdrawn,

        /// <summary>
        /// The IPO has been delayed, typically due to market conditions or company-specific reasons.
        /// </summary>
        [EnumMember(Value = "Postponed")]
        Postponed
    }
}