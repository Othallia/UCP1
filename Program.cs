using System;
using System.Windows.Forms;
using UCP1;

namespace UCP1
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new FormTambahTransaksi());
        }
    }
}
