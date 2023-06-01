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
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedTypeParameter

namespace XTMF.Testing.Modules.Editing
{

    public interface IGenericInterface<A,B,C,D> : IModule
    {

    }

    public abstract class NonGenericBase<E,F,G> : IGenericInterface<float, E, F, G>
    {
        public string Name { get; set; } = string.Empty;

        public float Progress { get; } = 0f;

        public Tuple<byte, byte, byte> ProgressColour => new Tuple<byte, byte, byte>(50, 150, 50);

        public bool RuntimeValidation(ref string? error)
        {
            return true;
        }
    }

    public class TestGenericModule<H, I> : NonGenericBase<float, H, I>
    {
        /// <summary>
        /// Actually have a data field in order to ensure the T matters
        /// </summary>
        // ReSharper disable once UnusedMember.Global
        public I? Data;
    }
}
