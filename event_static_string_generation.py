### Script to generate the "enum" static string of event types
from inflect import engine
from json import loads
from pathlib import Path
from re import sub
from urllib.request import urlopen

### PARAMETERS
OUTPUT_PATH = Path("/temp-output-directory/alternative/eodhd/economicevents")   # The output data files location to ietrate through

# Method to convert abrabic number to string
p = engine()
def replace_numbers_with_words(text):
    edge_case = {
        "1848": "eighteen forty eight",
        "20th": "twentieth"
    }
    # Function to convert numbers in a match object to words
    def convert(match):
        number = int(match.group())
        return p.number_to_words(number).replace('-', ' ').replace(',','')

    for key, value in edge_case.items():
        text = text.replace(key, value)
        
    # Use regex to find all numbers in the text
    result = sub(r'\b\d+\b', convert, text)
    return result

# The country code to country map file
iso_to_name = {}
lines = urlopen("https://raw.githubusercontent.com/QuantConnect/Lean/refs/heads/master/Common/Country.cs")
lines = lines.read().decode("utf-8").split('\n')
for i, line in enumerate(lines):
    start = line.find('public const string')
    if start < 0:
        continue
    x = lines[i-3]
    iso_to_name[line[-5:-2]] = [lines[i-2][12:], line[20+start:-9]]

### Create static event class
content = """/*
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

namespace QuantConnect.DataSource;

/// <summary>
/// EODHD static class contains shortcut definitions
/// </summary>
public static partial class EODHD
{
    /// <summary>
    /// The Events class contains all events normalized for your convenience
    /// </summary>
    public static class Events
    {"""

countries = sorted([iso_to_name.get(_dir.name.upper()), _dir] for _dir in OUTPUT_PATH.iterdir())
for (summary, name), country in countries:
    print(summary)
    content += f'''
        /// <summary>
        /// {summary}
        /// </summary>
        public static class {name}
        {{
'''

    events = set()
    for filename in country.iterdir():
        with open(filename, mode='r') as fp:
            for line in fp.readlines():
                events.add(line.split(',')[2].replace("_", " "))
    for event in sorted(events):
        content += f'''            /// <summary>
            /// {event.title()}
            /// </summary>
            public const string {replace_numbers_with_words(event).title().replace(" ", "")} = "{country.name.upper()}/{event.replace(" ", "_")}";
'''
    content += '''        }
'''
content += """    }
}"""

# Write to CS class file
with open("EODHD.Events.cs", "w", encoding="utf-8") as file:
    file.write(content)