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
    public enum ReportTime
    {
        /// <summary>
        /// The earnings will be reported that day before market open.
        /// </summary>
        [EnumMember(Value = "BeforeMarket")]
        BeforeMarket,

        /// <summary>
        /// The earnings will be reported that day after market closed.
        /// </summary>
        [EnumMember(Value = "AfterMarket")]
        AfterMarket
    }
}