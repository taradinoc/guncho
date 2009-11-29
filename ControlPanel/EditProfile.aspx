<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="EditProfile.aspx.cs" Inherits="ControlPanel.EditProfile" %>
<%@ Register TagPrefix="cp" TagName="Header" Src="~/header.ascx" %>
<%@ Register TagPrefix="cp" TagName="Footer" Src="~/footer.ascx" %>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">

<html xmlns="http://www.w3.org/1999/xhtml" >
<head runat="server">
    <title>Edit Profile - Guncho Control Panel</title>
</head>
<body>
    <form id="form1" runat="server">
        <cp:Header runat="server" />

        <asp:Panel BorderWidth="1" BackColor="lightgray" runat="server">
            <b>Gender:</b>
            <br />
            <asp:RadioButtonList ID="genderRadios" runat="server">
                <asp:ListItem Value="m">Male</asp:ListItem>
                <asp:ListItem Value="f">Female</asp:ListItem>
                <asp:ListItem Value="n">Neuter</asp:ListItem>
            </asp:RadioButtonList>
        </asp:Panel>
        
        <br />
        
        <asp:Panel BorderWidth="1" BackColor="lightgray" runat="server">
            <b>Description:</b>
            <br />
            <asp:TextBox ID="descriptionBox" TextMode="MultiLine" Width="400px" Height="120px" runat="server" />
        </asp:Panel>
        
        <br />
        
        <asp:Panel BorderWidth="1" BackColor="lightgray" runat="server">
            <b>Change Password:</b>
            <br />
            <table>
                <tr>
                    <td align="right">Old password:</td>
                    <td><asp:TextBox ID="oldPassword" TextMode="Password" runat="server" /></td>
                </tr>
                <tr>
                    <td align="right">New password:</td>
                    <td><asp:TextBox ID="newPassword1" TextMode="Password" runat="server" /></td>
                </tr>
                <tr>
                    <td align="right">Verify new password:</td>
                    <td><asp:TextBox ID="newPassword2" TextMode="Password" runat="server" /></td>
                </tr>
            </table>
        </asp:Panel>
        
        <p><asp:Label ID="errorLabel" ForeColor="red" runat="server" /></p>
        
        <asp:Button ID="saveChangesBtn" Text="Save Changes" OnClick="saveChangesBtn_Click" runat="server" />
        <asp:Button ID="cancelBtn" Text="Cancel" OnClick="cancelBtn_Click" runat="server" />
        
        <cp:Footer runat="server" />
    </form>
</body>
</html>
