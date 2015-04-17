using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Security.Principal;

namespace CustomURL
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {

                // Check if the current user is administrator 
                WindowsIdentity windowsIdentity = WindowsIdentity.GetCurrent();
                WindowsPrincipal windowsPrincipal = new WindowsPrincipal(windowsIdentity);
                bool isAdmin = windowsPrincipal.IsInRole(WindowsBuiltInRole.Administrator);

                // If not, restart the application to show the gui.
                // Using the "runas" verb will bring up the 
                // UAC prompt on windows vista.
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo();
                psi.FileName = Application.ExecutablePath;
                psi.Arguments = "/gui";
                if(!isAdmin)
                    psi.Verb = "runas";
                try
                {
                    System.Diagnostics.Process.Start(psi);
                }
                catch
                {
                }
       
            }
            else if (args[0].ToLower() == "/test")
            {
                System.Environment.Exit(2);
            }
            else if (args[0].ToLower() == "/gui")
            {
                // Show the gui.
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new Form1());
            }
            else
            {

                Uri u = new Uri(args[0]);
                RegistryKey reg = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(u.Scheme);
                
                if (reg == null)
                {
                    MessageBox.Show("Protocol '" + u.Scheme + "' is not registered.","CustomURL",MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (reg.GetValue("CustomUrlApplication") == null || reg.GetValue("CustomUrlArguments") == null)
                {
                    MessageBox.Show("No CustomURL information found for protocol '" + u.Scheme + "'.", "CustomURL", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                string fileName = reg.GetValue("CustomUrlApplication").ToString();
                string arguments = reg.GetValue("CustomUrlArguments").ToString();

                arguments = arguments.Replace("%Authority%", u.Authority);
                arguments = arguments.Replace("%Host%", u.Host);
                arguments = arguments.Replace("%Port%", u.Port.ToString());
                arguments = arguments.Replace("%UserInfo%", u.UserInfo);

                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo();
                psi.FileName = System.Environment.ExpandEnvironmentVariables(fileName);
                psi.Arguments = arguments;
                try
                {
                    System.Diagnostics.Process.Start(psi);
                }
                catch
                {
                    MessageBox.Show("Failed to create new process for command line '" + fileName + " " + arguments + "'.", "CustomURL", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}