/*
    Copyright 2014-2018 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace XTMF.Gui.Models;

public class ViewModelBase : INotifyPropertyChanged
{
    public string ViewTitle
    {
        get => _viewTitle;
        set
        {
            _viewTitle = value;
            OnPropertyChanged(nameof(ViewTitle));
        }
    }

    public ContentControl ContentPresenter { get; set; }

    private UserControl _viewModelControl;

    public bool IsSearchBoxVisible
    {
        get => _isSearchBarVisible;
        set
        {
            _isSearchBarVisible = value;
            OnPropertyChanged(nameof(IsSearchBoxVisible));
        }
    }

    private string _viewTitle;

    private bool _isSearchBarVisible = false;

    public UserControl ViewModelControl
    {
        get => _viewModelControl;
        set
        {
            _viewModelControl = value;
            OnPropertyChanged(nameof(ViewModelControl));
        }
    }
    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public Visibility SearchBoxVisiblity
    {
        get
        {
            if (IsSearchBoxVisible)
            {
                return Visibility.Visible;
            }
            else
            {
                return Visibility.Collapsed;
            }
        }

    }

   
}
