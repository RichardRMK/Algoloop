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

using Algoloop.Model;
using Algoloop.ViewSupport;
using Algoloop.WPF.DataGrid;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using Microsoft.Win32;
using Newtonsoft.Json;
using QuantConnect.AlgorithmFactory;
using QuantConnect.Logging;
using QuantConnect.Parameters;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Algoloop.ViewModel
{
    public class StrategyViewModel : ViewModelBase, ITreeViewModel
    {
        public const string DefaultName = "Strategy";

        private readonly string[] _exclude = new[] { "symbols", "resolution", "market", "startdate", "enddate", "cash" };
        private readonly StrategiesViewModel _parent;
        private bool _isSelected;
        private bool _isExpanded;
        private SymbolViewModel _selectedSymbol;
        private ObservableCollection<DataGridColumn> _trackColumns = new ObservableCollection<DataGridColumn>();
        private TrackViewModel _selectedTrack;
        private string _displayName;
        private readonly SettingsModel _settingsModel;

        public StrategyViewModel(StrategiesViewModel parent, StrategyModel model, SettingsModel settingsModel)
        {
            _parent = parent;
            Model = model;
            _settingsModel = settingsModel;

            StartCommand = new RelayCommand(() => DoRunStrategy(), () => true);
            StopCommand = new RelayCommand(() => { }, () => false);
            CloneCommand = new RelayCommand(() => DoCloneStrategy(), () => true);
            CloneAlgorithmCommand = new RelayCommand(() => DoCloneAlgorithm(), () => !string.IsNullOrEmpty(Model.AlgorithmName));
            ExportCommand = new RelayCommand(() => DoExportStrategy(), () => true);
            DeleteCommand = new RelayCommand(() => _parent?.DoDeleteStrategy(this), () => true);
            DeleteAllTracksCommand = new RelayCommand(() => DoDeleteAllTracks(), () => true);
            DeleteSelectedTracksCommand = new RelayCommand<IList>(m => DoDeleteTracks(m), m => true);
            UseParametersCommand = new RelayCommand<IList>(m => DoUseParameters(m), m => true);
            AddSymbolCommand = new RelayCommand(() => DoAddSymbol(), () => true);
            DeleteSymbolsCommand = new RelayCommand<IList>(m => DoDeleteSymbols(m), m => SelectedSymbol != null);
            ImportSymbolsCommand = new RelayCommand(() => DoImportSymbols(), () => true);
            ExportSymbolsCommand = new RelayCommand<IList>(m => DoExportSymbols(m), trm => SelectedSymbol != null);
            TrackDoubleClickCommand = new RelayCommand<TrackViewModel>(m => DoSelectItem(m));
            MoveUpSymbolsCommand = new RelayCommand<IList>(m => OnMoveUpSymbols(m), m => SelectedSymbol != null);
            MoveDownSymbolsCommand = new RelayCommand<IList>(m => OnMoveDownSymbols(m), m => SelectedSymbol != null);
            SortSymbolsCommand = new RelayCommand(() => Symbols.Sort(), () => true);

            Model.NameChanged += StrategyNameChanged;
            Model.AlgorithmNameChanged += AlgorithmNameChanged;
            DataFromModel();

            Model.EndDate = DateTime.Now;
        }

        public RelayCommand StartCommand { get; }
        public RelayCommand StopCommand { get; }
        public RelayCommand CloneCommand { get; }
        public RelayCommand CloneAlgorithmCommand { get; }
        public RelayCommand ExportCommand { get; }
        public RelayCommand DeleteCommand { get; }
        public RelayCommand DeleteAllTracksCommand { get; }
        public RelayCommand<IList> DeleteSelectedTracksCommand { get; }
        public RelayCommand<IList> UseParametersCommand { get; }
        public RelayCommand AddSymbolCommand { get; }
        public RelayCommand<IList> DeleteSymbolsCommand { get; }
        public RelayCommand ActiveCommand { get; }
        public RelayCommand ImportSymbolsCommand { get; }
        public RelayCommand<IList> ExportSymbolsCommand { get; }
        public RelayCommand<TrackViewModel> TrackDoubleClickCommand { get; }
        public RelayCommand<IList> MoveUpSymbolsCommand { get; }
        public RelayCommand<IList> MoveDownSymbolsCommand { get; }
        public RelayCommand SortSymbolsCommand { get; }

        public StrategyModel Model { get; }
        public SyncObservableCollection<SymbolViewModel> Symbols { get; } = new SyncObservableCollection<SymbolViewModel>();
        public SyncObservableCollection<ParameterViewModel> Parameters { get; } = new SyncObservableCollection<ParameterViewModel>();
        public SyncObservableCollection<TrackViewModel> Tracks { get; } = new SyncObservableCollection<TrackViewModel>();

        public string DisplayName
        {
            get => _displayName;
            set => Set(ref _displayName, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                Set(ref _isSelected, value);
            }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set => Set(ref _isExpanded, value);
        }

        public ObservableCollection<DataGridColumn> TrackColumns
        {
            get => _trackColumns;
            set => Set(ref _trackColumns, value);
        }

        public SymbolViewModel SelectedSymbol
        {
            get => _selectedSymbol;
            set
            {
                Set(ref _selectedSymbol, value);
                DeleteSymbolsCommand.RaiseCanExecuteChanged();
                ExportSymbolsCommand.RaiseCanExecuteChanged();
                MoveUpSymbolsCommand.RaiseCanExecuteChanged();
                MoveDownSymbolsCommand.RaiseCanExecuteChanged();
            }
        }

        public TrackViewModel SelectedTrack
        {
            get => _selectedTrack;
            set
            {
                Set(ref _selectedTrack, value);
            }
        }

        public void Refresh()
        {
            Model.Refresh();
        }

        internal static void AddPath(string path)
        {
            string pathValue = Environment.GetEnvironmentVariable("PATH");
            if (pathValue.Contains(path))
                return;

            pathValue += ";" + path;
            Environment.SetEnvironmentVariable("PATH", pathValue);
        }

        internal void UseParameters(TrackViewModel track)
        {
            if (track == null)
                return;

            Parameters.Clear();
            foreach (ParameterViewModel parameter in track.Parameters)
            {
                Parameters.Add(parameter);
            }
        }

        internal void DataToModel()
        {
            Model.Symbols.Clear();
            foreach (SymbolViewModel symbol in Symbols)
            {
                Model.Symbols.Add(symbol.Model);
            }

            Model.Parameters.Clear();
            foreach (ParameterViewModel parameter in Parameters)
            {
                Model.Parameters.Add(parameter.Model);
            }

            Model.Tracks.Clear();
            foreach (TrackViewModel track in Tracks)
            {
                Model.Tracks.Add(track.Model);
                track.DataToModel();
            }
        }

        internal bool DeleteTrack(TrackViewModel track)
        {
            bool ok = Tracks.Remove(track);
            return ok;
        }

        private void DoDeleteAllTracks()
        {
            Tracks.Clear();
            DataToModel();
        }

        private void DoDeleteTracks(IList tracks)
        {
            Debug.Assert(tracks != null);
            if (Tracks.Count == 0 || tracks.Count == 0)
                return;

            List<TrackViewModel> list = tracks.Cast<TrackViewModel>()?.ToList();
            foreach (TrackViewModel track in list)
            {
                if (track != null)
                {
                    track.DoDeleteTrack();
                }
            }
        }

        private void DoSelectItem(TrackViewModel track)
        {
            if (track == null)
                return;

            track.IsSelected = true;
            _parent.SelectedItem = track;
            IsExpanded = true;
        }

        private void DoAddSymbol()
        {
            var symbol = new SymbolViewModel(this, new SymbolModel());
            Symbols.Add(symbol);
        }

        private void DoUseParameters(IList selected)
        {
            if (selected == null)
                return;

            foreach (TrackViewModel track in selected)
            {
                UseParameters(track);
                break; // skip rest
            }
        }

        private async void DoRunStrategy()
        {
            DataToModel();

            int count = 0;
            var models = GridOptimizerModels(Model, 0);
            int total = models.Count;
            var tasks = new List<Task>();
            using (var throttler = new SemaphoreSlim(_settingsModel.MaxBacktests))
            {
                foreach (StrategyModel model in models)
                {
                    await throttler.WaitAsync();
                    count++;
                    var trackModel = new TrackModel(model.AlgorithmName, model);
                    Log.Trace($"Strategy {trackModel.AlgorithmName} {trackModel.Name} {count}({total})");
                    var track = new TrackViewModel(this, trackModel, _settingsModel);
                    Tracks.Add(track);
                    Task task = track
                        .StartTaskAsync()
                        .ContinueWith(m =>
                        {
                            FilterDataGridColumns.AddPropertyColumns(TrackColumns, track.Statistics, "Statistics");
                            throttler.Release();
                        },
                        TaskScheduler.FromCurrentSynchronizationContext());
                    tasks.Add(task);
                }

                await Task.WhenAll(tasks);
            }
        }

        private List<StrategyModel> GridOptimizerModels(StrategyModel rawModel, int index)
        {
            var model = new StrategyModel(rawModel);

            var list = new List<StrategyModel>();
            if (index < model.Parameters.Count)
            {
                var parameter = model.Parameters[index];
                if (parameter.UseRange)
                {
                    foreach (string value in SplitRange(parameter.Range))
                    {
                        parameter.Value = value;
                        parameter.UseValue = true;
                        list.AddRange(GridOptimizerModels(model, index + 1));
                    }
                }
                else
                {
                    list.AddRange(GridOptimizerModels(model, index + 1));
                }
            }
            else
            {
                list.Add(model);
            }

            return list;
        }

        private IEnumerable<string> SplitRange(string range)
        {
            foreach (string value in range.Split(','))
            {
                yield return value;
            }
        }

        private void DataFromModel()
        {
            Symbols.Clear();
            foreach (SymbolModel symbolModel in Model.Symbols)
            {
                var symbolViewModel = new SymbolViewModel(this, symbolModel);
                Symbols.Add(symbolViewModel);
            }

            AlgorithmNameChanged(Model.AlgorithmName);

            UpdateTracksAndColumns();
        }

        private void UpdateTracksAndColumns()
        {
            TrackColumns.Clear();
            Tracks.Clear();

            TrackColumns.Add(new DataGridCheckBoxColumn()
            {
                Header = "Active",
                Binding = new Binding("Active") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }
            });

            FilterDataGridColumns.AddTextColumn(TrackColumns, "Name", "Model.Name", false);
            foreach (TrackModel TrackModel in Model.Tracks)
            {
                var TrackViewModel = new TrackViewModel(this, TrackModel, _settingsModel);
                Tracks.Add(TrackViewModel);
                FilterDataGridColumns.AddPropertyColumns(TrackColumns, TrackViewModel.Statistics, "Statistics");
            }
        }

        private void StrategyNameChanged()
        {
            if (!string.IsNullOrWhiteSpace(Model.Name))
            {
                DisplayName = Model.Name;
            }
            else if (!string.IsNullOrWhiteSpace(Model.AlgorithmName))
            {
                DisplayName = Model.AlgorithmName;
            }
            else
            {
                DisplayName = DefaultName;
            }
        }

        private void AlgorithmNameChanged(string algorithmName)
        {
            CloneAlgorithmCommand.RaiseCanExecuteChanged();

            StrategyNameChanged();

            if (string.IsNullOrEmpty(Model.AlgorithmLocation) || string.IsNullOrEmpty(algorithmName))
                return;

            Assembly assembly = Assembly.LoadFrom(Model.AlgorithmLocation);
            if (assembly == null)
                return;

            IEnumerable<Type> type = assembly.GetTypes().Where(m => m.Name.Equals(algorithmName));
            if (type == null || type.Count() == 0)
                return;

            Parameters.Clear();
            foreach (KeyValuePair<string, string> parameter in ParameterAttribute.GetParametersFromType(type.First()))
            {
                string parameterName = parameter.Key;
                string parameterType = parameter.Value;

                if (_exclude.Contains(parameterName))
                    continue;

                ParameterModel parameterModel = Model.Parameters.FirstOrDefault(m => m.Name.Equals(parameterName));
                if (parameterModel == null)
                {
                    parameterModel = new ParameterModel() { Name = parameterName };
                }

                var parameterViewModel = new ParameterViewModel(this, parameterModel);
                Parameters.Add(parameterViewModel);
            }

            RaisePropertyChanged(() => Parameters);
        }

        private void DoDeleteSymbols(IList symbols)
        {
            Debug.Assert(symbols != null);
            if (Symbols.Count == 0 || symbols.Count == 0)
                return;

            // Create a copy of the list before remove
            List<SymbolViewModel> list = symbols.Cast<SymbolViewModel>()?.ToList();
            Debug.Assert(list != null);

            int pos = Symbols.IndexOf(list.First());
            foreach (SymbolViewModel symbol in list)
            {
                Symbols.Remove(symbol);
            }

            DataToModel();
            if (Symbols.Count > 0)
            {
                SelectedSymbol = Symbols[Math.Min(pos, Symbols.Count - 1)];
            }
        }

        private void DoImportSymbols()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                InitialDirectory = Directory.GetCurrentDirectory(),
                Multiselect = false,
                Filter = "symbol file (*.csv)|*.csv|All files (*.*)|*.*"
            };
            if (openFileDialog.ShowDialog() == false)
                return;

            try
            {
                foreach (string fileName in openFileDialog.FileNames)
                {
                    using (StreamReader r = new StreamReader(fileName))
                    {
                        while (!r.EndOfStream)
                        {
                            string line = r.ReadLine();
                            foreach (string name in line.Split(',').Where(m => !string.IsNullOrWhiteSpace(m)))
                            {
                                if (!Model.Symbols.Exists(m => m.Name.Equals(name)))
                                {
                                    var symbol = new SymbolModel() { Name = name };
                                    Model.Symbols.Add(symbol);
                                }
                            }
                        }
                    }
                }

                DataFromModel();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, ex.GetType().ToString());
            }
        }

        private void DoCloneStrategy()
        {
            try
            {
                _parent.IsBusy = true;
                DataToModel();
                var strategyModel = new StrategyModel(Model);
                var strategy = new StrategyViewModel(_parent, strategyModel, _settingsModel);
                _parent.Strategies.Add(strategy);
            }
            finally
            {
                _parent.IsBusy = false;
            }
        }

        private void DoCloneAlgorithm()
        {
            try
            {
                _parent.IsBusy = true;
                DataToModel();

                // Load assemblies of algorithms
                string assemblyPath = Model.AlgorithmLocation;
                Debug.Assert(!string.IsNullOrEmpty(assemblyPath));
                Assembly assembly = Assembly.LoadFrom(assemblyPath);

                //Get the list of extention classes in the library: 
                List<string> extended = Loader.GetExtendedTypeNames(assembly);
                List<string> list = assembly.ExportedTypes
                    .Where(m => extended.Contains(m.FullName))
                    .Select(m => m.Name)
                    .ToList();
                list.Sort();

                // Iterate and clone strategies
                foreach (string algorithm in list)
                {
                    if (algorithm.Equals(Model.AlgorithmName))
                        continue; // Skip this algorithm

                    var strategyModel = new StrategyModel(Model)
                    {
                        AlgorithmName = algorithm,
                        Name = null
                    };
                    var strategy = new StrategyViewModel(_parent, strategyModel, _settingsModel);
                    _parent.Strategies.Add(strategy);
                }
            }
            finally
            {
                _parent.IsBusy = false;
            }
        }

        private void DoExportStrategy()
        {
            DataToModel();
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                InitialDirectory = Directory.GetCurrentDirectory(),
                FileName = Model.Name,
                Filter = "json file (*.json)|*.json|All files (*.*)|*.*"
            };
            if (saveFileDialog.ShowDialog() == false)
                return;

            try
            {
                _parent.IsBusy = true;
                string fileName = saveFileDialog.FileName;
                using (StreamWriter file = File.CreateText(fileName))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    var strategies = new StrategiesModel();
                    strategies.Strategies.Add(Model);
                    serializer.Serialize(file, strategies);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, ex.GetType().ToString());
            }
            finally
            {
                _parent.IsBusy = false;
            }
        }

        private void DoExportSymbols(IList symbols)
        {
            Debug.Assert(symbols != null);
            if (Symbols.Count == 0 || symbols.Count == 0)
                return;

            DataToModel();
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                InitialDirectory = Directory.GetCurrentDirectory(),
                Filter = "symbol file (*.csv)|*.csv|All files (*.*)|*.*"
            };
            if (saveFileDialog.ShowDialog() == false)
                return;

            try
            {
                string fileName = saveFileDialog.FileName;
                using (StreamWriter file = File.CreateText(fileName))
                {
                    foreach (SymbolViewModel symbol in symbols)
                    {
                        file.WriteLine(symbol.Model.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, ex.GetType().ToString());
            }
        }

        private void OnMoveUpSymbols(IList symbols)
        {
            if (Symbols.Count <= 1)
                return;

            // Create a copy of the list before move
            List<SymbolViewModel> list = symbols.Cast<SymbolViewModel>()?.ToList();
            Debug.Assert(list != null);

            for (int i = 1; i < Symbols.Count; i++)
            {
                if (list.Contains(Symbols[i]) && !list.Contains(Symbols[i - 1]))
                {
                    Symbols.Move(i, i - 1);
                }
            }
        }

        private void OnMoveDownSymbols(IList symbols)
        {
            if (Symbols.Count <= 1)
                return;

            // Create a copy of the list before move
            List<SymbolViewModel> list = symbols.Cast<SymbolViewModel>()?.ToList();
            Debug.Assert(list != null);

            for (int i = Symbols.Count - 2; i >= 0; i--)
            {
                if (list.Contains(Symbols[i]) && !list.Contains(Symbols[i + 1]))
                {
                    Symbols.Move(i, i + 1);
                }
            }
        }
    }
}
