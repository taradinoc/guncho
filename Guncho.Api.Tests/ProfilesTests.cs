using System;
using Microsoft.Owin;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MyTested.WebApi;
using Guncho.Api.Controllers;
using System.Collections.Generic;
using System.Web.Http;
using System.Linq;
using System.Net.Http;
using Thinktecture.IdentityModel.Owin.ResourceAuthorization;
using Guncho.Api.Security;
using System.Net;

namespace Guncho.Api.Tests
{
    [TestClass]
    public class ProfilesTests
    {
        private TestRig rig;

        [TestInitialize]
        public void TestInitialize()
        {
            rig = new TestRig();
        }

        [TestMethod]
        public void Controller_Should_Require_Login()
        {
            MyWebApi
                .Controller<ProfilesController>()
                .WithTestRig(rig)
                .ShouldHave()
                .Attributes(a => a.RestrictingForAuthorizedRequests());
        }

        #region GET profiles

        [TestMethod]
        public void GET_profiles_Should_Return_All_User_Profiles()
        {
            MyWebApi
                .Routes()
                .ShouldMap("api/profiles")
                .To<ProfilesController>(c => c.Get());

            var profiles = MyWebApi
                .Controller<ProfilesController>()
                .WithTestRig(rig)
                .WithAuthenticatedUser(rig.WizardUser())
                .Calling(c => c.Get())
                .ShouldReturn()
                .NotNull()
                .AndProvideTheActionResult()
                .ToList();

            Assert.AreEqual(2, profiles.Count);
            Assert.AreEqual(1, profiles.Count(dto => dto.Name == "Wizard"));
            Assert.AreEqual(1, profiles.Count(dto => dto.Name == "Peon"));
        }

        #endregion

        #region GET profiles/my

        [TestMethod]
        public void GET_profiles_my_Should_Return_Current_User_Profile()
        {
            MyWebApi
                .Routes()
                .ShouldMap("api/profiles/my")
                .To<ProfilesController>(c => c.GetMy());

            MyWebApi
                .Controller<ProfilesController>()
                .WithTestRig(rig)
                .WithAuthenticatedUser(rig.WizardUser())
                .Calling(c => c.GetMy())
                .ShouldReturn()
                .Ok()
                .WithResponseModelOfType<ProfileDto>()
                .Passing(dto => dto.Name == "Wizard" && dto.Uri.EndsWith("/api/profiles/Wizard"));
        }

        #endregion

        #region GET profiles/{name}

        [TestMethod]
        public void GET_profiles_name_Should_Return_Named_User_Profile()
        {
            MyWebApi
                .Routes()
                .ShouldMap("api/profiles/Wizard")
                .To<ProfilesController>(c => c.GetProfileByName("Wizard"));

            MyWebApi
                .Controller<ProfilesController>()
                .WithTestRig(rig)
                .WithAuthenticatedUser(rig.WizardUser())
                .Calling(c => c.GetProfileByName("Wizard"))
                .ShouldReturn()
                .Ok()
                .WithResponseModelOfType<ProfileDto>()
                .Passing(dto => dto.Name == "Wizard" && dto.Uri.EndsWith("/api/profiles/Wizard"));
        }

        [TestMethod]
        public void GET_profiles_name_With_Invalid_Name_Should_Return_NotFound()
        {
            MyWebApi
                .Controller<ProfilesController>()
                .WithTestRig(rig)
                .WithAuthenticatedUser(rig.WizardUser())
                .Calling(c => c.GetProfileByName("NoSuchUser"))
                .ShouldReturn()
                .NotFound();
        }

        #endregion

        #region PUT profiles/{name}

        [TestMethod]
        public void PUT_profile_name_Should_Update_Named_Profile()
        {
            MyWebApi
                .Routes()
                .ShouldMap(r =>
                    r
                    .WithRequestUri("/api/profiles/Peon")
                    .WithMethod(HttpMethod.Put)
                    .WithJsonContent(@"{name: ""Peon"", attributes: {description: ""a peon""}"))
                .To<ProfilesController>(c => c.PutProfileByName(
                    "Peon",
                    new ProfileDto()
                    {
                        Name = "Peon",
                        Attributes = new Dictionary<string, string>
                        {
                            { "description", "a peon" }
                        }
                    }));

            MyWebApi
                .Controller<ProfilesController>()
                .WithTestRig(rig)
                .WithAuthenticatedUser(rig.PeonUser())
                .Calling(c => c.PutProfileByName(
                    "Peon",
                    new ProfileDto()
                    {
                        Name = "Peon",
                        Attributes = new Dictionary<string, string>
                        {
                            { "description", "a peon" }
                        },
                    }))
                .ShouldReturn()
                .Ok()
                .WithResponseModelOfType<ProfileDto>();
        }

        [TestMethod]
        public void PUT_profile_name_Should_Reject_NonAdmin_Changing_Other_Player()
        {
            MyWebApi
                .Controller<ProfilesController>()
                .WithTestRig(rig)
                .WithAuthenticatedUser(rig.PeonUser())
                .Calling(c => c.PutProfileByName(
                    "Wizard",
                    new ProfileDto() { Name = "Wizard" }))
                .ShouldReturn()
                .ResultOfType<ForbiddenResult>();
        }

        [TestMethod]
        public void PUT_profile_name_Should_Allow_Admin_Changing_Other_Player()
        {
            MyWebApi
                .Controller<ProfilesController>()
                .WithTestRig(rig)
                .WithAuthenticatedUser(rig.WizardUser())
                .Calling(c => c.PutProfileByName(
                    "Peon",
                    new ProfileDto() { Name = "Peon" }))
                .ShouldReturn()
                .Ok();
        }

        [TestMethod]
        public void PUT_profile_name_Should_Reject_Unknown_Attributes()
        {
            MyWebApi
                .Controller<ProfilesController>()
                .WithTestRig(rig)
                .WithAuthenticatedUser(rig.PeonUser())
                .Calling(c => c.PutProfileByName(
                    "Peon",
                    new ProfileDto()
                    {
                        Name = "Peon",
                        Attributes = new Dictionary<string, string>
                        {
                            { "no_such_attribute", "this attribute is unknown" }
                        },
                    }))
                .ShouldReturn()
                .BadRequest()
                .WithModelStateFor<ProfileDto>()
                .ContainingModelStateErrorFor(dto => dto.Attributes);
        }

        #endregion
    }
}
