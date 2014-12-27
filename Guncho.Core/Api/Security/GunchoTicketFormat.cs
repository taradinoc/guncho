using System;
using System.Security;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.DataProtection;
using Microsoft.Owin.Security.DataHandler.Encoder;
using Microsoft.Owin.Security.DataHandler.Serializer;
using System.IO;
using System.IO.Compression;
using System.Security.Claims;
using System.Linq;
using System.Diagnostics.Contracts;

namespace Guncho.Api.Security
{
    /* The default ticket serializer in Microsoft.Owin.Security depends on a BootstrapContext
     * class that isn't part of the latest Mono release (as of December 2014), so here we define
     * GunchoTicketFormat, which is compatible with the default serializer but doesn't use
     * BootstrapContext.
     * 
     * This can be removed once the patch for https://bugzilla.xamarin.com/show_bug.cgi?id=22560
     * has been included in a Mono release.
     */

    // http://stackoverflow.com/questions/21805755/using-oauth-tickets-across-several-services
    public sealed class GunchoTicketFormat : ISecureDataFormat<AuthenticationTicket>
    {
        #region Fields

        private readonly IDataSerializer<AuthenticationTicket> serializer;
        private readonly IDataProtector protector;
        private readonly ITextEncoder encoder;

        #endregion Fields

        #region Constructors

        public GunchoTicketFormat(byte[] key)
        {
            Contract.Requires(key != null);

            this.serializer = new GunchoTicketSerializer();
            this.protector = new GunchoDataProtector(key);
            this.encoder = TextEncodings.Base64Url;
        }

        #endregion Constructors

        #region ISecureDataFormat<AuthenticationTicket> Members

        public string Protect(AuthenticationTicket ticket)
        {
            var ticketData = this.serializer.Serialize(ticket);
            var ticketData2 = new GunchoTicketSerializer().Serialize(ticket);
            var protectedData = this.protector.Protect(ticketData);
            var protectedString = this.encoder.Encode(protectedData);
            return protectedString;
        }

        public AuthenticationTicket Unprotect(string text)
        {
            var protectedData = this.encoder.Decode(text);
            var ticketData = this.protector.Unprotect(protectedData);
            var ticket = this.serializer.Deserialize(ticketData);
            return ticket;
        }

        #endregion ISecureDataFormat<AuthenticationTicket> Members
    }

    public sealed class GunchoTicketSerializer : IDataSerializer<AuthenticationTicket>
    {
        private const int Version = 2;
        private const string Placeholder = "\0";
        private const string DefaultNameClaimType = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name";
        private const string DefaultRoleClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";
        private const string DefaultIssuer = "LOCAL AUTHORITY";
        private const string DefaultValueType = "http://www.w3.org/2001/XMLSchema#string";

        #region IDataSerializer<AuthenticationTicket> Members

        public AuthenticationTicket Deserialize(byte[] data)
        {
            using (var mstr = new MemoryStream(data))
            using (var gzip = new GZipStream(mstr, CompressionMode.Decompress))
            using (var rdr = new BinaryReader(gzip))
            {
                var version = rdr.ReadInt32();
                if (version != Version)
                {
                    return null;
                }

                var authenticationType = rdr.ReadString();
                var nameClaimType = ReadDefaultString(rdr, DefaultNameClaimType);
                var roleClaimType = ReadDefaultString(rdr, DefaultRoleClaimType);

                var identity = new ClaimsIdentity(authenticationType, nameClaimType, roleClaimType);

                var claimCount = rdr.ReadInt32();
                for (int i = 0; i < claimCount; i++)
                {
                    var type = ReadDefaultString(rdr, nameClaimType);
                    var value = rdr.ReadString();
                    var valueType = ReadDefaultString(rdr, DefaultValueType);
                    var issuer = ReadDefaultString(rdr, DefaultIssuer);
                    var originalIssuer = ReadDefaultString(rdr, issuer);

                    identity.AddClaim(new Claim(type, value, valueType, issuer, originalIssuer));
                }

                var bootstrapLength = rdr.ReadInt32();
                if (bootstrapLength != 0)
                {
                    return null;
                }

                var properties = PropertiesSerializer.Read(rdr);

                return new AuthenticationTicket(identity, properties);
            }
        }

        private static string ReadDefaultString(BinaryReader rdr, string defaultValue)
        {
            var result = rdr.ReadString();
            return result == Placeholder ? defaultValue : result;
        }

        public byte[] Serialize(AuthenticationTicket model)
        {
            using (var mstr = new MemoryStream())
            {
                using (var gzip = new GZipStream(mstr, CompressionLevel.Optimal))
                using (var wtr = new BinaryWriter(gzip))
                {
                    wtr.Write(Version);

                    var identity = model.Identity;

                    wtr.Write(identity.AuthenticationType);
                    WriteDefaultString(wtr, identity.NameClaimType, DefaultNameClaimType);
                    WriteDefaultString(wtr, identity.RoleClaimType, DefaultRoleClaimType);

                    wtr.Write(identity.Claims.Count());
                    foreach (var claim in identity.Claims)
                    {
                        WriteDefaultString(wtr, claim.Type, identity.NameClaimType);
                        wtr.Write(claim.Value);
                        WriteDefaultString(wtr, claim.ValueType, DefaultValueType);
                        WriteDefaultString(wtr, claim.Issuer, DefaultIssuer);
                        WriteDefaultString(wtr, claim.OriginalIssuer, claim.Issuer);
                    }

                    wtr.Write(0);

                    PropertiesSerializer.Write(wtr, model.Properties);
                }

                // GZipStream must be closed before we can read the compressed bytes.
                return mstr.ToArray();
            }
        }

        private static void WriteDefaultString(BinaryWriter wtr, string value, string defaultValue)
        {
            wtr.Write(value == defaultValue ? Placeholder : value);
        }

        #endregion
    }
}
