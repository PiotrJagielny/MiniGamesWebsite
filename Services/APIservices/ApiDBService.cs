﻿using Models.UsersManagment;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.APIservices
{
    public interface ApiDBService
    {
        Task<bool> IsLoginDataValid(UserLoginData userLoginData);
        Task<bool> RegisterUser(UserLoginData UserDataToRegister);
        Task<bool> IsUserAlreadyLogged(UserLoginData UserDataToRegister);
    }
}
