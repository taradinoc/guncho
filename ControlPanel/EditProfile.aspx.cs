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
    public partial class EditProfile : System.Web.UI.Page
    {
        private IController controller;
        private string playerName;

        protected void Page_Load(object sender, EventArgs e)
        {
            controller = Common.GetController(Session);
            playerName = Context.User.Identity.Name;

            if (!IsPostBack)
            {
                // load gender
                string gender = controller.GetPlayerAttribute(playerName, "gender");
                switch (gender.ToLower())
                {
                    case "m":
                        genderRadios.SelectedValue = "m";
                        break;
                    case "f":
                        genderRadios.SelectedValue = "f";
                        break;
                    default:
                        genderRadios.SelectedValue = "n";
                        break;
                }

                // load description
                string attrVal = controller.GetPlayerAttribute(playerName, "description");
                descriptionBox.Text = Controller.Desanitize(attrVal);
            }
        }

        protected void saveChangesBtn_Click(object sender, EventArgs e)
        {
            // change password?
            if (oldPassword.Text != "")
            {
                if (!controller.LogIn(playerName, oldPassword.Text))
                {
                    errorLabel.Text = "Old password is incorrect.";
                    return;
                }

                if (newPassword1.Text == "")
                {
                    errorLabel.Text = "New password is empty.";
                    return;
                }

                if (newPassword1.Text != newPassword2.Text)
                {
                    errorLabel.Text = "New password doesn't match verification.";
                    return;
                }

                controller.ChangePassword(playerName, newPassword1.Text);
            }

            // update gender
            controller.SetPlayerAttribute(playerName, "gender", genderRadios.SelectedValue);

            // update description
            string attrVal = Controller.Sanitize(descriptionBox.Text);
            controller.SetPlayerAttribute(playerName, "description", attrVal);

            errorLabel.Text = "";
            controller.SavePlayerChanges(playerName);
            Response.Redirect("Default.aspx");
        }

        protected void cancelBtn_Click(object sender, EventArgs e)
        {
            Response.Redirect("Default.aspx");
        }
    }
}
