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
using System.Collections.Specialized;
using System.Windows.Forms;

namespace XTMF.Gui.Models
{
    internal sealed class ModelSystemStructureDisplayModel : INotifyPropertyChanged
    {
        internal ModelSystemStructureModel BaseModel;
        private ObservableCollection<ModelSystemStructureModel> BaseChildren;

        public event PropertyChangedEventHandler PropertyChanged;

        private static Color AddingYellow;
        private static Color OptionalGreen;
        private static Color WarningRed;

        static ModelSystemStructureDisplayModel()
        {
            AddingYellow = (Color)App.Current.FindResource("AddingYellow");
            WarningRed = (Color)App.Current.FindResource("WarningRed");
            OptionalGreen = Color.FromRgb(50, 140, 50);
        }

        public ModelSystemStructureDisplayModel(ModelSystemStructureModel baseModel)
        {
            BaseModel = baseModel;
            BaseChildren = baseModel.Children;
            Children = BaseChildren == null ? new ObservableCollection<ModelSystemStructureDisplayModel>()
                : new ObservableCollection<ModelSystemStructureDisplayModel>(
                from child in baseModel.Children
                select new ModelSystemStructureDisplayModel(child));
            BaseModel.PropertyChanged += BaseModel_PropertyChanged;
            if(BaseChildren != null)
            {
                BaseChildren.CollectionChanged += BaseChildren_CollectionChanged;
            }
        }

        private void BaseChildren_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch(e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    {
                        var insertAt = e.NewStartingIndex;
                        foreach(var item in e.NewItems)
                        {
                            Children.Insert(insertAt, new ModelSystemStructureDisplayModel(item as ModelSystemStructureModel));
                            insertAt++;
                        }
                    }
                    break;
                case NotifyCollectionChangedAction.Move:
                    {
                        Children.Move(e.OldStartingIndex, e.NewStartingIndex);
                    }
                    break;
                case NotifyCollectionChangedAction.Remove:
                    {
                        if(Children.Count > 0)
                        {
                            Children.RemoveAt(e.OldStartingIndex);
                        }
                    }
                    break;
                case NotifyCollectionChangedAction.Replace:
                    {
                        Children[e.OldStartingIndex] = new ModelSystemStructureDisplayModel(e.NewItems[0] as ModelSystemStructureModel);
                    }
                    break;
                case NotifyCollectionChangedAction.Reset:
                    {
                        Children.Clear();
                    }
                    break;
                default:
                    {
                        throw new NotImplementedException("An unknown action was performed!");
                    }
            }
            ModelHelper.PropertyChanged(PropertyChanged, this, "Children");
        }

        private void BaseModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var ev = PropertyChanged;
            if(ev != null)
            {
                if(e.PropertyName == "Children")
                {
                    if(BaseChildren != null)
                    {
                        BaseChildren.CollectionChanged -= BaseChildren_CollectionChanged;
                    }
                    BaseChildren = BaseModel.Children;
                    if(BaseChildren != null)
                    {
                        BaseChildren.CollectionChanged += BaseChildren_CollectionChanged;
                    }
                }
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
                    return AddingYellow;
                }
                if(BaseModel.Type == null)
                {
                    return (BaseModel.IsOptional) ? OptionalGreen : WarningRed;
                }
                return Color.FromRgb(0x30, 0x30, 0x30);
            }
        }

        public ObservableCollection<ModelSystemStructureDisplayModel> Children { get; private set; }
        public Type Type
        {
            get
            {
                return this.BaseModel.Type;
            }
        }

        internal ObservableCollection<ParameterModel> GetParameters()
        {
            return BaseModel.Parameters.GetParameters();
        }

        internal void CopyModule()
        {
            Clipboard.SetText(BaseModel.CopyModule());
        }

        internal bool Paste(string toPaste, ref string error)
        {
            return BaseModel.Paste(toPaste, ref error);
        }
    }
}
