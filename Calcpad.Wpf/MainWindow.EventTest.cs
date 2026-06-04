using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Calcpad.Core;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace Calcpad.Wpf
{
    public partial class MainWindow : Window
    {
        private void RegisterEvents()
        {
            this._parser.ProgressChanged += Parser_Progress;
        }

        private void Parser_Progress(object sender, ProgressEventArgs e)
        {
            Console.WriteLine($"Progress: {e.Value}, Message: {e.Message}");
        }
    }
}
