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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Media;

namespace XTMF.Gui.Models
{
    internal class ModelSystemStructureDisplayModel : INotifyPropertyChanged
    {
        internal ModelSystemStructureModel BaseModel;

        public event PropertyChangedEventHandler PropertyChanged;

        public ModelSystemStructureDisplayModel(ModelSystemStructureModel baseModel)
        {
            BaseModel = baseModel;
            Children = baseModel.Children == null ? null 
                : new ObservableCollection <ModelSystemStructureDisplayModel>(
                from child in baseModel.Children
                select new ModelSystemStructureDisplayModel(child));
            BaseModel.PropertyChanged += BaseModel_PropertyChanged;
        }

        private void BaseModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var ev = PropertyChanged;
            if(ev != null)
            {
                ModelHelper.PropertyChanged(ev, this, e.PropertyName);
                // If the type changes it is possible that our background colour should change as well
                if(e.PropertyName == "Type")
                {
                    ModelHelper.PropertyChanged(ev, this, "BackgroundColour");
                }
            }
        }

        public string Name { get { return BaseModel.Name; } }

        public string Description { get { return BaseModel.Description; } }

        public Color BackgroundColour
        {
            get
            {
                if(BaseModel.IsCollection)
                {
                    return Color.FromRgb(140, 140, 0);
                }
                if(BaseModel.Type == null)
                {
                    return (BaseModel.IsOptional) ? Color.FromRgb(0, 140, 0) : Color.FromRgb(140, 0, 0);
                }
                return Color.FromRgb(0x30, 0x30, 0x30);
            }
        }

        public ObservableCollection<ModelSystemStructureDisplayModel> Children { get; private set; }
    }
}
