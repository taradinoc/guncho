<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="EditRealm.aspx.cs" Inherits="ControlPanel.EditRealm" ValidateRequest="false" %>
<%@ Register TagPrefix="cp" TagName="Header" Src="~/header.ascx" %>
<%@ Register TagPrefix="cp" TagName="Footer" Src="~/footer.ascx" %>
<%@ Register TagPrefix="cp" Namespace="ControlPanel" Assembly="ControlPanel" %>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">

<html xmlns="http://www.w3.org/1999/xhtml" >
<head runat="server">
    <title>Edit Realm Source - Guncho Control Panel</title>
    <script language="javascript" type="text/javascript" src="edit_area/edit_area_full.js"></script>
    <script language="javascript" type="text/javascript">
        editAreaLoader.init({
	        id : "sourceBox"	    	// textarea id
	        ,syntax: "inform7"			// syntax to be used for highlighting
	        ,start_highlight: true		// to display with highlight mode on start-up
	        ,font_family: "tahoma,verdana,sans-serif"
	        ,plugins: "i7headings"
	        ,end_toolbar: "|,i7headings"
        });
        
        function allowTab(el) {
            if (el.setSelectionRange) {
                var sel = el.selectionStart;
                el.value = el.value.substring(0,el.selectionStart) +
                    String.fromCharCode(9) +
                    el.value.substring(el.selectionEnd,el.value.length);
                el.selectionStart = sel + 1;
                el.selectionEnd = sel + 1;
            } else {
                el.selection = document.selection.createRange();
                el.selection.text = String.fromCharCode(9);
            }
            return false;
        }
    </script>
</head>
<body>
    <form id="form1" runat="server">
        <cp:Header runat="server" />

        <asp:Panel ID="errorPanel" Visible="false" runat="server">
            <asp:Label ID="errorLabel" ForeColor="Red" runat="server" />
            <br />
            <a href="Default.aspx">Go back.</a>
        </asp:Panel>
        
        <asp:Panel ID="sourcePanel" Visible="true" runat="server">
            <p><asp:Label ID="sourceRealmLabel" runat="server" /></p>
            
            <table width="100%">
                <tr>
                    <th width="10%">Privacy level:</th>
                    <td>
                        <asp:DropDownList ID="privacyDrop" runat="server">
                            <asp:ListItem Value="private">Private</asp:ListItem>
                            <asp:ListItem Value="hidden">Hidden</asp:ListItem>
                            <asp:ListItem Value="public">Visible</asp:ListItem>
                            <asp:ListItem Value="joinable">Anyone can teleport in</asp:ListItem>
                            <asp:ListItem Value="viewable">Anyone can view source</asp:ListItem>
                        </asp:DropDownList>
                    </td>
                </tr>
                <tr id="deleteRow" runat="server">
                    <th>Delete realm:</th>
                    <td>
                        <asp:CheckBox ID="confirmDeleteChk" Text="I'm sure!" runat="server" />
                        <asp:Button ID="deleteRealmBtn" Text="Delete Realm"
                        OnClick="deleteRealmBtn_Click" runat="server" />
                    </td>
                </tr>
                <tr>
                    <th>Source language:</th>
                    <td>
                        <asp:DropDownList ID="srcLangDrop" runat="server"></asp:DropDownList>
                    </td>
                </tr>
                <tr>
                    <th valign="top">Source code:</th>
                    <td>
                        <asp:TextBox ID="sourceBox" runat="server" Height="350px" Width="90%"
                        TextMode="MultiLine" Columns="60" Rows="30" />
                    </td>
                </tr>
            </table>
            
            <p><asp:Button ID="sourceSaveBtn" Text="Save Changes" runat="server" OnClick="sourceSaveBtn_Click" />
            <asp:Button ID="sourceCancelBtn" Text="Cancel" runat="server" OnClick="sourceCancelBtn_Click" /></p>
        </asp:Panel>
        
        <asp:Panel ID="resultsPanel" Visible="false" BorderWidth="1" BackColor="lightgray" runat="server">
            <cp:InformHtml ID="resultsView" runat="server" />
            <asp:Label ID="resultsErrorLabel" Visible="false" runat="server" />
            <br />
            <asp:Button ID="viewIndexBtn" Text="View Index" OnClick="viewIndexBtn_Click" runat="server" />
        </asp:Panel>
        
        <cp:Footer runat="server" />
    </form>
</body>
</html>
