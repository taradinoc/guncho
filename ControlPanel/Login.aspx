<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Login.aspx.cs" Inherits="ControlPanel.Login" %>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">

<html xmlns="http://www.w3.org/1999/xhtml" >
<head runat="server">
    <title>Log In - Guncho Control Panel</title>
</head>
<body>
    <form id="form1" runat="server">
        <h1>Welcome to Guncho</h1>
        <table>
            <tr>
                <td>Player Name:</td>
                <td><input id="UserName" type="text" runat="server"/></td>
                <td><ASP:RequiredFieldValidator ID="RequiredFieldValidator1"
                ControlToValidate="UserName" Display="Static" ErrorMessage="*" runat="server"/></td>
            </tr>
            <tr>
                <td>Password:</td>
                <td><input id="UserPass" type="password" runat="server"/></td>
                <td><ASP:RequiredFieldValidator ID="RequiredFieldValidator2"
                ControlToValidate="UserPass" Display="Static" ErrorMessage="*" runat="server"/></td>
            </tr>
            <tr>
                <td colspan="2" align="right"><asp:Button ID="btnLogin" text="Log In" runat="server"
                OnClick="btnLogin_Click"/></td>
            </tr>
            <tr>
                <td>Verify Password:</td>
                <td><input id="VerifyPass" type="password" runat="server" /></td>
            </tr>
            <tr>
                <td colspan="2" align="right"><asp:Button ID="btnCreatePlayer"
                Text="Create New Player" runat="server" OnClick="btnCreatePlayer_Click" /></td>
            </tr>
        </table>

        <p><asp:Label id="Msg" ForeColor="red" Font-Names="Verdana" Font-Size="10" runat="server" /></p>
    </form>
</body>
</html>
