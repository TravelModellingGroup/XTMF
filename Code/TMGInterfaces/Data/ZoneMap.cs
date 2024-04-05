/*
    Copyright 2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System;
using System.Collections.Generic;
using System.Linq;

namespace TMG.Data
{
    /// <summary>
    /// This class is designed to help map the zone system by a given mapping
    /// </summary>
    public sealed class ZoneMap
    {
        /// <summary>
        /// This provides a mapping for each zone to what new category it fits into
        /// </summary>
        public int[] Map { get; private set; }

        /// <summary>
        /// This provides a mapping for each new category to all of the contained zones
        /// </summary>
        public IReadOnlyDictionary<int, IReadOnlyList<int>> KeyToZoneIndex { get; private set; }


        private IReadOnlyList<int> _MapValues;
        /// <summary>
        /// The set of different values provided to map to.
        /// </summary>
        public IReadOnlyList<int> MapValues
        {
            get
            {
                // in order to save some memory and time we are only going to generate the different values in the map once
                var value = _MapValues;
                if (value == null)
                {
                    // make sure to sort the keys to help readability
                    value = _MapValues = KeyToZoneIndex.Keys.OrderBy(x => x).ToList();
                }
                return value;
            }
        }


        private ZoneMap(int[] map)
        {
            // this mapping is the easiest one as it is provided for us from the loader
            Map = map;
            // next we need to make the mapping to zone index lookup
            var initialMap = new Dictionary<int, List<int>>();

            for (int i = 0; i < map.Length; i++)
            {
                if (!initialMap.TryGetValue(map[i], out List<int> mapList))
                {
                    mapList = [];
                    initialMap.Add(map[i], mapList);
                }
                mapList.Add(i);
            }
            KeyToZoneIndex = initialMap.Keys.ToDictionary(key => key, key => (IReadOnlyList<int>)initialMap[key]);
        }

        // ReSharper disable once UnusedParameter.Local
        private static void ThrowIfNull<T>(T parameter, string name) where T : class
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(name, "A parameter with a required non-null field was null!");
            }
        }

        public static ZoneMap CreateZoneMap(IZone[] zones, int[] map)
        {
            ThrowIfNull(zones, nameof(zones));
            ThrowIfNull(map, nameof(map));
            if (zones.Length != map.Length)
            {
                throw new ArgumentException("The size of the mapping is not equal to the number of zones!");
            }
            // clone the current mapping to make sure that it is not going to be recycled 
            var ret = new ZoneMap(map.ToArray());

            return ret;
        }


    }
}
