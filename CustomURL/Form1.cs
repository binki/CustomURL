using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace CustomURL
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            panel1.Visible = false;
            panel2.Visible = true;
            panel1.Top = panel1.Left = panel2.Top = panel2.Left = 0;

        }




        private void btnBrowse_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.InitialDirectory = System.Environment.GetEnvironmentVariable("ProgramFiles");
            DialogResult r = d.ShowDialog();
            if (r == DialogResult.OK)
            {
                txtApplication.Text  = d.FileName;
            }

        }

        private void btnHelp_Click(object sender, EventArgs e)
        {
            string h = "";
            h += "Variables for command line arguments:\n";
            h += "\n";
            h += "%Host% = Hostname\n";
            h += "%Port% = Port\n";
            h += "%Authority% = Hostname:Port\n";
            h += "%UserInfo% = Username\n";
            h += "\n";
            h += "Variables are case sensitive!\n";

            MessageBox.Show(h, "CustomURL", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if(txtProtocol.Text.Length == 0)
            {
                MessageBox.Show("Bad protocol name!", "CustomURL", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            RegistryKey r = Registry.CurrentUser;
            if(radLocalMachine.Checked)
                r=Registry.LocalMachine;
            bool success = Register(txtProtocol.Text, txtApplication.Text, txtArguments.Text,r);
            if (success)
            {
                panel1.Visible = false;
                panel2.Visible = true;
            }
        }


        private bool Register(string protocol, string application, string arguments, RegistryKey registry)
        {

            RegistryKey cl = Registry.ClassesRoot.OpenSubKey(protocol);

            if (cl != null && cl.GetValue("URL Protocol") != null && cl.GetValue("CustomUrlApplication") == null)
                if (System.Windows.Forms.MessageBox.Show("Protocol '" + protocol + "' is already registered. Do you wish to overwrite the current information?", "CustomURL", System.Windows.Forms.MessageBoxButtons.YesNo, System.Windows.Forms.MessageBoxIcon.Warning) == System.Windows.Forms.DialogResult.No)
                    return false;

            try
            {
                RegistryKey r;
                r = registry.OpenSubKey("SOFTWARE\\Classes\\" + protocol, true);
                if (r == null)
                    r = registry.CreateSubKey("SOFTWARE\\Classes\\" + protocol);
                r.SetValue("", "URL: Protocol handled by CustomURL");
                r.SetValue("URL Protocol", "");
                r.SetValue("CustomUrlApplication", application);
                r.SetValue("CustomUrlArguments", arguments);

                r = registry.OpenSubKey("SOFTWARE\\Classes\\" + protocol + "\\DefaultIcon", true);
                if (r == null)
                    r = registry.CreateSubKey("SOFTWARE\\Classes\\" + protocol + "\\DefaultIcon");
                r.SetValue("", application);

                r = registry.OpenSubKey("SOFTWARE\\Classes\\" + protocol + "\\shell\\open\\command", true);
                if (r == null)
                    r = registry.CreateSubKey("SOFTWARE\\Classes\\" + protocol + "\\shell\\open\\command");

                r.SetValue("", "CustomURL.exe \"%1\"");


                // If 64-bit OS, also register in the 32-bit registry area. 
                if (registry.OpenSubKey("SOFTWARE\\Wow6432Node\\Classes") != null)
                {
                    r = registry.OpenSubKey("SOFTWARE\\Wow6432Node\\Classes\\" + protocol, true);
                    if (r == null)
                        r = registry.CreateSubKey("SOFTWARE\\Wow6432Node\\Classes\\" + protocol);
                    r.SetValue("", "URL: Protocol handled by CustomURL");
                    r.SetValue("URL Protocol", "");
                    r.SetValue("CustomUrlApplication", application);
                    r.SetValue("CustomUrlArguments", arguments);

                    r = registry.OpenSubKey("SOFTWARE\\Wow6432Node\\Classes\\" + protocol + "\\DefaultIcon", true);
                    if (r == null)
                        r = registry.CreateSubKey("SOFTWARE\\Wow6432Node\\Classes\\" + protocol + "\\DefaultIcon");
                    r.SetValue("", application);

                    r = registry.OpenSubKey("SOFTWARE\\Wow6432Node\\Classes\\" + protocol + "\\shell\\open\\command", true);
                    if (r == null)
                        r = registry.CreateSubKey("SOFTWARE\\Wow6432Node\\Classes\\" + protocol + "\\shell\\open\\command");

                    r.SetValue("", "CustomURL.exe \"%1\"");

                }

            }
            catch (System.UnauthorizedAccessException ex)
            {
                MessageBox.Show("You do not have permission to make changes to the registry!\n\nMake sure that you have administrative rights on this computer.", "CustomURL", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
         
            RefreshList();



            return true;




        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            panel1.Visible = false;
            panel2.Visible = true;
        }

        private void RefreshList()
        {
            this.Cursor = System.Windows.Forms.Cursors.WaitCursor;
            listBox1.Items.Clear();
            progressBar1.Visible = true;
            RegistryKey reg = Microsoft.Win32.Registry.ClassesRoot;

            string[] subkeys = reg.GetSubKeyNames();
            progressBar1.Maximum = subkeys.Length;         
            for(int i = 0 ; i < subkeys.Length ; i++)
            {
                progressBar1.Value = i;
                string subKeyName = subkeys[i];
                RegistryKey subKey = reg.OpenSubKey(subKeyName);
                if (subKey.GetValue("CustomUrlApplication") != null && subKey.GetValue("CustomUrlArguments") != null)
                    listBox1.Items.Add(subKeyName + ":// (" + subKey.GetValue("CustomUrlApplication").ToString() + " " + subKey.GetValue("CustomUrlArguments").ToString() + ")");
   
            }
            progressBar1.Value = 0;
            progressBar1.Visible = false;
            
            this.Cursor = System.Windows.Forms.Cursors.Default;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            timer1.Enabled = false;

            // Test if application exists in a directory within the path
            bool existsInPath = true;
            string dest = System.Environment.ExpandEnvironmentVariables("%SystemRoot%\\System32");
            dest += Application.ExecutablePath.Substring(Application.StartupPath.Length);
            if(!System.IO.File.Exists(dest))
            {
                DialogResult r = MessageBox.Show("CustomURL must be placed in the system path to work.\n\nDo you wish to copy CustomURL to " + dest + "?","CustomURL",MessageBoxButtons.YesNo,MessageBoxIcon.Question);
                if(r==DialogResult.Yes)
                {
                    System.IO.File.Copy(Application.ExecutablePath,dest);
                }
            }
          
            
            
            RefreshList();
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            RefreshList();
        }

        private bool SelectProtocol()
        {
            if (listBox1.SelectedItem == null)
            {
                MessageBox.Show("No protocol selected!", "CustomURL", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            string protocol = listBox1.SelectedItem.ToString();
            if (protocol.IndexOf("://") > 0)
                protocol = protocol.Substring(0, protocol.IndexOf("://"));

            txtProtocol.Text = protocol;
            RegistryKey r = Registry.ClassesRoot.OpenSubKey(protocol);
            txtApplication.Text = r.GetValue("CustomUrlApplication").ToString();
            txtArguments.Text = r.GetValue("CustomUrlArguments").ToString();
            if (Registry.CurrentUser.OpenSubKey("Software\\Classes\\" + protocol) != null)
                radCurrentUser.Select();
            else
                radLocalMachine.Select();

            return true;
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            txtProtocol.Enabled = true;
            txtProtocol.Text = txtApplication.Text = txtArguments.Text = "";
            radCurrentUser.Enabled = true;
            radLocalMachine.Enabled = true;
            panel1.Visible = true;
            panel2.Visible = false;
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            if (SelectProtocol())
            {
                try
                {
                    string p = txtProtocol.Text;
                    if (MessageBox.Show("You are about to unregister protocol '" + p + "'.\n\nAre you sure?", "CustomURL", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                    {
                        if (Registry.CurrentUser.OpenSubKey("Software\\Classes\\" + p) != null)
                            Registry.CurrentUser.OpenSubKey("Software\\Classes", true).DeleteSubKeyTree(p);
                        if (Registry.CurrentUser.OpenSubKey("Software\\Wow6432Node\\Classes\\" + p) != null)
                            Registry.CurrentUser.OpenSubKey("Software\\Wow6432Node\\Classes", true).DeleteSubKeyTree(p);
                        if (Registry.LocalMachine.OpenSubKey("Software\\Classes\\" + p) != null)
                            Registry.LocalMachine.OpenSubKey("Software\\Classes", true).DeleteSubKeyTree(p);
                        if (Registry.LocalMachine.OpenSubKey("Software\\Wow6432Node\\Classes\\" + p) != null)
                            Registry.LocalMachine.OpenSubKey("Software\\Wow6432Node\\Classes", true).DeleteSubKeyTree(p);
                        RefreshList();
                    }
                }
                catch (System.Security.SecurityException ex)
                {
                    MessageBox.Show("You do not have permission to make changes to the registry!\n\nMake sure that you have administrative rights on this computer.", "CustomURL", MessageBoxButtons.OK, MessageBoxIcon.Error);
       
                }

            }
        }

        private void listBox1_DoubleClick(object sender, EventArgs e)
        {
            btnEdit_Click(null, null);
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            if (SelectProtocol())
            {
                panel1.Visible = true;
                panel2.Visible = false;
                txtProtocol.Enabled = false;
                radLocalMachine.Enabled = false;
                radCurrentUser.Enabled = false;
            }
        }
    }
}