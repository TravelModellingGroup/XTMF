/*
    Copyright 2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XTMF.Gui.UserControls.Help
{
    /// <summary>
    /// This class is designed to build a layer of abstraction between
    /// the target of documentation, and the view model.
    /// </summary>
    public class ContentReference
    {
        private UIElement _Content;

        public UIElement Content
        {
            get
            {
                if(_Content == null)
                {
                    _Content = GenerateContent();
                }
                return _Content;
            }
        }

        /// <summary>
        /// The text value that will be displayed
        /// </summary>
        private string Name;

        private Type Module;

        /// <summary>
        /// Create a reference to a module
        /// </summary>E
        /// <param name="module">The module to create a reference to.</param>
        public ContentReference(string name, Type module)
        {
            Name = name;
            Module = module;
        }

        private UIElement GenerateContent()
        {
            return new TextBlock() { Text = Module == null ? "Unknown module" : Module.Name, Foreground = Brushes.White };
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
