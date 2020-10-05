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
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Algoloop.ViewModel.Tests
{
    [TestClass()]
    public class StrategyViewModelTests
    {
        private StrategyViewModel _strategy;
        private StrategiesViewModel _strategies;

        [TestInitialize()]
        public void Initialize()
        {
            _strategies = new StrategiesViewModel(new StrategiesModel(), null, null, null);
            _strategy = new StrategyViewModel(_strategies, new StrategyModel { Name = "A" }, null, null, null);
            _strategies.AddStrategy(_strategy);
        }

        [TestMethod()]
        public void AddSymbolCommandTest()
        {
            Assert.IsTrue(_strategy.AddSymbolCommand.CanExecute(null));
            Assert.AreEqual(0, _strategy.Symbols.Count);
            _strategy.AddSymbolCommand.Execute(null);
            Assert.AreEqual(1, _strategy.Symbols.Count);
        }

        [TestMethod()]
        public void MoveStrategyCommandTest_child_to_track_failed()
        {
            var drag = new StrategyViewModel(_strategy, new StrategyModel { Name = "drag" }, null, null, null);
            _strategy.AddStrategy(drag);
            var drop = new TrackViewModel(_strategy, new TrackModel { Name = "drop" }, null, null);
            _strategy.Tracks.Add(drop);
            Assert.AreEqual(1, _strategies.Strategies.Count);
            Assert.AreEqual(1, _strategy.Strategies.Count);
            Assert.AreEqual(1, _strategy.Tracks.Count);
            Assert.AreEqual(0, drag.Strategies.Count);

            Assert.IsTrue(drag.MoveStrategyCommand.CanExecute(null));
            drag.MoveStrategyCommand.Execute(drop);
            Assert.AreEqual(1, _strategies.Strategies.Count);
            Assert.AreEqual(1, _strategy.Strategies.Count);
            Assert.AreEqual(1, _strategy.Tracks.Count);
            Assert.AreEqual(0, drag.Strategies.Count);
        }

        [TestMethod()]
        public void MoveStrategyCommandTest_top_to_child()
        {
            StrategyViewModel drag = _strategy;
            var drop = new StrategyViewModel(_strategies, new StrategyModel { Name = "drop" }, null, null, null);
            _strategies.AddStrategy(drop);
            Assert.AreEqual(2, _strategies.Strategies.Count);
            Assert.AreEqual(0, drag.Strategies.Count);
            Assert.AreEqual(0, drop.Strategies.Count);

            Assert.IsTrue(drag.MoveStrategyCommand.CanExecute(null));
            drag.MoveStrategyCommand.Execute(drop);
            Assert.AreEqual(1, _strategies.Strategies.Count);
            Assert.AreEqual(0, drag.Strategies.Count);
            Assert.AreEqual(1, drop.Strategies.Count);
            Assert.AreSame(drop, drag._parent);
        }

        [TestMethod()]
        public void MoveStrategyCommandTest_child_to_child()
        {
            var drag = new StrategyViewModel(_strategy, new StrategyModel { Name = "drag" }, null, null, null);
            _strategy.AddStrategy(drag);
            var drop = new StrategyViewModel(_strategies, new StrategyModel { Name = "drop" }, null, null, null);
            _strategies.AddStrategy(drop);
            Assert.AreEqual(2, _strategies.Strategies.Count);
            Assert.AreEqual(1, _strategy.Strategies.Count);
            Assert.AreEqual(0, drag.Strategies.Count);
            Assert.AreEqual(0, drop.Strategies.Count);

            Assert.IsTrue(drag.MoveStrategyCommand.CanExecute(null));
            drag.MoveStrategyCommand.Execute(drop);
            Assert.AreEqual(2, _strategies.Strategies.Count);
            Assert.AreEqual(0, _strategy.Strategies.Count);
            Assert.AreEqual(0, drag.Strategies.Count);
            Assert.AreEqual(1, drop.Strategies.Count);
            Assert.AreSame(drop, drag._parent);
        }

        [TestMethod()]
        public void MoveStrategyCommandTest_child_to_top()
        {
            StrategiesViewModel drop = _strategies;
            var drag = new StrategyViewModel(_strategy, new StrategyModel { Name = "drag" }, null, null, null);
            _strategy.AddStrategy(drag);
            Assert.AreEqual(1, _strategy.Strategies.Count);
            Assert.AreEqual(0, drag.Strategies.Count);
            Assert.AreEqual(1, drop.Strategies.Count);

            Assert.IsTrue(drag.MoveStrategyCommand.CanExecute(null));
            drag.MoveStrategyCommand.Execute(drop);
            Assert.AreEqual(0, _strategy.Strategies.Count);
            Assert.AreEqual(0, drag.Strategies.Count);
            Assert.AreEqual(2, drop.Strategies.Count);
            Assert.AreSame(drop, drag._parent);
        }
    }
}