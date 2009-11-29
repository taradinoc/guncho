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
using System.Collections.Generic;

namespace ControlPanel
{
    public partial class _Default : System.Web.UI.Page
    {
        private IController controller;

        protected void Page_Load(object sender, EventArgs e)
        {
            controller = Common.GetController(Session);

            // fill in WHO list
            foreach (string[] playerInfo in controller.GetWhoList())
            {
                TableRow row = new TableRow();

                foreach (string item in playerInfo)
                {
                    TableCell cell = new TableCell();
                    cell.Text = item;
                    row.Cells.Add(cell);
                }

                whoTable.Rows.Add(row);
            }

            if (whoTable.Rows.Count == 1)
            {
                TableCell cell = new TableCell();
                cell.ColumnSpan = 3;
                cell.HorizontalAlign = HorizontalAlign.Center;
                cell.Text = "(No players are logged in.)";

                TableRow row = new TableRow();
                row.Cells.Add(cell);
                whoTable.Rows.Add(row);
            }

            // fill in realms list
            string[] ownedRealms, editableRealms, viewableRealms, otherRealms;
            ListAndSortRealms(out ownedRealms, out editableRealms, out viewableRealms, out otherRealms);

            AddRealmsToTable(ownedRealms, "Realms You Own", true, true);
            AddRealmsToTable(editableRealms, "Realms You Can Edit", true, true);
            AddRealmsToTable(viewableRealms, "Realms You Can View", true, false);
            AddRealmsToTable(otherRealms, "Other Realms", false, false);

            if (realmsTable.Rows.Count == 1)
            {
                TableCell cell = new TableCell();
                cell.ColumnSpan = 5;
                cell.HorizontalAlign = HorizontalAlign.Center;
                cell.Text = "(No realms available.)";

                TableRow row = new TableRow();
                row.Cells.Add(cell);
                realmsTable.Rows.Add(row);
            }
        }

        void ListAndSortRealms(out string[] ownedRealms, out string[] editableRealms,
            out string[] viewableRealms, out string[] otherRealms)
        {
            List<string> ownedList = new List<string>();
            List<string> editableList = new List<string>();
            List<string> viewableList = new List<string>();
            List<string> otherList = new List<string>();

            string[] allRealms = controller.ListRealms();
            string curPlayer = Context.User.Identity.Name.ToLower();

            foreach (string realmName in allRealms)
            {
                if (controller.GetRealmOwner(realmName).ToLower() == curPlayer)
                {
                    ownedList.Add(realmName);
                }
                else
                {
                    RealmAccessLevel access = controller.GetAccessLevel(curPlayer, realmName);

                    if (access >= RealmAccessLevel.EditSource)
                        editableList.Add(realmName);
                    else if (access >= RealmAccessLevel.ViewSource)
                        viewableList.Add(realmName);
                    else if (access >= RealmAccessLevel.Visible)
                        otherList.Add(realmName);
                }
            }

            ownedList.Sort();
            ownedRealms = ownedList.ToArray();

            editableList.Sort();
            editableRealms = editableList.ToArray();

            viewableList.Sort();
            viewableRealms = viewableList.ToArray();

            otherList.Sort();
            otherRealms = otherList.ToArray();
        }

        private static TableHeaderRow MakeRealmsColHeaderRow()
        {
            TableHeaderRow row = new TableHeaderRow();

            TableHeaderCell nameCell = new TableHeaderCell();
            nameCell.Text = "Realm Name";
            row.Cells.Add(nameCell);

            TableHeaderCell ownerCell = new TableHeaderCell();
            ownerCell.Text = "Owner";
            row.Cells.Add(ownerCell);

            TableHeaderCell actionsCell = new TableHeaderCell();
            actionsCell.Text = "Actions";
            actionsCell.ColumnSpan = 3;
            row.Cells.Add(actionsCell);

            return row;
        }

        void AddRealmsToTable(string[] realmNames, string sectionHeader,
            bool canView, bool canEdit)
        {
            if (realmNames == null || realmNames.Length == 0)
                return;

            string playerName = Context.User.Identity.Name;

            TableRow secHdrRow = new TableRow();
            realmsTable.Rows.Add(secHdrRow);

            TableCell secHdrCell = new TableCell();
            secHdrCell.ColumnSpan = 5;
            secHdrCell.HorizontalAlign = HorizontalAlign.Center;
            secHdrCell.Text = sectionHeader;
            secHdrCell.CssClass = "realmsSection";
            secHdrRow.Cells.Add(secHdrCell);

            realmsTable.Rows.Add(MakeRealmsColHeaderRow());

            foreach (string name in realmNames)
            {
                TableRow row = new TableRow();
                TableCell cell;
                string thisName = name; // capture for the delegates

                // realm name
                cell = new TableCell();
                cell.Text = name;
                row.Cells.Add(cell);

                // owner
                cell = new TableCell();
                cell.Text = controller.GetRealmOwner(name);
                row.Cells.Add(cell);

                string encodedID = Common.EncodeAsID(thisName);

                // View Source button
                cell = new TableCell();
                Button viewSrcBtn = new Button();
                viewSrcBtn.ID = "srcbtn_" + encodedID;
                if (canEdit)
                    viewSrcBtn.Text = "Edit Source";
                else
                    viewSrcBtn.Text = "View Source";
                if (canView)
                    viewSrcBtn.Click += delegate { DoViewSource(thisName); };
                else
                    viewSrcBtn.Enabled = false;
                cell.Controls.Add(viewSrcBtn);
                row.Cells.Add(cell);

                // View Index button
                cell = new TableCell();
                Button viewIndexBtn = new Button();
                viewIndexBtn.ID = "idxbtn_" + encodedID;
                viewIndexBtn.Text = "View Index";
                if (canView)
                    viewIndexBtn.Click += delegate { DoViewIndex(thisName); };
                else
                    viewIndexBtn.Enabled = false;
                cell.Controls.Add(viewIndexBtn);
                row.Cells.Add(cell);

                // Access List button
                cell = new TableCell();
                Button accessListBtn = new Button();
                accessListBtn.ID = "aclbtn_" + encodedID;
                accessListBtn.Text = "Edit Access List";
                if (controller.GetAccessLevel(playerName, thisName) >= RealmAccessLevel.EditAccess)
                    accessListBtn.Click += delegate { DoEditAccess(thisName); };
                else
                    accessListBtn.Enabled = false;
                cell.Controls.Add(accessListBtn);
                row.Cells.Add(cell);

                realmsTable.Rows.Add(row);
            }
        }

        void DoViewSource(string realmName)
        {
            Response.Redirect("EditRealm.aspx?realm=" + HttpUtility.UrlEncode(realmName));
        }

        void DoViewIndex(string realmName)
        {
            Response.Redirect("ViewIndex.aspx?realm=" + HttpUtility.UrlEncode(realmName));
        }

        void DoEditAccess(string realmName)
        {
            Response.Redirect("EditAccess.aspx?realm=" + HttpUtility.UrlEncode(realmName));
        }

        protected void newRealmBtn_Click(object sender, EventArgs e)
        {
            string srcLang;
            Common.GetRealmFactories(controller, out srcLang);

            if (controller.CreateRealm(Context.User.Identity.Name, newRealmNameBox.Text, srcLang) == false)
            {
                newRealmErrorLabel.Text = "Invalid realm name, name already in use, or permission denied.";
            }
            else
            {
                DoViewSource(newRealmNameBox.Text);
            }
        }
    }
}
