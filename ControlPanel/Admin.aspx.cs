using System;
using System.Data;
using System.Configuration;
using System.Collections;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.UI.WebControls.WebParts;
using System.Web.UI.HtmlControls;
using Guncho;

namespace ControlPanel
{
    public partial class Admin : System.Web.UI.Page
    {
        private IController controller;

        protected void Page_Load(object sender, EventArgs e)
        {
            controller = Common.GetController(Session);

            if (controller.IsPlayerAdmin(Context.User.Identity.Name))
            {
                errorLabel.Text = "";
                adminPanel.Visible = true;
            }
            else
            {
                errorLabel.Text = "Permission denied.";
                adminPanel.Visible = false;
            }
        }

        protected void btnShutdown_Click(object sender, EventArgs e)
        {
            if (chkConfirmShutdown.Checked)
            {
                errorLabel.Text = "The server is shutting down.";
                controller.Shutdown("requested by " + Context.User.Identity.Name);
            }
            else
            {
                errorLabel.Text = "You must confirm shutdown by clicking the check box.";
            }
        }

        protected void btnResetPasswd_Click(object sender, EventArgs e)
        {
            string user = txtResetUser.Text;
            string pass = txtResetPasswd.Text;

            if (user == "" || pass == "")
            {
                errorLabel.Text = "Player name or password missing.";
            }
            else if (!controller.IsPlayer(user))
            {
                errorLabel.Text = "No such player.";
            }
            else
            {
                errorLabel.Text = "Changed password for " + controller.CapitalizePlayerName(user) + ".";
                controller.ChangePassword(user, pass);
                controller.SavePlayerChanges(user);
            }
        }
    }
}
