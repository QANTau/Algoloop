﻿/*
 * Copyright 2018 Capnode AB
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

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;

namespace Algoloop.Model
{
    [DataContract]
    public class MarketsModel
    {
        public const int version = 1;

        [Description("Major Version - Increment at breaking change.")]
        [Browsable(false)]
        [DataMember]
        public int Version { get; set; }

        [Browsable(false)]
        [DataMember]
        public Collection<MarketModel> Markets { get; } = new Collection<MarketModel>();

        internal void Copy(MarketsModel marketsModel)
        {
            Markets.Clear();
            foreach (MarketModel market in marketsModel.Markets)
            {
                Markets.Add(market);

                // Make sure all symbols has an id
                foreach (SymbolModel symbol in market.Symbols)
                {
                    if (string.IsNullOrEmpty(symbol.Id))
                    {
                        symbol.Id = symbol.Name;
                        Debug.Assert(!string.IsNullOrEmpty(symbol.Id));
                    }
                }
            }
        }

        internal IReadOnlyList<MarketModel> GetMarkets()
        {
            return Markets;
        }

        internal MarketModel GetMarket(string market)
        {
            return Markets.FirstOrDefault(m => m.Name.Equals(market, StringComparison.OrdinalIgnoreCase));
        }
    }
}
