// Copyright 2013-2015 Serilog Contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.IO;

using Serilog.Events;

namespace Serilog.Formatting.Display
{
    // Allows for the specific handling of the {Level} element.
    // can now have a fixed width applied to it, as well as casing rules.
    // Width is set through formats like "u3" (uppercase three chars),
    // "w1" (one lowercase char), or "t4" (title case four chars).
    class LogEventLevelValue : LogEventPropertyValue
    {
        readonly LogEventLevel _value;

        static readonly string[][] _titleCaseLevelMap = {
            new []{ "V", "Vb", "Vrb", "Verb" },
            new []{ "D", "De", "Dbg", "Dbug" },
            new []{ "I", "In", "Inf", "Info" },
            new []{ "W", "Wn", "Wrn", "Warn" },
            new []{ "E", "Er", "Err", "Eror" },
            new []{ "F", "Fa", "Ftl", "Fatl" }
        };

        static readonly string[][] _lowercaseLevelMap = {
            new []{ "v", "vb", "vrb", "verb" },
            new []{ "d", "de", "dbg", "dbug" },
            new []{ "i", "in", "inf", "info" },
            new []{ "w", "wn", "wrn", "warn" },
            new []{ "e", "er", "err", "eror" },
            new []{ "f", "fa", "ftl", "fatl" }
        };

        static readonly string[][] _uppercaseLevelMap = {
            new []{ "V", "VB", "VRB", "VERB" },
            new []{ "D", "DE", "DBG", "DBUG" },
            new []{ "I", "IN", "INF", "INFO" },
            new []{ "W", "WN", "WRN", "WARN" },
            new []{ "E", "ER", "ERR", "EROR" },
            new []{ "F", "FA", "FTL", "FATL" }
        };

        public LogEventLevelValue(LogEventLevel value)
        {
            _value = value;
        }

        /// <summary>
        /// This method will apply only upper or lower case formatting, not fixed width
        /// </summary>
        public override void Render(TextWriter output, string format = null, IFormatProvider formatProvider = null)
        {
            if (format != null && (format.Length == 2 || format.Length == 3))
            {
                // Using int.Parse() here requires allocating a string to exclude the first character prefix.
                // Junk like "wxy" will be accepted but produce benign results.
                var width = format[1] - '0';
                if (format.Length == 3)
                {
                    width *= 10;
                    width += format[2] - '0';
                }

                if (width < 1)
                    return;

                if (width > 4)
                {
                    var value = _value.ToString();
                    if (value.Length > width)
                        value = value.Substring(0, width);
                    output.Write(Casing.Format(value));
                    return;
                }

                var index = (int)_value;
                if (index >= 0 && index <= (int) LogEventLevel.Fatal)
                {
                    switch (format[0])
                    {
                        case 'w':
                            output.Write(_lowercaseLevelMap[index][width - 1]);
                            return;
                        case 'u':
                            output.Write(_uppercaseLevelMap[index][width - 1]);
                            return;
                        case 't':
                            output.Write(_titleCaseLevelMap[index][width - 1]);
                            return;
                    }
                }
            }

            output.Write(Casing.Format(_value.ToString(), format));
        }
    }
}