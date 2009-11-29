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
    public partial class Header : System.Web.UI.UserControl
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (Context.User.Identity.IsAuthenticated)
            {
                userGreetingLabel.Text = "Hello, " + Context.User.Identity.Name + ".";

                IController controller = Common.GetController(Session);
                adminLabel.Visible = controller.IsPlayerAdmin(Context.User.Identity.Name);
            }
            else
            {
                userGreetingLabel.Text = "You are not logged in.";
                userActionsLabel.Visible = false;
            }
        }

        protected void logoutLink_Click(object sender, EventArgs e)
        {
            FormsAuthentication.SignOut();
            Response.Redirect("Default.aspx");
        }
    }
}