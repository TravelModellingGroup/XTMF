/*
    Copyright 2014 Travel Modelling Group, Department of Civil Engineering, University of Toronto

    This file is part of XTMF.

    XTMF is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    XTMF is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with XTMF.  If not, see <http://www.gnu.org/licenses/>.
*/
using System.Collections.Concurrent;

namespace XTMF
{
    /// <summary>
    /// Loads the data for the given type T
    /// </summary>
    public interface IDataLoader<T> : IProducerConsumerCollection<T>, IModule
    {
        /// <summary>
        /// If there is no more data to load, this will
        /// be set to true.
        /// </summary>
        bool OutOfData { get; }

        /// <summary>
        /// Load the data for this data source
        /// </summary>
        void LoadData();

        /// <summary>
        /// This is called when we want to load the data
        /// from the beginning again
        /// </summary>
        void Reset();
    }
}