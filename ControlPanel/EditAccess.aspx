<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="EditAccess.aspx.cs" Inherits="ControlPanel.EditAccess" %>
<%@ Register TagPrefix="cp" TagName="Header" Src="~/Header.ascx" %>
<%@ Register TagPrefix="cp" TagName="Footer" Src="~/Footer.ascx" %>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">

<html xmlns="http://www.w3.org/1999/xhtml" >
<head runat="server">
    <title>Edit Realm Access - Guncho Control Panel</title>
</head>
<body>
    <form id="form1" runat="server">
        <cp:Header runat="server" />
        
        <asp:Table ID="aclEntriesTable" runat="server">
            <asp:TableHeaderRow>
                <asp:TableHeaderCell>Player</asp:TableHeaderCell>
                <asp:TableHeaderCell>Access Level</asp:TableHeaderCell>
                <asp:TableHeaderCell>Action</asp:TableHeaderCell>
            </asp:TableHeaderRow>
            <asp:TableRow ID="emptyRow">
                <asp:TableCell><asp:TextBox ID="newEntryName" runat="server" /></asp:TableCell>
                <asp:TableCell>
                    <asp:DropDownList ID="newEntryLevel" runat="server">
                        <asp:ListItem>------------</asp:ListItem>
                        <asp:ListItem>Visible</asp:ListItem>
                        <asp:ListItem>Invited</asp:ListItem>
                        <asp:ListItem>ViewSource</asp:ListItem>
                        <asp:ListItem>EditSource</asp:ListItem>
                        <asp:ListItem>EditSettings</asp:ListItem>
                        <asp:ListItem>EditAccess</asp:ListItem>
                        <asp:ListItem>SafetyOff</asp:ListItem>
                    </asp:DropDownList>
                </asp:TableCell>
                <asp:TableCell><asp:Button ID="addEntryBtn" Text="Add" OnClick="addEntryBtn_Click" runat="server" /></asp:TableCell>
            </asp:TableRow>
        </asp:Table>
        <br />
        <asp:Button ID="doneBtn" Text="Done" OnClick="doneBtn_Click" runat="server" />
        <asp:Label ID="errorLabel" ForeColor="Red" runat="server" />
    
        <cp:Footer runat="server" />
    </form>
</body>
</html>
