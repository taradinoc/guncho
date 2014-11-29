using Microsoft.AspNet.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Guncho.Api
{
    public class ApiUser : IUser<int>
    {
        public ApiUser()
        {
        }

        internal ApiUser(int id)
        {
            this.Id = id;
        }

        #region IUser<int> Members

        public int Id { get; private set; }

        public string UserName { get; set; }

        #endregion
    }
}
