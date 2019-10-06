﻿/*
 * Copyright 2019 Capnode AB
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
using Algoloop.Properties;
using Algoloop.ViewSupport;
using Capnode.Wpf.DataGrid;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Algoloop.ViewModel
{
    public class FolderViewModel : ViewModelBase, ITreeViewModel
    {
        private MarketViewModel _market;
        private SymbolViewModel _selectedSymbol;
        private SymbolViewModel _marketSymbol;
        private ObservableCollection<DataGridColumn> _symbolColumns = new ObservableCollection<DataGridColumn>();
        private IList _selectedItems;

        public FolderViewModel(MarketViewModel market, FolderModel model)
        {
            if (market == null) throw new ArgumentNullException(nameof(market));

            _market = market;
            Model = model;

            DeleteCommand = new RelayCommand(() => _market?.DeleteFolder(this), () => !_market.Active);
            StartCommand = new RelayCommand(() => { }, () => false);
            StopCommand = new RelayCommand(() => { }, () => false);
            AddSymbolCommand = new RelayCommand<SymbolViewModel>(m => DoAddSymbol(m), m => !_market.Active && MarketSymbols.View.Cast<object>().FirstOrDefault() != null);
            RemoveSymbolsCommand = new RelayCommand<IList>(m => DoRemoveSymbols(m), m => !_market.Active && SelectedSymbol != null);
            ExportFolderCommand = new RelayCommand(() => DoExportFolder(), () => !_market.Active);

            DataFromModel();

            MarketSymbols.Filter += (object sender, FilterEventArgs e) =>
            {
                if (e.Item is SymbolViewModel marketSymbol)
                {
                    e.Accepted = marketSymbol.Active && !Symbols.Any(m => m.Model.Name.Equals(marketSymbol.Model.Name, StringComparison.OrdinalIgnoreCase));
                }
            };

            MarketSymbols.Source = _market.Symbols;
        }

        public RelayCommand DeleteCommand { get; }
        public RelayCommand StartCommand { get; }
        public RelayCommand StopCommand { get; }
        public RelayCommand<SymbolViewModel> AddSymbolCommand { get; }
        public RelayCommand<IList> RemoveSymbolsCommand { get; }
        public RelayCommand ExportFolderCommand { get; }


        public FolderModel Model { get; }
        public SyncObservableCollection<SymbolViewModel> Symbols { get; } = new SyncObservableCollection<SymbolViewModel>();
        public CollectionViewSource MarketSymbols { get; } = new CollectionViewSource();


        public SymbolViewModel MarketSymbol
        {
            get => _marketSymbol;
            set => Set(ref _marketSymbol, value);
        }

        public IList SelectedItems
        {
            get { return _selectedItems; }
            set
            {
                _selectedItems = value;
                string message = string.Empty;
                if (_selectedItems?.Count > 0)
                {
                    message = string.Format(Resources.SelectedCount, _selectedItems.Count);
                }

                Messenger.Default.Send(new NotificationMessage(message));
            }
        }

        public SymbolViewModel SelectedSymbol
        {
            get => _selectedSymbol;
            set
            {
                Set(ref _selectedSymbol, value);
                RemoveSymbolsCommand.RaiseCanExecuteChanged();
            }
        }

        public ObservableCollection<DataGridColumn> SymbolColumns
        {
            get => _symbolColumns;
            set => Set(ref _symbolColumns, value);
        }


        public void Refresh()
        {
            Model.Refresh();
            DataFromModel();
            MarketSymbols.View.Refresh();
            AddSymbolCommand.RaiseCanExecuteChanged();
        }

        public void AddSymbols(IEnumerable<SymbolViewModel> symbols)
        {
            if (symbols == null) throw new ArgumentNullException(nameof(symbols));

            foreach (SymbolViewModel symbol in symbols)
            {
                if (!Symbols.Contains(symbol))
                {
                    Symbols.Add(symbol);
                }
            }

            DataToModel();
            MarketSymbols.View.Refresh();
            AddSymbolCommand.RaiseCanExecuteChanged();
        }

        private void DoAddSymbol(SymbolViewModel symbol)
        {
            if (symbol == null)
                return;

            AddSymbols(new List<SymbolViewModel> { symbol });
        }

        internal void DataToModel()
        {
            Model.Symbols.Clear();
            foreach (SymbolViewModel symbol in Symbols)
            {
                Model.Symbols.Add(symbol.Model.Name);
            }
        }

        internal void DataFromModel()
        {
            SymbolColumns.Clear();
            ExDataGridColumns.AddTextColumn(SymbolColumns, "Symbol", "Model.Name", false);

            Symbols.Clear();
            foreach (SymbolViewModel marketSymbol in _market.Symbols)
            {
                if (marketSymbol.Active)
                {
                    string symbol = Model.Symbols.Find(m => m.Equals(marketSymbol.Model.Name, StringComparison.OrdinalIgnoreCase));
                    if (symbol != null)
                    {
                        Symbols.Add(marketSymbol);
                    }

                    ExDataGridColumns.AddPropertyColumns(SymbolColumns, marketSymbol.Model.Properties, "Model.Properties");
                }
            }
        }

        private void DoRemoveSymbols(IList symbols)
        {
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

            MarketSymbols.View.Refresh();
            AddSymbolCommand.RaiseCanExecuteChanged();
        }

        private void DoExportFolder()
        {
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
                    foreach (SymbolViewModel symbol in Symbols)
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
    }
}
