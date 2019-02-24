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

using Algoloop.Common;
using Algoloop.Lean;
using Algoloop.Model;
using QuantConnect.Logging;
using QuantConnect.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Algoloop.Provider
{
    public class ProviderFactory : MarshalByRefObject
    {
        public MarketModel Run(MarketModel model, SettingsModel settings, HostDomainLogger logger)
        {
            Log.LogHandler = logger;
            PrepareDataFolder(settings.DataFolder);

            using (var writer = new StreamLogger(logger))
            {
                Console.SetOut(writer);
                MarketDownloader(model, settings);
            }

            Log.LogHandler.Dispose();
            return model;
        }

        internal static IEnumerable<SymbolModel> GetAllSymbols(string name)
        {
            IProvider provider = CreateProvider(name);
            if (provider == null)
                return null;

            return provider.GetAllSymbols();
        }

        public static void PrepareDataFolder(string dataFolder)
        {
            string marketHoursFolder = Path.Combine(dataFolder, "market-hours");
            const string marketHoursFile = "market-hours-database.json";
            string marketHoursPath = Path.Combine(marketHoursFolder, marketHoursFile);
            Directory.CreateDirectory(marketHoursFolder);
            string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", marketHoursFile);
            File.Copy(file, marketHoursPath, true);

            string symbolPropertiesFolder = Path.Combine(dataFolder, "symbol-properties");
            const string symbolPropertiesFile = "symbol-properties-database.csv";
            string symbolPropertiesPath = Path.Combine(symbolPropertiesFolder, symbolPropertiesFile);
            Directory.CreateDirectory(symbolPropertiesFolder);
            file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", symbolPropertiesFile);
            File.Copy(file, symbolPropertiesPath, true);
        }

        private static void MarketDownloader(MarketModel model, SettingsModel settings)
        {
            IList<string> list = model.Symbols.Where(m => m.Active).Select(m => m.Name).ToList();
            if (!list.Any())
            {
                Log.Trace($"No symbols selected");
                model.Active = false;
                return;
            }

            IProvider provider = CreateProvider(model.Provider);
            if (provider == null)
            {
                model.Active = false;
            }

            try
            {
                provider.Download(model, settings, list);
            }
            catch (Exception ex)
            {
                Log.Error(string.Format("{0}: {1}", ex.GetType(), ex.Message));
                model.Active = false;
            }
        }

        private static IProvider CreateProvider(string name)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => typeof(IProvider).IsAssignableFrom(p) && !p.IsInterface)
                .FirstOrDefault(m => m.Name.Equals(name));

            if (type == null)
            {
                Log.Trace($"Provider {name} not found");
                return null;
            }

            IProvider provider = (IProvider)Activator.CreateInstance(type);
            if (provider == null)
            {
                Log.Trace($"Can not create provider {name}");
                return null;
            }

            return provider;
        }
    }
}