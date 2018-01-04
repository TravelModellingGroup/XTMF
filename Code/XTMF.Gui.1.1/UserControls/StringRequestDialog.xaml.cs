using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// Interaction logic for StringRequestDialog.xaml
    /// </summary>
    public partial class StringRequestDialog : UserControl
    {

        private Func<string, bool> _validation;

        public string QuestionText { get; set; }

        public bool DidComplete { get; set; }

        public string UserInput { get; set; }

        private DialogSession _dialogSession;

        public StringRequestDialog(string question, Func<string, bool> validation)
        {
            InitializeComponent();
            DataContext = this;
            _validation = validation;
            QuestionText = question;
            DidComplete = false;
            UserInput = null;
            if (validation != null)
            {
                //ValidationLabel.Visibility = validation(Answer) ? Visibility.Hidden : Visibility.Visible;
            }
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
        /// <returns></returns>
        public async Task<object> ShowAsync()
        {
            return await DialogHost.Show(this, "RootDialog", OpenedEventHandler, ClosingEventHandler);
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StringRequestDialog_OnPreviewKeyDown(object sender, KeyEventArgs e)
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
    public class NotEmptyValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            return string.IsNullOrWhiteSpace((value ?? "").ToString())
                ? new ValidationResult(false, "Field is required.")
                : ValidationResult.ValidResult;
        }
    }
}
