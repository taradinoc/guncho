<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Admin.aspx.cs" Inherits="ControlPanel.Admin" %>
<%@ Register TagPrefix="cp" TagName="Header" Src="~/Header.ascx" %>
<%@ Register TagPrefix="cp" TagName="Footer" Src="~/Footer.ascx" %>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">

<html xmlns="http://www.w3.org/1999/xhtml" >
<head runat="server">
    <title>Adminstration - Guncho Control Panel</title>
</head>
<body>
    <form id="form1" runat="server">
        <cp:Header runat="server" />
        
        <asp:Label ID="errorLabel" ForeColor="red" runat="server" />
        
        <asp:Panel ID="adminPanel" runat="server">
            <asp:Panel ID="shutdownPanel" runat="server">
                <b>Shut down the server:</b>
                <br />
                <asp:CheckBox ID="chkConfirmShutdown" Text="Really!" runat="server" />
                <asp:Button ID="btnShutdown" Text="Shut Down" OnClick="btnShutdown_Click" runat="server" />
            </asp:Panel>
            <asp:Panel ID="passwdPanel" runat="server">
                <b>Reset a player's password:</b>
                <br />
                <asp:TextBox ID="txtResetUser" runat="server" />
                <asp:TextBox ID="txtResetPasswd" TextMode="Password" runat="server" />
                <asp:Button ID="btnResetPasswd" Text="Change Password" OnClick="btnResetPasswd_Click" runat="server" />
            </asp:Panel>
        </asp:Panel>
        
        <cp:Footer runat="server" />
    </form>
</body>
</html>
