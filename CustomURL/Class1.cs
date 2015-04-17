using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Design;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using System.IO;

namespace StudioControls.Controls
{
    /// <summary>
    /// Makes a button that shows a shield if its application is run without admin privileges.
    /// 
    /// This control can be used to restart the current application with admin, or start another with admin.
    /// 
    /// Place code in a subscribed Click event that you only want executed by administrators.
    /// The reason is, you can use the button to both obtain admin power via UAC, and when the program restarts
    /// it'll loose the shield icon (but theirs a option to still show it), and then whatever code you wanted
    /// to be executed in the click event --- for admin only --- can now be executed in the priviliged app.
    /// </summary>
    /// <example>
    /// Some things to watch for:
    /// 
    /// 0. AlwaysShowShield property for your enjoyment.
    /// 
    /// 1. Not all UAC privilege escalation requires a program restart, for example in "programs and features", the "turn windows features on or off" 
    /// feature opens a new application without closing the one which spawned it.
    /// 
    /// Use the following properties to specify loading a new process:
    ///
    /// EscalationCustomProcessPath = string path to a .exe, etc
    /// EscalationGoal = EscalationGoal.StartAnotherApplication;
    /// 
    /// 2. if the user gets admin priviliges, and this controls EscalationGoal = EscalationGoal.RestartThisApplication, 
    /// the program will restart... so your program might require saving and restoring of program state.
    /// You can listen for this in general by hooking onto this event "Application.ApplicationExit += ..."
    /// However, their is no way to differentiate between UAC starting the application shutdown/restart, and a normal shutdown, 
    /// so i included additional events:
    ///
    /// EscalationStarting
    /// EscalationCancelled
    /// EscalationSuccessful
    /// 
    /// Note: if EscalationGoal = EscalationGoal.StartAnotherApplication then this program will not exit.
    /// 
    /// 3. When your program restarts, make sure you preserve your programs StartPosition, else it might look sloppy if it suddenly moves from where it
    /// appears in the greyed out UAC background... but then again... the taskmanager doesn't care about its StartPosition when you press 
    /// "Show processes from all users" and it restarts lol. For me i just use StartPosition = CenterScreen for the affected forms.
    /// </example>
    /// <remarks>
    /// Reference: 
    /// http://www.codeproject.com/KB/vista-security/UAC_Shield_for_Elevation.aspx, 
    /// http://buildingsecurecode.blogspot.com/2007/06/how-to-display-uac-shield-icon-inside.html
    /// </remarks>
    public class StudioShieldButton : Button
    {
        #region Fields

        public delegate void EscalationHandler(EscalationGoal escalationGoal, object sender, EventArgs e);

        #endregion

        #region Properties

        #region FlatStyle Appearance Property

        /// <summary>
        /// Determines the appearance of the control when a user moves the mouse the mouse over the control and clicks.
        /// </summary>
        [
        Category("Appearance"),
        Description("Determines the appearance of the control when a user moves the mouse the mouse over the control and clicks."),
        DefaultValue(typeof(FlatStyle), "System"),
        ReadOnly(true),
        Browsable(false)
        ]
        public new FlatStyle FlatStyle
        {
            get { return base.FlatStyle; }
        }

        #endregion

        #region AlwaysShowShield Appearance Property

        private bool alwaysShowShield = false;
        /// <summary>
        /// Gets or sets if the shield icon should always be visible.
        /// </summary>
        [
        Category("Appearance"),
        Description("Gets or sets if the shield icon should always be visible."),
        DefaultValue(typeof(bool), "false"),
        ]
        public bool AlwaysShowShield
        {
            get { return alwaysShowShield; }
            set
            {
                alwaysShowShield = value;

                if (!UACUtilities.HasAdminPrivileges() || alwaysShowShield) // then show the shield on the button
                {
                    // Show the shield
                    SendMessage(new HandleRef(this, this.Handle), BCM_SETSHIELD, new IntPtr(0), new IntPtr(1)); // the (1) for true
                }

                else
                {
                    // Hide the shield
                    SendMessage(new HandleRef(this, this.Handle), BCM_SETSHIELD, new IntPtr(0), new IntPtr(0)); // the (0) for false
                }

            }
        }

        #region Serialization
        /// <summary>
        /// Should serialize AlwaysShowShield property.
        /// </summary>
        /// <returns>True if should serialize, else false.</returns>
        private bool ShouldSerializeAlwaysShowShield()
        {
            //Only serialize nondefault values
            return (alwaysShowShield != false);
        }
        /// <summary>
        /// Resets AlwaysShowShield property.
        /// </summary>
        private void ResetAlwaysShowShield()
        {
            alwaysShowShield = false;

            if (UACUtilities.HasAdminPrivileges()) // then show the shield on the button
            {
                // Show the shield
                SendMessage(new HandleRef(this, this.Handle), BCM_SETSHIELD, new IntPtr(0), new IntPtr(1)); // the (1) for true
            }

            else
            {
                // Hide the shield
                SendMessage(new HandleRef(this, this.Handle), BCM_SETSHIELD, new IntPtr(0), new IntPtr(0)); // the (0) for false
            }
        }
        #endregion

