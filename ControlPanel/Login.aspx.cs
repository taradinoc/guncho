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
    public partial class Login : System.Web.UI.Page
    {
        private IController controller;

        protected void Page_Load(object sender, EventArgs e)
        {
            controller = Common.GetController(Session, true);
        }

        protected void btnLogin_Click(object sender, EventArgs e)
        {
            string name = UserName.Value;
            string password = UserPass.Value;

            if (controller.LogIn(name, password) == false)
            {
                Msg.Text = "Player name or password is invalid.";
            }
            else
            {
                FormsAuthentication.RedirectFromLoginPage(controller.CapitalizePlayerName(name), false);
            }
        }

        protected void btnCreatePlayer_Click(object sender, EventArgs e)
        {
            string name = UserName.Value;
            string password = UserPass.Value;

            if (password != VerifyPass.Value)
            {
                Msg.Text = "Passwords do not match.";
            }
            else if (controller.CreatePlayer(name, password) == false)
            {
                Msg.Text = "Invalid player name, name already in use, or permission denied.";
            }
            else
            {
                FormsAuthentication.RedirectFromLoginPage(name, false);
            }
        }
    }
}
