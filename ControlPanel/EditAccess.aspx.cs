using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;
using System.Web.UI.WebControls.WebParts;
using System.Xml.Linq;
using Guncho;

namespace ControlPanel
{
    public partial class EditAccess : System.Web.UI.Page
    {
        private IController controller;
        private string realmName;

        private string[] aclNames;
        private RealmAccessLevel[] aclLevels;

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
                    errorLabel.Text = "No realm specified.";
                    aclEntriesTable.Visible = false;
                    return;
                }
            }

            InitRealmsTable();

            string playerName = Context.User.Identity.Name;
            RealmAccessLevel access = controller.GetAccessLevel(playerName, realmName);
            if (access >= RealmAccessLevel.EditAccess)
            {
                errorLabel.Text = "";
            }
            else
            {
                errorLabel.Text = "Unable to edit access list for this realm.";
                aclEntriesTable.Visible = false;
            }
        }

        private void InitRealmsTable()
        {
            while (aclEntriesTable.Rows.Count > 2)
                aclEntriesTable.Rows.RemoveAt(1);

            controller.GetRealmAccessList(realmName, out aclNames, out aclLevels);

            for (int i = 0; i < aclNames.Length; i++)
            {
                TableRow row = new TableRow();

                TableCell cell = new TableCell();
                cell.Text = HttpUtility.HtmlEncode(aclNames[i]);
                row.Cells.Add(cell);

                cell = new TableCell();
                cell.Text = Enum.GetName(typeof(RealmAccessLevel), aclLevels[i]);
                row.Cells.Add(cell);

                cell = new TableCell();
                Button deleteBtn = new Button();
                deleteBtn.ID = "delbtn_" + Common.EncodeAsID(aclNames[i]);
                deleteBtn.Text = "Delete";
                string thisName = aclNames[i];
                deleteBtn.Click += delegate { DoDeleteEntry(thisName); };
                cell.Controls.Add(deleteBtn);
                row.Cells.Add(cell);

                aclEntriesTable.Rows.AddAt(aclEntriesTable.Rows.Count - 1, row);
            }
        }

        protected void doneBtn_Click(object sender, EventArgs e)
        {
            Response.Redirect("Default.aspx");
        }

        protected void addEntryBtn_Click(object sender, EventArgs e)
        {
            string playerName = Context.User.Identity.Name;
            RealmAccessLevel access = controller.GetAccessLevel(playerName, realmName);
            if (access >= RealmAccessLevel.EditAccess)
            {
                if (newEntryLevel.SelectedIndex == 0)
                {
                    errorLabel.Text = "You must select an access level.";
                    return;
                }

                newEntryName.Text = newEntryName.Text.Trim();
                if (newEntryName.Text == "")
                {
                    errorLabel.Text = "You must enter a player name to add to the list.";
                    return;
                }

                foreach (string name in aclNames)
                    if (name.ToLower() == newEntryName.Text.ToLower())
                    {
                        errorLabel.Text = "That player is already in the access list.";
                        return;
                    }

                List<string> newNames = new List<string>(aclNames);
                List<RealmAccessLevel> newLevels = new List<RealmAccessLevel>(aclLevels);

                RealmAccessLevel newLevel = (RealmAccessLevel)Enum.Parse(
                    typeof(RealmAccessLevel), newEntryLevel.SelectedValue);

                newNames.Add(newEntryName.Text);
                newLevels.Add(newLevel);

                try
                {
                    controller.SetRealmAccessList(playerName, realmName,
                        newNames.ToArray(), newLevels.ToArray());
                }
                catch (Exception ex)
                {
                    string[] lines = ex.Message.Split('\n');
                    if (lines.Length > 0)
                        errorLabel.Text = HttpUtility.HtmlEncode(lines[0]);
                    else
                        errorLabel.Text = "Unknown error";
                    return;
                }

                InitRealmsTable();

                newEntryName.Text = "";
                newEntryLevel.SelectedIndex = 0;
            }
            else
            {
                errorLabel.Text = "Unable to edit access list for this realm.";
                aclEntriesTable.Visible = false;
            }
        }

        protected void DoDeleteEntry(string whose)
        {
            string playerName = Context.User.Identity.Name;
            RealmAccessLevel access = controller.GetAccessLevel(playerName, realmName);
            if (access >= RealmAccessLevel.EditAccess)
            {
                List<string> newNames = new List<string>(aclNames);
                List<RealmAccessLevel> newLevels = new List<RealmAccessLevel>(aclLevels);

                int index = newNames.IndexOf(whose);
                if (index >= 0)
                {
                    newNames.RemoveAt(index);
                    newLevels.RemoveAt(index);
                }

                try
                {
                    controller.SetRealmAccessList(playerName, realmName,
                        newNames.ToArray(), newLevels.ToArray());
                }
                catch (Exception ex)
                {
                    string[] lines = ex.Message.Split('\n');
                    if (lines.Length > 0)
                        errorLabel.Text = HttpUtility.HtmlEncode(lines[0]);
                    else
                        errorLabel.Text = "Unknown error";
                    return;
                }

                InitRealmsTable();
            }
            else
            {
                errorLabel.Text = "Unable to edit access list for this realm.";
                aclEntriesTable.Visible = false;
            }
        }
    }
}
