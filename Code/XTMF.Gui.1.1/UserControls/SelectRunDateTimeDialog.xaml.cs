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
using MaterialDesignThemes.Wpf;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for SelectRunDateTimeDialog.xaml
    /// </summary>
    public partial class SelectRunDateTimeDialog : UserControl


    {
        private DialogSession _dialogSession;


        public bool DidComplete { get; set; }

        public SelectRunDateTimeDialog()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task<object> ShowAsync()
        {
            return await DialogHost.Show(this, "RootDialog", OpenedEventHandler, ClosingEventHandler);
        }

        private void OpenedEventHandler(object sender, DialogOpenedEventArgs eventargs)
        {
            this._dialogSession = eventargs.Session;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="eventArgs"></param>
        private void ClosingEventHandler(object sender, DialogClosingEventArgs eventArgs)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SelectRunDateTimeDialog_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                DidComplete = true;
                e.Handled = true;
                this._dialogSession.Close(false);
            }
            else if (e.Key == Key.Escape)
            {
                DidComplete = false;
                e.Handled = true;
                this._dialogSession.Close(false);
            }
        }
    }

    public class XtmfDialog
    {
        public bool DidComplete;
    }


}
