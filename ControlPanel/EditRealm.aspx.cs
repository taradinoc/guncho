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
using System.Collections.Generic;

namespace ControlPanel
{
    public partial class EditRealm : System.Web.UI.Page
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
                    sourcePanel.Visible = false;
                    resultsPanel.Visible = false;
                    errorLabel.Text = "No realm specified.";
                    return;
                }
            }

            sourceBox.Attributes["onkeydown"] =
                "if (event.which || event.keyCode) {" +
                    "if ((event.which == 9) || (event.keyCode == 9)) {" +
                        "return allowTab(document.getElementById('" + sourceBox.ClientID + "'));" +
                    "} else {" +
                        "return true;" +
                    "}" +
                "}";
            sourceBox.Attributes["onkeypress"] =
                "if (event.which || event.keyCode) {" +
                    "if ((event.which == 9) || (event.keyCode == 9)) {" +
                        "return false;" +
                    "}" +
                "}" +
                "return true;";

            string playerName = Context.User.Identity.Name;
            RealmAccessLevel access = controller.GetAccessLevel(playerName, realmName);
            if (access >= RealmAccessLevel.ViewSource)
            {
                errorPanel.Visible = false;
                resultsPanel.Visible = false;
                sourcePanel.Visible = true;

                if (srcLangDrop.Items.Count == 0)
                {
                    string dummy;
                    foreach (KeyValuePair<string, string> pair in Common.GetRealmFactories(controller, out dummy))
                        srcLangDrop.Items.Add(new ListItem(pair.Value, pair.Key));
                }

                if (!Page.IsPostBack)
                {
                    switch (controller.GetRealmPrivacy(realmName))
                    {
                        case RealmPrivacyLevel.Private:
                            privacyDrop.SelectedValue = "private";
                            break;

                        case RealmPrivacyLevel.Hidden:
                            privacyDrop.SelectedValue = "hidden";
                            break;

                        case RealmPrivacyLevel.Public:
                            privacyDrop.SelectedValue = "public";
                            break;

                        case RealmPrivacyLevel.Joinable:
                            privacyDrop.SelectedValue = "joinable";
                            break;

                        case RealmPrivacyLevel.Viewable:
                            privacyDrop.SelectedValue = "viewable";
                            break;
                    }

                    string factoryName;
                    sourceBox.Text = controller.GetRealmSource(realmName, out factoryName);
                    srcLangDrop.SelectedValue = factoryName;
                }

                if (controller.IsRealmCondemned(realmName) && !controller.IsPlayerAdmin(playerName))
                {
                    sourceRealmLabel.Text = "The realm \"" + realmName +
                        "\" has been condemned and may only be restored by an admin.";
                    sourceBox.ReadOnly = true;
                    sourceSaveBtn.Visible = false;
                }
                else if (access >= RealmAccessLevel.EditSource)
                {
                    sourceRealmLabel.Text = "Editing realm \"" + realmName + "\":";
                    sourceBox.ReadOnly = false;
                    sourceSaveBtn.Visible = true;
                }
                else
                {
                    sourceRealmLabel.Text = "Viewing realm \"" + realmName + "\":";
                    sourceBox.ReadOnly = true;
                    sourceSaveBtn.Visible = false;
                }

                if (access >= RealmAccessLevel.EditSettings)
                {
                    privacyDrop.Enabled = true;
                }
                else
                {
                    privacyDrop.Enabled = false;
                }

                if (access >= RealmAccessLevel.SafetyOff)
                {
                    deleteRow.Visible = true;
                }
                else
                {
                    deleteRow.Visible = false;
                }
            }
            else
            {
                errorPanel.Visible = true;
                errorLabel.Text = "Unable to view source for this realm.";
                sourcePanel.Visible = false;
            }
        }

        protected void Page_PreRender(object sender, EventArgs e)
        {
            confirmDeleteChk.Checked = false;
        }

        protected void sourceSaveBtn_Click(object sender, EventArgs e)
        {
            string playerName = Context.User.Identity.Name;
            RealmAccessLevel access = controller.GetAccessLevel(playerName, realmName);

            if (access >= RealmAccessLevel.EditSettings)
            {
                bool ok = false;

                switch (privacyDrop.SelectedValue)
                {
                    case "private":
                        ok = controller.SetRealmPrivacy(playerName, realmName, RealmPrivacyLevel.Private);
                        break;

                    case "hidden":
                        ok = controller.SetRealmPrivacy(playerName, realmName, RealmPrivacyLevel.Hidden);
                        break;

                    case "public":
                        ok = controller.SetRealmPrivacy(playerName, realmName, RealmPrivacyLevel.Public);
                        break;

                    case "joinable":
                        ok = controller.SetRealmPrivacy(playerName, realmName, RealmPrivacyLevel.Joinable);
                        break;

                    case "viewable":
                        ok = controller.SetRealmPrivacy(playerName, realmName, RealmPrivacyLevel.Viewable);
                        break;
                }

                if (!ok)
                {
                    errorLabel.Visible = true;
                    errorLabel.Text = "Privacy level could not be changed.";
                }
            }

            if (access >= RealmAccessLevel.EditSource)
            {
                RealmEditingOutcome outcome = controller.SetRealmSource(
                    playerName,
                    realmName,
                    srcLangDrop.SelectedValue,
                    sourceBox.Text);

                string indexPath = controller.GetRealmIndexPath(realmName);

                resultsPanel.Visible = true;
                resultsView.Visible = true;
                resultsErrorLabel.Visible = false;

                if (outcome == RealmEditingOutcome.Success)
                {
                    resultsPanel.BackColor = System.Drawing.Color.LightGreen;
                    viewIndexBtn.Visible = true;
                }
                else
                {
                    resultsPanel.BackColor = System.Drawing.Color.LightSalmon;
                    viewIndexBtn.Visible = false;
                }

                switch (outcome)
                {
                    case RealmEditingOutcome.Success:
                        resultsView.FileName = Path.Combine(indexPath, "Problems.html");
                        break;

                    case RealmEditingOutcome.NiError:
                    case RealmEditingOutcome.InfError:
                        resultsView.FileName = Path.Combine(indexPath + ".preview", "Problems.html");
                        break;

                    case RealmEditingOutcome.Missing:
                        resultsView.Visible = false;
                        resultsErrorLabel.Visible = true;
                        resultsErrorLabel.Text = "Source file missing. Maybe the realm no longer exists.";
                        break;

                    case RealmEditingOutcome.PermissionDenied:
                        resultsView.Visible = false;
                        resultsErrorLabel.Visible = true;
                        resultsErrorLabel.Text = "Permission denied.";
                        break;

                    case RealmEditingOutcome.VMError:
                        resultsView.Visible = false;
                        resultsErrorLabel.Visible = true;
                        resultsErrorLabel.Text = "The realm compiled but could not be loaded.";
                        break;
                }
            }
        }

        protected void sourceCancelBtn_Click(object sender, EventArgs e)
        {
            Response.Redirect("Default.aspx");
        }

        protected void viewIndexBtn_Click(object sender, EventArgs e)
        {
            Response.Redirect(string.Format("ViewIndex.aspx?realm={0}",
                HttpUtility.UrlEncode(realmName)));
        }

        protected void deleteRealmBtn_Click(object sender, EventArgs e)
        {
            if (confirmDeleteChk.Checked)
            {
                if (controller.DeleteRealm(Context.User.Identity.Name, realmName))
                {
                    Response.Redirect("Default.aspx");
                }
                else
                {
                    errorLabel.Text = "The realm could not be deleted.";
                    errorLabel.Visible = true;
                }
            }
            else
            {
                errorLabel.Text = "To delete the realm, you must check the confirmation check box.";
                errorLabel.Visible = true;
            }
        }
    }
}
