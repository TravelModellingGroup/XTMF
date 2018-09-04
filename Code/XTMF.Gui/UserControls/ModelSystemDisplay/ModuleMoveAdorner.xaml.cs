using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for ModuleMoveAdorner.xaml
    /// </summary>
    public partial class ModuleMoveAdorner 
    {

        public Border Border { get; set; }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="adornedElement"></param>
        public ModuleMoveAdorner(UIElement adornedElement) :
            base(adornedElement)
        {

            Border = new Border();
           
            Border.Width = adornedElement.RenderSize.Width;
            Border.Margin = new Thickness(0, -5,0,0);
            Border.BorderBrush = (Brush)Application.Current.TryFindResource("SecondaryAccentBrush");
            Border.BorderThickness = new Thickness(0,5,0,3);
            Border.Height = 3;


            AddVisualChild(Border);


            Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        protected override Visual GetVisualChild(int index)
        {
            if (index != 0)
            {
                throw new ArgumentOutOfRangeException();
            }
            return Border;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="constraint"></param>
        /// <returns></returns>
        protected override Size MeasureOverride(Size constraint)
        {
            Border.Measure(constraint);
            return Border.DesiredSize;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="finalSize"></param>
        /// <returns></returns>
        protected override Size ArrangeOverride(Size finalSize)
        {
            Border.Arrange(new Rect(new Point(0, 0), finalSize));
            return new Size(Border.ActualWidth, Border.ActualHeight);
        }

        /// <summary>
        /// 
        /// </summary>
        protected override int VisualChildrenCount => 1;
    }
}
