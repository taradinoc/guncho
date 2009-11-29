<%@ Control Language="C#" AutoEventWireup="true" CodeBehind="Header.ascx.cs" Inherits="ControlPanel.Header" %>

<p><small><asp:Label ID="userGreetingLabel" runat="server" />
<asp:Label ID="userActionsLabel" runat="server">[ <asp:HyperLink ID="homeLink" NavigateUrl="~/Default.aspx" runat="server">Home</asp:HyperLink>
| <asp:HyperLink ID="editProfileLink" NavigateUrl="~/EditProfile.aspx" runat="server">Edit Profile</asp:HyperLink>
<asp:Label ID="adminLabel" Visible="false" runat="server"> | <asp:HyperLink ID="adminLink" NavigateUrl="~/Admin.aspx" runat="server">Admin</asp:HyperLink></asp:Label>
| <asp:LinkButton ID="logoutLink" OnClick="logoutLink_Click" runat="server">Log Out</asp:LinkButton>
]</asp:Label></small></p>

<hr />