        #endregion

        #region EscalationGoal Behavior Property

        private EscalationGoal escalationGoal = EscalationGoal.StartAnotherApplication;
        /// <summary>
        /// Gets or sets the EscalationGoal for this UAC "shield" button.
        /// </summary>
        [
        Category("Behavior"),
        Description("Gets or sets the EscalationGoal for this UAC \"shield\" button."),
        DefaultValue(typeof(EscalationGoal), "StartAnotherApplication")
        ]
        public EscalationGoal EscalationGoal
        {
            get { return escalationGoal; }
            set { escalationGoal = value; }
        }

        #region Serialization
        /// <summary>
        /// Should serialize EscalationGoal property.
        /// </summary>
        /// <returns>True if should serialize, else false.</returns>
        private bool ShouldSerializeEscalationGoal()
        {
            //Only serialize nondefault values
            return (escalationGoal != EscalationGoal.StartAnotherApplication);
        }
        /// <summary>
        /// Resets EscalationGoal property.
        /// </summary>
        private void ResetEscalationGoal()
        {
            escalationGoal = EscalationGoal.StartAnotherApplication;
        }
        #endregion

        #endregion

        #region EscalationCustomProcessPath Behavior Property

        private string escalationCustomProcessPath = String.Empty;
        /// <summary>
        /// Gets or sets the path to the application that this program starts when hit. 
        /// Leave it \"\" to load the current Application.ExecutablePath.
        /// </summary>
        [
        Category("Behavior"),
        Description("Gets or sets the path to the application that this program starts when hit. Leave it \"\" to load the current Application.ExecutablePath."),
        DefaultValue(typeof(string), "\"\""),
        Editor(typeof(FilteredFileNameEditor /*FileNameEditor*/), typeof(UITypeEditor))
        ]
        public string EscalationCustomProcessPath
        {
            get { return escalationCustomProcessPath; }
            set { escalationCustomProcessPath = value; }
        }

        #region Serialization
        /// <summary>
        /// Should serialize EscalationCustomProcessPath property.
        /// </summary>
        /// <returns>True if should serialize, else false.</returns>
        private bool ShouldSerializeEscalationCustomProcessPath()
        {
            //Only serialize nondefault values
            return (escalationCustomProcessPath != String.Empty);
        }
        /// <summary>
        /// Resets EscalationCustomProcessPath property.
        /// </summary>
        private void ResetEscalationCustomProcessPath()
        {
            escalationCustomProcessPath = String.Empty;
        }
        #endregion

        #endregion

        #endregion

        #region DllImports

        private const uint BCM_SETSHIELD = 0x0000160C;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(HandleRef hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        #endregion

        #region Constructor

        public StudioShieldButton()
        {
            base.FlatStyle = FlatStyle.System; // This must be FlatStyle.System for the shield to work
            // I also locked the FlatStyle property:
            // DefaultValue(typeof(FlatStyle), "System"), 
            // ReadOnly(true),
            // Browsable(false) 
            // so hopefully vs.net won't try to serialize it.

            AlwaysShowShield = alwaysShowShield; //i'm forcing the code in the Set{} of "AlwaysShowShield" to run.
        }

        #endregion

        #region Members

        private string EscalationProcessPath()
        {
            string path;

            if (escalationGoal == EscalationGoal.StartAnotherApplication)
            {
                if (String.IsNullOrEmpty(escalationCustomProcessPath))
                    throw new Exception("escalationGoal == EscalationGoal.StartAnotherApplication but escalationCustomProcessPath is null or empty.");

                path = escalationCustomProcessPath;
            }

            else
                path = Application.ExecutablePath;

            return path;
        }

        private void OnEscalationStarting(EscalationGoal escalationGoal, object sender, EventArgs e)
        {
            if (EscalationStarting != null)
            {
                EscalationStarting(escalationGoal, sender, e);
            }
        }

        private void OnEscalationCancelled(EscalationGoal escalationGoal, object sender, EventArgs e)
        {
            if (EscalationCancelled != null)
            {
                EscalationCancelled(escalationGoal, sender, e);
            }
        }

        private void OnEscalationSuccessful(EscalationGoal escalationGoal, object sender, EventArgs e)
        {
            if (EscalationSuccessful != null)
            {
                EscalationSuccessful(escalationGoal, sender, e);
            }
        }

        #endregion

        #region Overrided Members

        protected override void OnClick(EventArgs e)
        {
            if (UACUtilities.HasAdminPrivileges())
                base.OnClick(e); // this can only be called if we're an admin, we are assuming it contains admin-only stuff

            else
            {
                UACUtilities.AttemptPrivilegeEscalation(
                EscalationProcessPath(),
                delegate()
                {
                    OnEscalationStarting(escalationGoal, this, new EventArgs());
                },
                delegate()
                {
                    OnEscalationCancelled(escalationGoal, this, new EventArgs());
                },
                delegate()
                {
                    OnEscalationSuccessful(escalationGoal, this, new EventArgs());

                    if (escalationGoal == EscalationGoal.RestartThisApplication)
                        Application.Exit();
                });
            }
        }

        #endregion

        #region Events

        #region EscalationStarting Action

        [Category("Action")]
        [Description("A UAC privilege escalation is going to start next.")]
        public event EscalationHandler EscalationStarting;

        #endregion

        #region EscalationCancelled Action

        [Category("Action")]
        [Description("The user cancelled the UAC privilege escalation prompt.")]
        public event EscalationHandler EscalationCancelled;

        #endregion

        #region EscalationSuccessful Action

        [Category("Action")]
        [Description("The user was successful in getting admin privileges.")]
        public event EscalationHandler EscalationSuccessful;

        #endregion

        #endregion
    }

