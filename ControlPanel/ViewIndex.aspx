<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="ViewIndex.aspx.cs" Inherits="ControlPanel.ViewIndex" %>
<%@ Register TagPrefix="cp" TagName="Header" Src="~/header.ascx" %>
<%@ Register TagPrefix="cp" TagName="Footer" Src="~/footer.ascx" %>
<%@ Register TagPrefix="cp" Namespace="ControlPanel" Assembly="ControlPanel" %>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">

<html xmlns="http://www.w3.org/1999/xhtml" >
<head runat="server">
    <title>View Realm Index - Guncho Control Panel</title>
</head>
<body>
    <form id="form1" runat="server">
        <cp:Header runat="server" />
        
        <asp:Panel ID="errorPanel" Visible="false" runat="server">
            <asp:Label ID="errorLabel" ForeColor="Red" runat="server" />
            <br />
            <a href="default.aspx">Go back.</a>
        </asp:Panel>
        
        <asp:Panel ID="indexPanel" runat="server">
            <asp:Panel BorderWidth="1" BackColor="lightgray" runat="server">
                Viewing index for realm "<asp:Label ID="indexRealmLabel" runat="server" />":
                <br />
                <asp:HyperLink ID="problemsLink" runat="server">Compiler Output</asp:HyperLink> |
                <asp:HyperLink ID="contentsLink" runat="server">Contents</asp:HyperLink> |
                <asp:HyperLink ID="actionsLink" runat="server">Actions</asp:HyperLink> |
                <asp:HyperLink ID="kindsLink" runat="server">Kinds</asp:HyperLink> |
                <asp:HyperLink ID="phrasesLink" runat="server">Phrases</asp:HyperLink> |
                <asp:HyperLink ID="rulesLink" runat="server">Rules</asp:HyperLink> |
                <asp:HyperLink ID="scenesLink" runat="server">Scenes</asp:HyperLink> |
                <asp:HyperLink ID="worldLink" runat="server">World</asp:HyperLink>
                <br />
                <asp:Button ID="backButton" Text="Close Index" OnClick="backButton_Click" runat="server" />
            </asp:Panel>
            
            <asp:Label ID="indexErrorLabel" Visible="false" ForeColor="Red" runat="server" />
            <cp:InformHtml ID="indexView" runat="server" />
        </asp:Panel>
        
        <cp:Footer runat="server" />
    </form>
</body>
</html>
