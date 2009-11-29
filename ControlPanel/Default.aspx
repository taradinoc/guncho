<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="ControlPanel._Default" %>
<%@ Register TagPrefix="cp" TagName="Header" Src="~/header.ascx" %>
<%@ Register TagPrefix="cp" TagName="Footer" Src="~/footer.ascx" %>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">

<html xmlns="http://www.w3.org/1999/xhtml" >
<head runat="server">
    <title>Guncho Control Panel</title>
    <style type="text/css">
        .realmsSection { background-color: #eeeeee; font-style: italic; }
    </style>
</head>
<body>
    <form id="form1" runat="server">
        <cp:Header runat="server" />
        
        <asp:Panel ID="whoPanel" runat="server">
            <h2>Connected Players</h2>
            
            <p><asp:Table ID="whoTable" runat="server">
                <asp:TableHeaderRow runat="server">
                    <asp:TableHeaderCell runat="server">Player</asp:TableHeaderCell>
                    <asp:TableHeaderCell runat="server">Connected</asp:TableHeaderCell>
                    <asp:TableHeaderCell runat="server">Idle</asp:TableHeaderCell>
                </asp:TableHeaderRow>
            </asp:Table></p>
        </asp:Panel>
        
        <hr />
        
        <asp:Panel ID="realmsPanel" runat="server">
            <h2>Realms</h2>
            
            <p><asp:Table ID="realmsTable" runat="server" /></p>
            
            <p><asp:TextBox ID="newRealmNameBox" runat="server" />
            <asp:Button ID="newRealmBtn" Text="Create New Realm" runat="server" OnClick="newRealmBtn_Click" />
            <asp:Label ID="newRealmErrorLabel" runat="server"
             ForeColor="red" Font-Names="Verdana" Font-Size="10" /></p>
        </asp:Panel>
        
        <cp:Footer runat="server" />
    </form>
</body>
</html>
