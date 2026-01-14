using Frosty.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Flurry.Editor.Windows
{
    public partial class SideBySideDiffDisplay : FrostyDockableWindow
    {
        public string OldString { get; set; }
        public string NewString { get; set; }

        public SideBySideDiffDisplay(string oldString, string newString)
        {
            InitializeComponent();

            OldString = oldString;
            NewString = newString;

            this.DataContext = this;
        }
    }
}
