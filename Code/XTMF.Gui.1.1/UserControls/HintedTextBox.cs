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

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace XTMF.Gui.UserControls
{
    public class HintedTextBox : TextBox
    {
        private string hintText;
        private FormattedText HintTextImage;
        private static ContextMenu _LocalContextMenu;

        static HintedTextBox()
        {

           

            var contextMenu = new ContextMenu();
            // copy
            contextMenu.Items.Add(new MenuItem
            {
                Header = "Copy",
                Command = ApplicationCommands.Copy,
                
            });
            // cut
            contextMenu.Items.Add(new MenuItem
            {
                Header = "Cut",
                Command = ApplicationCommands.Cut,
               
            });
            // paste
            contextMenu.Items.Add(new MenuItem
            {
                Header = "Paste",
                Command = ApplicationCommands.Paste,
                
            });
            _LocalContextMenu = contextMenu;
        }

        public HintedTextBox()
        {
            ClipToBounds = true;
            ContextMenu = _LocalContextMenu;
            Padding = new Thickness(5, 5, 5, 5);
        }

        public string HintText
        {
            get
            {
                return hintText;
            }

            set
            {
                hintText = value;
                if (!String.IsNullOrEmpty(hintText))
                {
                    Background = Brushes.Transparent;
                }
                RebuildFont();
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            if (ClipToBounds)
            {
                dc.PushClip(new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight)));
            }
            dc.DrawRectangle(Brushes.White, new Pen(), new Rect(0, 0, ActualWidth, ActualHeight));
            base.OnRender(dc);
            if (Text == String.Empty && HintTextImage != null)
            {
                dc.DrawText(HintTextImage, new Point(4, (ActualHeight - HintTextImage.Height) / 2));
            }
            if (ClipToBounds)
            {
                // pop the clip that we added
                dc.Pop();
            }
        }

        protected override void OnTextChanged(TextChangedEventArgs e)
        {
            base.OnTextChanged(e);
            InvalidateVisual();
        }

        private void RebuildFont()
        {
            if (hintText == null)
            {
                HintTextImage = null;
            }
            else
            {
                HintTextImage = new FormattedText(hintText, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface(FontFamily, FontStyle, FontWeight, FontStretch), FontSize, Brushes.Gray);
                int constantBorder = 8;
                if (ActualWidth > 0)
                {
                    if (HintTextImage.Width + constantBorder > ActualWidth)
                    {
                        Width = HintTextImage.Width + constantBorder;
                    }
                }
            }
            InvalidateVisual();
        }
    }
}