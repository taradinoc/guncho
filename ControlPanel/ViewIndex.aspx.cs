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
using System.IO;

namespace ControlPanel
{
    public partial class ViewIndex : System.Web.UI.Page
    {
        private IController controller;
        private string realmName;

        protected void Page_Load(object sender, EventArgs e)
        {
            controller = Common.GetController(Session);

            realmName = Request.Params["realm"];
            if (realmName != null && realmName != "")
            {
                ViewState["realm"] = realmName;
            }
            else
            {
                realmName = ViewState["realm"] as string;
                if (realmName == null || realmName == "")
                {
                    errorPanel.Visible = true;
                    indexPanel.Visible = false;
                    errorLabel.Text = "No realm specified.";
                    return;
                }
            }

            if (controller.GetAccessLevel(Context.User.Identity.Name, realmName) >= RealmAccessLevel.ViewSource)
            {
                indexRealmLabel.Text = realmName;

                problemsLink.NavigateUrl = MakeLink("Problems.html");
                contentsLink.NavigateUrl = MakeLink("Contents.html");
                actionsLink.NavigateUrl = MakeLink("Actions.html");
                kindsLink.NavigateUrl = MakeLink("Kinds.html");
                phrasesLink.NavigateUrl = MakeLink("Phrasebook.html");
                rulesLink.NavigateUrl = MakeLink("Rules.html");
                scenesLink.NavigateUrl = MakeLink("Scenes.html");
                worldLink.NavigateUrl = MakeLink("World.html");

                string file = Request.Params["file"];

                if (file == null || file == "")
                    file = "Contents.html";

                string indexPath = controller.GetRealmIndexPath(realmName);
                string errorMsg = null;

                if (indexPath == null)
                {
                    errorMsg = "Realm index not available.";
                }
                else
                {
                    if (file.Contains("..") || file.Contains(":") ||
                                file.StartsWith("/") || file.StartsWith(@"\"))
                    {
                        errorMsg = "Invalid file specification.";
                    }
                    else
                    {
                        file = Path.Combine(indexPath, file);
                        if (!File.Exists(file))
                            errorMsg = "Requested index file not found.";
                    }
                }

                if (errorMsg == null)
                {
                    indexErrorLabel.Visible = false;
                    indexView.Visible = true;
                    indexView.FileName = file;
                }
                else
                {
                    indexErrorLabel.Visible = true;
                    indexErrorLabel.Text = errorMsg;
                    indexView.Visible = false;
                }
            }
            else
            {
                errorPanel.Visible = true;
                indexPanel.Visible = false;
                errorLabel.Text = "Unable to view this realm.";
            }
        }

        private string MakeLink(string file)
        {
            return string.Format("ViewIndex.aspx?realm={0}&file={1}",
                HttpUtility.UrlEncode(realmName),
                HttpUtility.UrlEncode(file));
        }

        protected void backButton_Click(object sender, EventArgs e)
        {
            Response.Redirect("Default.aspx");
        }
    }
}