    public class UACUtilities
    {
        #region Fields

        public delegate void EscalationEvent();

        #endregion

        #region Members

        public static bool HasAdminPrivileges()
        {
            WindowsIdentity windowsIdentity = WindowsIdentity.GetCurrent();
            WindowsPrincipal windowsPrincipal = new WindowsPrincipal(windowsIdentity);
            return windowsPrincipal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        public static ProcessStartInfo EscalationProcessStartInfo(string path)
        {
            if (String.IsNullOrEmpty(path))
                throw new ArgumentNullException("path");

            if (!File.Exists(path))
                throw new FileNotFoundException("path");

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.UseShellExecute = true;
            startInfo.FileName = path;
            startInfo.Verb = "runas"; // will bring up the UAC run-as menu when this ProcessStartInfo is used

            return startInfo;
        }

        public static void AttemptPrivilegeEscalation(string path)
        {
            if (String.IsNullOrEmpty(path))
                throw new ArgumentNullException("path");

            AttemptPrivilegeEscalation(path, null, null, null);
        }

        public static void AttemptPrivilegeEscalation(string path, EscalationEvent starting, EscalationEvent cancelled, EscalationEvent successful)
        {
            if (String.IsNullOrEmpty(path))
                throw new ArgumentNullException("path");

            if (!File.Exists(path))
                throw new FileNotFoundException("path");

            if (HasAdminPrivileges())
                throw new SecurityException("Already have administrator privileges.");

            ProcessStartInfo startInfo = EscalationProcessStartInfo(path);

            if (starting != null)
                starting();

            ////todo: slight issue with this... when the program is in admin status... 
            ////if my EscalationCustomProcessPath is set to some random program that goes online right away
            ////i get norton saying this application program (not the other program) is trying to connect to the internet...

            try
            {
                Process.Start(startInfo);
            }

            catch (System.ComponentModel.Win32Exception) //occurs when the user has clicked Cancel on the UAC prompt.
            {
                if (cancelled != null)
                    cancelled();

                return; // By returning, we are ignoring the user tried to get UAC priviliges but then hit cancel at the "Run-As" prompt.
            }

            if (successful != null)
                successful();
        }

        #endregion
    }

    public enum EscalationGoal
    {
        RestartThisApplication,
        StartAnotherApplication
    }

    //http://72.14.205.104/search?q=cache:2HrHWneYMSAJ:forums.microsoft.com/MSDN/ShowPost.aspx%3FPostID%3D66703%26SiteID%3D1+UITypeEditor+directory+path+Editor+property&hl=en&ct=clnk&cd=2&gl=us
    internal class FilteredFileNameEditor : UITypeEditor
    {
        private OpenFileDialog ofd = new OpenFileDialog();

        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
        {
            return UITypeEditorEditStyle.Modal;
        }

        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            ofd.FileName = value.ToString();
            ofd.Filter = "Programs|*.exe|All Files|*.*";
            //ofd.Filter = "Programs|*.exe;*.pif,*.com;*.bat;*.cmd|All Files|*.*"; 
            //ofd.Filter = "Text File|*.txt|All Files|*.*";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                return ofd.FileName;
            }
            return base.EditValue(context, provider, value);
        }
    }
}

